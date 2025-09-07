using System.Collections.Immutable;
using CityWorker.Models;

namespace CityWorker.Services;

public interface ICityCache 
{
    int Count{get;}
    IReadOnlyList<CityEntry> Top(int n);
    IReadOnlyList<CityEntry> All();
    void ReplaceAll(IEnumerable<CityEntry> items);
}
public sealed class CityCache:ICityCache
{
    private ImmutableArray<CityEntry> _all= ImmutableArray<CityEntry>.Empty;

    public void ReplaceAll(IEnumerable<CityEntry> items)
    => _all = items.OrderByDescending(x=>x.Population).ToImmutableArray();

    public int Count=>_all.Length;
    public IReadOnlyList<CityEntry> Top(int n)=> _all.Take(n).ToList();
    public IReadOnlyList<CityEntry> All()=>_all;
}