namespace CityWorker.Models;

public sealed record CityEntry(
    long CityId,
    long CityHashCode,
    string City,
    string State,
    string StateCode,
    long Population,
    double? Lat,
    double? Lon,
    string WikipediaTitle
);
