using Grpc.Core;
using CityWorker.Proto;
using Google.Protobuf.WellKnownTypes;

namespace CityWorker.Server.Services;

public class CityDirectoryService : CityDirectory.CityDirectoryBase
{
    private readonly ILogger<CityDirectoryService> _logger;
    private readonly HostServerData _serverDataObj;

    public CityDirectoryService(ILogger<CityDirectoryService> logger,HostServerData hostServerObj)
    { 
         _logger = logger;
         _serverDataObj = hostServerObj;
    }

    // NOTE: method name must be PascalCase in C#
    public override async Task<HostResponse> registerHost(HostRequest request, ServerCallContext context)
    {
        _logger.LogInformation("RegisterHost: HostId={HostId}, HostName={HostName}, IP={IP}, Session={SessionId}",
            request.HostId, request.HostName, request.HostIP, request.SessionId);

       var mappedCity = await _serverDataObj.GetCityMapping(request.HostId);
       _logger.LogInformation($"Mapped City for Host ID :{request.HostId} ,City Name:{mappedCity.CityName}, City Id:{mappedCity.CityId}");
        var resp = new HostResponse
        {
            HostId = request.HostId,
            CityId = mappedCity.CityId,
            CityName = mappedCity.CityName,
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            ServerTimestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Status = true
        };

        return resp;
    }
}
