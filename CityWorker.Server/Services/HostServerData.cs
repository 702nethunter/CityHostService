using System.Collections.Concurrent;
using CityWorker.Models;
using CityWorker.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

public class HostServerData
{
    private readonly ConcurrentDictionary<long, HostDataEntry> _hostDict = new();
    private readonly ConcurrentDictionary<long, HostCityMap> _cityMapping = new();
    private readonly ConcurrentQueue<CityEntry> _unallocatedCities= new();
    private readonly ILogger<HostServerData> _logger;
    private readonly ICityCache _cityCache;
    private readonly object cityMapLock = new();
    private readonly CityBootstrapper _bootStrapper;
    public HostServerData(ILogger<HostServerData> logger, ICityCache cityCache,CityBootstrapper bootStrapper)
    {
        _logger = logger;
        _cityCache = cityCache;
        _bootStrapper =bootStrapper;
        _bootStrapper.CitiesLoaded+=OnCitiesLoaded; 
    }
    private void OnCitiesLoaded(object? sender, List<CityEntry> cities)
    {
         _logger.LogInformation("OnCitiesLoaded event fired",cities.Count);
        // Clear existing queue to avoid duplicates
        while (_unallocatedCities.TryDequeue(out _)) { }
        foreach (var city in cities.OrderByDescending(c => c.Population))
        {
            // Only enqueue cities not already mapped
            if (!_cityMapping.Values.Any(mapping => mapping.CityId == city.CityId))
            {
                _unallocatedCities.Enqueue(city);
            }
        }
        _logger.LogInformation("Populated city queue with {Count} unallocated cities", _unallocatedCities.Count);
    }
    public bool AddOrUpdateHost(HostDataEntry hostData)
    {
        try
        {
            var previousValue = _hostDict.AddOrUpdate(
                hostData.HostId,
                key =>
                {
                    _logger?.LogInformation("Added new host: {HostId}", hostData.HostId);
                    return hostData;
                },
                (key, existingValue) =>
                {
                    _logger?.LogInformation("Updated existing host: {HostId}", hostData.HostId);
                    return hostData;
                }
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add/update host: {HostId}", hostData.HostId);
            return false;
        }
    }

    private async Task<HostCityMap?> MapCity(long hostId)
    {
       if (_cityMapping.TryGetValue(hostId, out var existingMapping))
        {
            _logger?.LogInformation("Host {HostId} already mapped to city: {City}, CityId: {CityId}", 
                hostId, existingMapping.CityName, existingMapping.CityId);
            return existingMapping;
        }
        try
        {
            if (_unallocatedCities.IsEmpty)
            {
                _logger.LogWarning("No unallocated cities available for host ID: {HostId}. Waiting for cache refresh.", hostId);
                return null;
            }

            if (_unallocatedCities.TryDequeue(out var assignedCity))
            {
                var hostCityMap = new HostCityMap(
                    HostId: hostId,
                    CityId: assignedCity.CityId,
                    CityName: assignedCity.City,
                    AssignedAt: DateTime.UtcNow
                );
                if (_cityMapping.TryAdd(hostId, hostCityMap))
                {
                    _logger.LogInformation("Mapped host {HostId} to city: {City}, CityId: {CityId}", 
                        hostId, hostCityMap.CityName, hostCityMap.CityId);
                    return hostCityMap;
                }
                else
                {
                    _logger.LogWarning("Failed to add mapping for host {HostId} to city {City}, CityId: {CityId}. Requeuing city.", 
                        hostId, assignedCity.City, assignedCity.CityId);
                    _unallocatedCities.Enqueue(assignedCity); // Requeue if mapping fails
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("Failed to dequeue city for host ID: {HostId}", hostId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error mapping city for host: {HostId}", hostId);
            return null;
        }
    }

    private HostCityMap CreateHostCityMap(long hostId, CityEntry assignedCity)
    {
        return new HostCityMap(
            HostId: hostId,
            CityId: assignedCity.CityId,
            CityName: assignedCity.City,
            AssignedAt: DateTime.UtcNow
        );
    }

    

    public async Task<HostCityMap?> GetCityMapping(long hostId)
    {
        if (_cityMapping.TryGetValue(hostId, out var mapping))
        {
            return mapping;
        }
        return await MapCity(hostId);
    }

    public bool RemoveHost(long hostId)
    {
        return _hostDict.TryRemove(hostId, out _);
    }

    public int GetHostCount()
    {
        return _hostDict.Count;
    }
}