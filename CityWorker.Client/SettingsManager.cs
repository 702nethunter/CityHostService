using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
public class Settings
{   
    public HostDetails HostData{get;set;}=new HostDetails();

}
public class GrpcSettings
{
    public string ServerUrl { get; set; } = string.Empty;
}
public class HostDetails
{
    public long HostID{get;set;}
}
public class SettingsManager
{
    private readonly string _settingsPath;
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1,1);
    private ILogger<SettingsManager> _logger;
    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory,"settings.json");
        _logger = logger;
    }
   
    public async Task<Settings> ReadSettingsAsync()
    {
        await _fileLock.WaitAsync();
        try{
            if(!File.Exists(_settingsPath))
            {
                _logger.LogError("settings.json not found at {Path}",_settingsPath);
                throw new FileNotFoundException("settings.json file not found");
            }
            string json = await File.ReadAllTextAsync(_settingsPath);
            return JsonSerializer.Deserialize<Settings>(json)?? new Settings();
        }
        finally
        {
            _fileLock.Release();
        }
    }
    public async Task WriteSettingsAsync(Settings settings)
    {
        await _fileLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        finally
        {
            _fileLock.Release(); // Release lock
        }
    }
}