using Grpc.Core;
using CityWorker.Proto;
using Google.Protobuf.WellKnownTypes;

namespace CityWorker.Server.Services;

public class CityDirectoryService : CityDirectory.CityDirectoryBase
{
    private readonly ILogger<CityDirectoryService> _logger;

    public CityDirectoryService(ILogger<CityDirectoryService> logger) => _logger = logger;

    // NOTE: method name must be PascalCase in C#
    public override Task<HostResponse> registerHost(HostRequest request, ServerCallContext context)
    {
        _logger.LogInformation("RegisterHost: HostId={HostId}, HostName={HostName}, IP={IP}, Session={SessionId}",
            request.HostId, request.HostName, request.HostIP, request.SessionId);

        // TODO: plug in your city assignment logic here
        var cityId = 1;
        var cityName = "Las Vegas";

        var resp = new HostResponse
        {
            HostId = request.HostId,
            CityId = cityId,
            CityName = cityName,
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            ServerTimestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Status = true
        };

        return Task.FromResult(resp);
    }
}
