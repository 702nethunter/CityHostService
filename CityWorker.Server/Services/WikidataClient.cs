using System.Net.Http.Headers;
using System.Text.Json;

namespace CityWorker.Services;

public sealed class WikidataClient
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _ua;

    public WikidataClient(HttpClient http,IConfiguration cfg)
    {
        _http = http;
        _endpoint = cfg.GetValue<string>("Wikidata:Endpoint")??"https://query.wikipedia.org/sparql";
        _ua = cfg.GetValue<string>("Wikidata:UserAgent")??"CityWorker/1.0 (nethunter2023@gmail.com)";

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

    }
    public async Task<IReadOnlyList<Row>> GetTopUsCitiesAsync(int topN,CancellationToken ct)
    {
        var sparql = $@"
            SELECT ?place ?placeLabel ?state ?stateLabel ?stateCode ?population ?lat ?lon ?enTitle WHERE {{
            ?place wdt:P17 wd:Q30 .
            ?place wdt:P31/wdt:P279* ?class .
            VALUES ?class {{ wd:Q1093829 wd:Q515 wd:Q3957 wd:Q15284 }}
            ?place wdt:P1082 ?population .
            OPTIONAL {{ ?place wdt:P625 ?coord .
                        BIND(geof:latitude(?coord)  AS ?lat)
                        BIND(geof:longitude(?coord) AS ?lon) }}
            OPTIONAL {{
                ?place wdt:P131 ?state .
                ?state wdt:P31 wd:Q35657 .
                OPTIONAL {{ ?state wdt:P300 ?iso2 . BIND(STRAFTER(?iso2, ""US-"") AS ?stateCode) }}
            }}
            OPTIONAL {{
                ?enwikiArticle schema:about ?place ;
                            schema:isPartOf <https://en.wikipedia.org/> ;
                            schema:name ?enTitle .
            }}
            SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
            }}
            ORDER BY DESC(?population)
            LIMIT {topN}
            ";
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(sparql)
        };
        req.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/sparql-query");

        for(int attempt=1 ; attempt<=4;attempt++)
        {
            using var res = await _http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);
            if((int)res.StatusCode is 429 or 503)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200*Math.Pow(2,attempt)+Random.Shared.Next(100,400)),ct);
                continue;
            }
            res.EnsureSuccessStatusCode();
            using var s = await res.Content.ReadAsStreamAsync(ct);
            return Parse(s);
        }
        throw new HttpRequestException("Wikidata throttled repeatedly.");
    }
    private static List<Row> Parse(Stream jsonStream)
    {
        using var doc = JsonDocument.Parse(jsonStream);
        var bindings = doc.RootElement.GetProperty("results").GetProperty("bindings");
        var rows = new List<Row>(bindings.GetArrayLength());

        foreach (var b in bindings.EnumerateArray())
        {
            string S(string n) => b.TryGetProperty(n, out var x) && x.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
            double? D(string n) => (b.TryGetProperty(n, out var x) && x.TryGetProperty("value", out var v) && double.TryParse(v.GetString(), out var d)) ? d : null;
            long L(string n) => (b.TryGetProperty(n, out var x) && x.TryGetProperty("value", out var v) && long.TryParse(v.GetString(), out var d)) ? d : 0;

            rows.Add(new Row(
                City: S("placeLabel"),
                State: S("stateLabel"),
                StateCode: S("stateCode"),
                Population: L("population"),
                Lat: D("lat"),
                Lon: D("lon"),
                WikipediaTitle: string.IsNullOrWhiteSpace(S("enTitle")) ? S("placeLabel") : S("enTitle")
            ));
        }
        return rows;
    }
    public sealed record Row(string City,string State,string StateCode,long Population,double? Lat,double? Lon,string WikipediaTitle);
}