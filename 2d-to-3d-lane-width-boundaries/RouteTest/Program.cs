// Usage:
//   dotnet run                             -> sanity check on MockRoute.Points
//   dotnet run -- route.geojson            -> sanity check on a saved OpenRouteService /geojson response
//   dotnet run -- route.geojson --osm      -> also fetch OSM widths (saved to overpass.json, reused next run)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

Console.WriteLine("=== Mock route ===");
RouteSanityCheck.Run();

if (args.Length > 0)
{
    Console.WriteLine($"\n=== ORS route: {args[0]} ===");
    var route = OrsRoute.FromGeoJson(File.ReadAllText(args[0]));
    RouteSanityCheck.Run(route);

    if (args.Contains("--osm"))
    {
        Console.WriteLine("\n=== OSM widths ===");
        const string cache = "overpass.json";
        if (!File.Exists(cache))
        {
            string query = OsmWidths.BuildQuery(route.Points);
            Console.WriteLine($"Querying Overpass: {query}");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MRUHacks-VR-Bike/0.1");
            var resp = http.PostAsync("https://overpass-api.de/api/interpreter",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query })).Result;
            resp.EnsureSuccessStatusCode();
            File.WriteAllText(cache, resp.Content.ReadAsStringAsync().Result);
            Console.WriteLine($"Saved {cache} (delete it to re-fetch)");
        }

        var c = new RouteCenterline(route.Points);
        var ways = OsmWidths.ParseOverpass(File.ReadAllText(cache), c.Lat0, c.Lng0);
        var ranges = OsmWidths.MatchToRoute(c, ways, log: Console.WriteLine);
        if (ranges.Count == 0)
            Console.WriteLine("\nNo width tags on the route — leave OsmWidthOverrides empty; DefaultHalfWidth applies everywhere.");
        else
            Console.WriteLine("\nPaste into LaneWidthConfig.OsmWidthOverrides:\n" + OsmWidths.ToCSharp(ranges));
    }
}
