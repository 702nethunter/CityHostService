using System.Collections.Concurrent;
using CityWorker.Models;
using CityWorker.Services;
public class HostServerData
{
    private ConcurrentDictionary<long,HostDataEntry> _hostDict = new ConcurrentDictionary<long,HostDataEntry>();
    private ConcurrentDictionary<long,HostCityMap> _cityMapping = new ConcurrentDictionary<long,HostCityMap>();
    private readonly ILogger<HostServerData> _logger;
    private readonly ICityCache _cityCache;
    private readonly object _assignmentLock = new object();
    private int _currentCityIndex = 0;

    public HostServerData(ILogger<HostServerData> logger,ICityCache cityCache)
    {
        _logger=logger;
        _cityCache = cityCache;
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
    private async Task<HostCityMap> MapCity(long hostId)
    {
       if (_cityMapping.TryGetValue(hostId, out var existingMapping))
        {
            _logger?.LogInformation("Host {HostId} already mapped to city: {City}", hostId, existingMapping.CityName);
            return existingMapping;
        }
        try
        {
            var cities =  _cityCache.All();
            if(cities ==null ||!cities.Any())
            {
                _logger.LogError("Not cities available in cache for mapping");
                return null;
            }
            CityEntry assingedCity;
            lock (_assignmentLock)
            {
                assingedCity = cities[_currentCityIndex%cities.Count];
                Interlocked.Increment(ref _currentCityIndex);

                if(_currentCityIndex>=cities.Count)
                {
                    _currentCityIndex=0;
                }
            }
            HostCityMap hostCityMap =  await CreateHostCityMapAsync(hostId,assingedCity);
            // Add to mapping dictionary
            if (_cityMapping.TryAdd(hostId, hostCityMap))
            {
                _logger.LogInformation("Mapped host {HostId} to city: {City} ", 
                    hostId, hostCityMap.CityName);
                return hostCityMap;
            }
            else
            {
                return null;
            }

        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error mapping city for host: {HostId}", hostId);
            return null;
        }

    }
    private async Task<HostCityMap> CreateHostCityMapAsync(long hostId,CityEntry assignedCity)
    {
        HostCityMap hostCityMap = new HostCityMap(
            HostId: hostId,
            CityId: await GenerateCityId(assignedCity),
            CityName: assignedCity.City,
            AssignedAt: DateTime.UtcNow
        );
        return hostCityMap;
    }
    private async Task<long> GenerateCityId(CityEntry city)
    {
        var cityKey =$"{city.City}_{city.State}_{city.Population}";
        return (long)city.GetHashCode();
    }
    public async Task<HostCityMap> GetCityMapping(long hostId)
    {
        if(_cityMapping.TryGetValue(hostId,out var mapping))
        {
            return mapping;
        }
        return await MapCity(hostId);
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