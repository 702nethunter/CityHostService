using System.Collections.Concurrent;
using CityWorker.Models;

public class HostServerData
{
    private ConcurrentDictionary<long,HostDataEntry> _hostDict = new ConcurrentDictionary<long,HostDataEntry>();
    private ConcurrentDictionary<long,CityEntry> _cityMapping = new ConcurrentDictionary<long,CityEntry>();
    private readonly ILogger<HostServerData> _logger;
    public HostServerData(ILogger<HostServerData> logger=null)
    {
        _logger=logger;
    }
    public async Task<bool> AddOrUpdateHost(HostDataEntry hostData)
    {
        try
        {
           var previousValue = _hostDict.AddOrUpdate(
            hostData.HostId,
            key=>
            {
                _logger?.LogInformation("Added new host:{HostId}",hostData.HostId);
                return hostData;
            },
            (key,existingValue)=>
            {
                _logger?.LogInformation("Updated existing host:{HostId}",hostData.HostId);
                return hostData;
            }
           );
           return true;
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Failed to add/update host: {HostId}", hostData.HostId);
            return false;
        }
    }
    public async Task<bool> RemoveHost(long hostId)
    {
        return _hostDict.TryRemove(hostId,out _);
    }
    public async Task<int> GetHostCount()
    {
        return _hostDict.Count;
    }
}