using CityWorker.Proto;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace CityWorker.Client.Services;

public class GrpcClientService
{
    private readonly ILogger<GrpcClientService> _logger;
    private readonly GrpcChannel _channel;
    private readonly CityDirectory.CityDirectoryClient _client;

    public GrpcClientService(string serverUrl,ILogger<GrpcClientService> logger)
    {
        _logger = logger;
        if(serverUrl.StartsWith("http://",StringComparison.OrdinalIgnoreCase))
        {
            var sockets = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections=true
            };
            sockets.SslOptions=null;
            sockets.AllowAutoRedirect=false;

            _channel = GrpcChannel.ForAddress(serverUrl,new GrpcChannelOptions{
                HttpHandler = sockets
            });
        }
        else
        {
           _channel = GrpcChannel.ForAddress(serverUrl);
        }
        _client = new CityDirectory.CityDirectoryClient(_channel);
            
        _logger.LogInformation("gRPC client initialized for server: {ServerUrl}", serverUrl);
    }
   private GrpcChannel CreateHttp11Channel(string serverUrl)
    {
        try
        {
            // Ensure we're using HTTP, not HTTPS
            var httpUrl = serverUrl.Replace("https://", "http://");
            
            // Create a custom HTTP handler that forces HTTP/1.1
            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                // Additional settings for reliability
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            // Create a custom SocketsHttpHandler to force HTTP/1.1
            var socketsHandler = new SocketsHttpHandler
            {
                // Explicitly disable HTTP/2 to force HTTP/1.1
                EnableMultipleHttp2Connections = false,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                // Use the inner handler's certificate validation
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                }
            };

            // For .NET 6+, set the switch to prefer HTTP/1.1
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", false);
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", false);

            return GrpcChannel.ForAddress(httpUrl, new GrpcChannelOptions
            {
                HttpHandler = socketsHandler,
                // Force HTTP/1.1 by setting these options
                MaxReceiveMessageSize = 10 * 1024 * 1024, // 10MB
                MaxSendMessageSize = 10 * 1024 * 1024,    // 10MB
                // Disable retries for simpler debugging
                MaxRetryAttempts = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create HTTP/1.1 gRPC channel");
            throw;
        }
    }
    public async Task<HostResponse> RegisterHostAsync(long hostId,string hostName,string hostIP,string hostVersion,long sessionId,long requestId,long clientSeqNum)
    {
         try
            {
                var request = new HostRequest
                {
                    HostId = hostId,
                    HostName = hostName,
                    HostIP = hostIP,
                    HostVersion = hostVersion,
                    SessionId = sessionId,
                    RequestId = requestId,
                    ClientSequenceNumber = clientSeqNum,
                    ClientTimestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                _logger.LogInformation("Sending host registration: {HostName} ({HostIP})", hostName, hostIP);
                
                var response = await _client.registerHostAsync(request);
                
                _logger.LogInformation("Host registration successful. Assigned to city: {CityName}", response.CityName);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering host {HostName}", hostName);
                throw;
            }
    }
}