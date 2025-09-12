using CityWorker.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace CityWorker.Services;

public sealed class BootstrapOptions{
    public int TopN{get;set;}=1000;
    public int RefreshMinutes { get;set;}=0;
}

public sealed class CityBootstrapper(
    WikidataClient _wikidata,
    ICityCache _cache,
    IOptions<BootstrapOptions> _options,
    ILogger<CityBootstrapper> _log,
    SnowflakeIdGenerator _idGen
)
{
    public event EventHandler<List<CityEntry>>? CitiesLoaded;

    public async Task StartAsync(CancellationToken stoppingToken=default)
    {
        await LoadOnce(stoppingToken);

        var refresh = Math.Max(0,_options.Value.RefreshMinutes);
        if (refresh == 0) return;

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(refresh));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await LoadOnce(stoppingToken);
        }
        catch (OperationCanceledException ex)
        {
            _log.LogError(ex,"Erorr in running City BootStrapper service");
        }
    }

    private static string MakeCityKey(string? city, string? stateCode, string? state, string? wikipediaTitle)
    {
        // Prefer Wikipedia title (usually unique), then City+StateCode, then City+State
        string norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
        var byWiki = norm(wikipediaTitle);
        if (!string.IsNullOrEmpty(byWiki)) return $"wiki:{byWiki}";
        var byCode = $"{norm(city)}|{norm(stateCode)}";
        if (!byCode.EndsWith("|")) return $"cs:{byCode}";
        return $"cS:{norm(city)}|{norm(state)}";
    }

    private static IEnumerable<T> DistinctBest<T>(
        IEnumerable<T> rows,
        Func<T,string> keySelector,
        Func<T,long> populationSelector)
    {
        // Keep one row per key; if duplicates, keep the highest population
        return rows
            .GroupBy(keySelector)
            .Select(g => g.OrderByDescending(populationSelector).First());
    }

    private async Task LoadOnce(CancellationToken ct)
    {
        _log.LogInformation("Loading TopN={TopN} cities from Wikidata...", _options.Value.TopN);
        var startTime = DateTime.UtcNow;
        var cacheFile = "cities_cache.json";
        List<CityEntry> cityList = new();

        try
        {
            // 1) Try cache first, but de-duplicate the cached list before using it
            if (File.Exists(cacheFile))
            {
                _log.LogInformation("Checking cached city data...");
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(cacheFile, ct);
                    var cachedRows = JsonSerializer.Deserialize<List<CityEntry>>(cachedJson);

                    if (cachedRows != null && cachedRows.Any())
                    {
                        // De-dup cached content to avoid propagating duplicates
                        var distinctCached = DistinctBest(
                            cachedRows,
                            r => MakeCityKey(r.City, r.StateCode, r.State, r.WikipediaTitle),
                            r => r.Population
                        ).ToList();

                        if (distinctCached.Count != cachedRows.Count)
                        {
                            _log.LogInformation("Removed {Dupes} duplicate cached entries.", cachedRows.Count - distinctCached.Count);
                            // Rewrite the cache file with the de-duped set
                            await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(distinctCached), ct);
                        }

                        foreach (var r in distinctCached)
                        {
                            long cityId = _idGen.NextId();
                            cityList.Add(new CityEntry(
                                cityId,
                                cityId,
                                r.City,
                                r.State,
                                r.StateCode,
                                r.Population,
                                r.Lat,
                                r.Lon,
                                r.WikipediaTitle
                            ));
                        }

                        cityList = cityList.OrderByDescending(x => x.Population).ToList();
                        _cache.ReplaceAll(cityList);
                        _log.LogInformation("Loaded {Count} cities from cache", _cache.Count);
                    }
                    else
                    {
                        _log.LogWarning("Cached data is empty or invalid, fetching from Wikidata");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to deserialize cached city data, fetching from Wikidata");
                }
            }

            // 2) If no usable cache, fetch, de-duplicate, then save ONLY the distinct set
            if (!cityList.Any())
            {
                var rows = await _wikidata.GetTopUsCitiesAsync(_options.Value.TopN, ct);
                _log.LogInformation("Wikidata query took {Duration} seconds", (DateTime.UtcNow - startTime).TotalSeconds);

                // De-dup fresh results before building entries / saving to file
                var distinctRows = DistinctBest(
                    rows,
                    r => MakeCityKey(r.City, r.StateCode, r.State, r.WikipediaTitle),
                    r => r.Population
                ).ToList();

                var removed = rows.Count() - distinctRows.Count;
                if (removed > 0)
                    _log.LogInformation("Removed {Count} duplicate rows from Wikidata results.", removed);

                foreach (var r in distinctRows)
                {
                    long cityId = _idGen.NextId();
                    cityList.Add(new CityEntry(
                        cityId,
                        cityId,
                        r.City,
                        r.State,
                        r.StateCode,
                        r.Population,
                        r.Lat,
                        r.Lon,
                        r.WikipediaTitle
                    ));
                }

                cityList = cityList.OrderByDescending(x => x.Population).ToList();
                _cache.ReplaceAll(cityList);

                // Save ONLY the distinct set back to cache (prevents duplicates in JSON file)
                await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(distinctRows), ct);
                _log.LogInformation("Loaded {Count} cities into cache and saved to file", _cache.Count);
            }

            _log.LogInformation("Invoking CitiesLoaded event with {Count} cities", cityList.Count);
            CitiesLoaded?.Invoke(this, cityList);
            _log.LogInformation("CitiesLoaded event invoked successfully");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error loading cities from Wikidata or cache");
            throw;
        }
    }

    private Task<long> GenerateCityId(string city, string state, long population)
    {
        var cityKey = $"{city}_{state}_{population}";
        return Task.FromResult((long)cityKey.GetHashCode());
    }
}
