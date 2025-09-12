using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using CityWorker.Services;
public class CityWorkerService:BackgroundService
{
    private readonly ILogger<CityWorkerService> _logger;
    private CityBootstrapper _bootStrapper;
    public CityWorkerService(ILogger<CityWorkerService> logger,CityBootstrapper bootStrapper)
    {
        _logger = logger;
        _bootStrapper = bootStrapper;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bootStrapper.StartAsync(stoppingToken);
        _logger.LogInformation("Started CityWorker Service Successfully!");
    }
}