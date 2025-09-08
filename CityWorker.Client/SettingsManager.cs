using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class Settings
{
    // Match the requested JSON shape: { "HostDetails": { "HostID": 0 } }
    public HostDetails HostData { get; set; } = new HostDetails();
}

public class GrpcSettings
{
    public string ServerUrl { get; set; } = string.Empty;
}

public class HostDetails
{
    public long HostID { get; set; }
}

public class SettingsManager
{
    private readonly string _settingsPath;
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
    private readonly ILogger<SettingsManager> _logger;
    private readonly int _instanceNumber;

    public SettingsManager(ILogger<SettingsManager> logger, int instanceNumber)
    {
        _logger = logger;
        _instanceNumber = instanceNumber <= 0 ? 1 : instanceNumber;

        // settings_{instance}.json in the app directory
        _settingsPath = Path.Combine(AppContext.BaseDirectory, $"settings_{_instanceNumber}.json");

        // ensure directory exists (AppContext.BaseDirectory is usually fine, but safe to guard)
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    public async Task<Settings> ReadSettingsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("Settings file not found for instance {Instance}. Creating default at {Path}.",
                    _instanceNumber, _settingsPath);

                var defaults = DefaultSettings();
                await WriteInternalAsync(defaults).ConfigureAwait(false);
                return defaults;
            }

            string json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<Settings>(json);

            if (settings is null)
            {
                _logger.LogWarning("Settings file at {Path} could not be deserialized. Recreating with defaults.", _settingsPath);
                var defaults = DefaultSettings();
                await WriteInternalAsync(defaults).ConfigureAwait(false);
                return defaults;
            }

            return settings;
        }
        catch (JsonException jx)
        {
            _logger.LogWarning(jx, "Invalid JSON in settings file {Path}. Recreating with defaults.", _settingsPath);
            var defaults = DefaultSettings();
            await WriteInternalAsync(defaults).ConfigureAwait(false);
            return defaults;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task WriteSettingsAsync(Settings settings)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await WriteInternalAsync(settings).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    // ----- helpers -----

    private static Settings DefaultSettings() => new Settings
    {
        HostData = new HostDetails { HostID = 0 }
    };

    private async Task WriteInternalAsync(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
        _logger.LogInformation("Settings written for instance {Instance} at {Path}.", _instanceNumber, _settingsPath);
    }
}
