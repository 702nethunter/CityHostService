using System.Net;
using System.Net.Sockets;
using System.Threading;
using CityWorker.Client.Services;
namespace CityWorker.Client;

public class ClientWorker : BackgroundService
{
    private readonly ILogger<ClientWorker> _logger;
    private readonly GrpcClientService _grpcClient;
    private SnowflakeIdGenerator _idGenerator;
    private long _cachedHostId=0;
    private long _clientSequenceNumber=0;
    private long _sessionId=0;
    private SettingsManager _settingsManager;

    public ClientWorker(ILogger<ClientWorker> logger,GrpcClientService grpcClient,SettingsManager settingsManager)
    {
        _logger = logger;
        _grpcClient = grpcClient;
        _idGenerator = new SnowflakeIdGenerator(1);
        _settingsManager = settingsManager;
    }
    private async Task<long> IntializeHostId()
    {
        try
        {
            Settings settings = await _settingsManager.ReadSettingsAsync();

            if(settings.HostData.HostID==0)
            {
                settings.HostData.HostID = _idGenerator.NextId();
                _logger.LogInformation("Generated new HostID: {HostID}", settings.HostData.HostID);
                // Update settings.json
                await _settingsManager.WriteSettingsAsync(settings);
            }
            else
            {
                _logger.LogInformation("Reusing existing HostID: {HostID}", settings.HostData.HostID);
            }
            return settings.HostData.HostID;
        }
        catch(System.Exception ex)
        {
            _logger.LogError("Error in reading Settings file");
            return -1;
        }
    }
    private long GetSessionId()
    {
        if(_sessionId==0)
        {
            _sessionId = _idGenerator.NextId();
        }
        return _sessionId;
    }
    private async Task SendRegisterHost()
    {
        string hostName = Environment.MachineName;
        string clientIP = this.GetLocalIpAddress();
        string hostVersion = "1.0.0";
        long requestId = _idGenerator.NextId();
        var response = await _grpcClient.RegisterHostAsync(_cachedHostId,hostName,clientIP,hostVersion,this.GetSessionId(),
        requestId,Interlocked.Increment(ref _clientSequenceNumber));
        _logger.LogInformation($"Server Assigned City {response.CityName} to hostId:{_cachedHostId}");
        
    }
    public  string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        
        // Loop through all addresses and find the first IPv4 address that is not loopback and not internal link-local
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && // IPv4
                !IPAddress.IsLoopback(ip) &&
                ip.ToString().StartsWith("169.254.") == false) // Skip APIPA addresses
            {
                return ip.ToString();
            }
        }
        
        // Fallback: try getting any IPv4 address, even if it's loopback
        var fallbackIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        return fallbackIp ?? "127.0.0.1"; // Ultimate fallback to localhost
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._cachedHostId = await this.IntializeHostId();
         
        //Now send the registerHost command
        await this.SendRegisterHost();
         /*
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
        */
    }
}
