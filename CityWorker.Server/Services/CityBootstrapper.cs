using CityWorker.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
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
        await LoadOnce(stoppingToken);

        var refresh = Math.Max(0,opt.Value.RefreshMinutes);
        if(refresh==0) return;

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(refresh));
        try{
            while(await timer.WaitForNextTickAsync(stoppingToken))
             await LoadOnce(stoppingToken);
        }
        catch(OperationCanceledException ex)
        {
            log.LogError(ex,"Erorr in running City BootStrapper service");
        }
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