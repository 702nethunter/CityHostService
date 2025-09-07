namespace CityWorker.Models;

public sealed record HostCityMap(
    long HostId,
    long CityId,
    string CityName,
    DateTime AssignedAt
);