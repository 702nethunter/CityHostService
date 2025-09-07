namespace CityWorker.Models;

public sealed record CityEntry(
    string City,
    string State,
    string StateCode,
    long Population,
    double? Lat,
    double? Lon,
    string WikipediaTitle
);
