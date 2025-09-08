using CityWorker.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CityWorker.Client.Services;

var builder = Host.CreateApplicationBuilder(args);
// Read configuration from appsettings.json
var grpcSettings = builder.Configuration.GetSection("GrpcSettings").Get<GrpcSettings>();

builder.Services.AddSingleton(grpcSettings);
builder.Services.AddSingleton<SettingsManager>();
builder.Services.AddSingleton<GrpcClientService>(provider=>{
    var logger = provider.GetRequiredService<ILogger<GrpcClientService>>();
    return new GrpcClientService(grpcSettings.ServerUrl,logger);
});


builder.Services.AddHostedService<ClientWorker>();
var host = builder.Build();
host.Run();
