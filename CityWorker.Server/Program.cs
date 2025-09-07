using CityWorker.Server.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Microsoft.AspNetCore.Builder;  
using Microsoft.AspNetCore.Hosting;



Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.log", rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// HTTP/2 for gRPC (dev: cleartext; prod: use HTTPS)
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(5001, o => o.Protocols = HttpProtocols.Http2);
    // For TLS in prod:
    // k.ListenAnyIP(5001, o => { o.UseHttps("cert.pfx","password"); o.Protocols = HttpProtocols.Http2; });
});

// Services
builder.Services.AddGrpc();


var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGrpcService<CityDirectoryService>();


app.MapGet("/", () => "CityWorker gRPC is running. Call city.worker.v1.CityDirectory/RegisterHost");

await app.RunAsync();
