using CityWorker.Server.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Microsoft.AspNetCore.Builder;  
using Microsoft.AspNetCore.Hosting;
using CityWorker.Models;
using CityWorker.Services;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.log", rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// HTTP/2 for gRPC (dev: cleartext; prod: use HTTPS)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5003, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2; // Force HTTP/2
        listenOptions.UseHttps(); // Use development certificate
    });
    
    // Optional: Also listen on HTTP/1.1 for REST endpoints
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

// Services
builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection("Bootstrap"));
builder.Services.AddSingleton<ICityCache, CityCache>(); // Fixed: Services not Sevices
builder.Services.AddHttpClient<WikidataClient>();
builder.Services.AddGrpc();
builder.Services.AddSingleton<HostServerData>();
builder.Services.AddHostedService<CityBootstrapper>(); // Fixed: AddHostedService not AddHostedSevice

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGrpcService<CityDirectoryService>();

app.MapGet("/", () => "CityWorker gRPC is running. Call city.worker.v1.CityDirectory/RegisterHost");

await app.RunAsync();