using CityWorker.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityWorker.Services;

public sealed class BootstrapOptions{
    public int TopN{get;set;}=1000;
    public int RefreshMinutes { get;set;}=0;

}
public sealed class CityBootstrapper(
    WikidataClient wikidata,
    ICityCache cache,
    IOptions<BootstrapOptions> opt,
    ILogger<CityBootstrapper> log
):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

    }
    private async Task LoadOnce(CancellationToken ct)
    {
        log.LogInformation("Loading TopN={TopN} cities from Wikidata..",opt.Value.TopN);
        var rows = await wikidata.GetTopUsCitiesAsync(opt.Value.TopN,ct);

        var list = rows.Select(r=>new CityEntry(r.City,r.State,r.StateCode,r.Population,r.Lat,r.Lon,r.WikipediaTitle))
                    .OrderByDescending(x=>x.Population)
                    .ToList();
        cache.ReplaceAll(list);

        log.LogInformation("Loaded {Count} cities into cache",cache.Count);

    }
}