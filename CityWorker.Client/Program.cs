using CityWorker.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CityWorker.Client.Services;

var builder = Host.CreateApplicationBuilder(args);
// Parse command line arguments
var instanceNumber = GetInstanceNumberFromArgs(args);
// Read configuration from appsettings.json
var grpcSettings = builder.Configuration.GetSection("GrpcSettings").Get<GrpcSettings>();

builder.Services.AddSingleton(grpcSettings);
builder.Services.AddSingleton<SettingsManager>(provider=>{
var logger = provider.GetRequiredService<ILogger<SettingsManager>>();
return new SettingsManager(logger,instanceNumber);
}
);
builder.Services.AddSingleton<GrpcClientService>(provider=>{
    var logger = provider.GetRequiredService<ILogger<GrpcClientService>>();
    return new GrpcClientService(grpcSettings.ServerUrl,logger);
});


builder.Services.AddHostedService<ClientWorker>();
var host = builder.Build();
host.Run();

static int GetInstanceNumberFromArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--instance" || args[i] == "-i")
        {
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int instanceNumber))
            {
                return instanceNumber;
            }
        }
        else if (args[i].StartsWith("--instance="))
        {
            var value = args[i].Substring("--instance=".Length);
            if (int.TryParse(value, out int instanceNumber))
            {
                return instanceNumber;
            }
        }
    }
    
    // Default to 1 if no instance number provided
    return 1;
}
