namespace CityWorker.Models;

public sealed record HostDataEntry(
    long HostId,
    string HostName,
    string HostIP,
    string HostVersion,
    DateTime HostAddedDate
);
