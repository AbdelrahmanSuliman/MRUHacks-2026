// RouteCenterline.cs — task #2 (route-to-world) + the hook for task #3 (lane clamp).
//
// Search this file for  >>> INPUT  to find every place you need to fill in data by hand.
//
// Works in Unity (UnityEngine.Vector3) and in a plain .NET console/test project
// (System.Numerics.Vector3) — the V shim below hides the differences.
// Coordinate convention: x = east, y = up, z = north, meters, origin = first route point.

using System;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Numerics;
#endif

// ---------------------------------------------------------------------------
// MOCK ROUTE DATA
// ---------------------------------------------------------------------------
public static class MockRoute
{
    // >>> INPUT 1: route coordinates.
    // In Google Maps, right-click 8-10 points along a real bike path (in riding order)
    // and click the coordinates to copy them, e.g. "51.01234, -114.12345".
    // Paste each as (lat, lng). Replace ALL the placeholder (0, 0) rows.
    // Later: swap this list for OrsRoute.FromGeoJson(<OpenRouteService response>).Points.
    public static readonly List<(double lat, double lng)> Points = new List<(double lat, double lng)>
    {
        (51.052863795503875, -114.07829761505127),   // point 1 (start / origin)
        (51.05324146801681, -114.07824933528902),   // point 2
        (51.05339286465942, -114.07826629909417),   // point 3
        (51.053456000000004, -114.078254),   // point 4
        (51.05354526086976, -114.07837982275598),   // point 5
        (51.05374277451704, -114.07866381004733),   // point 6
        (51.05422264607814, -114.07935454842777),   // point 7
        (51.0543453601662, -114.0795299684634),   // point 8
        // (0.0, 0.0), // point 9  (optional)
        // (0.0, 0.0), // point 10 (optional)
    };

    // >>> INPUT 2: reference distance for the SAME route, in meters (measured in Google Maps).
    // Used by the sanity check; should match within ~1%. For an OpenRouteService route the
    // check uses the response's own summary.distance instead.
    public const double ReferenceDistanceMeters = 201.0;

    // >>> INPUT 3: known side-offset test (sanity check for Project()).
    // Pick a distance along the route and an offset to the side; positive = rider's right.
    // The check builds a point there and confirms Project() returns the same numbers.
    public const float TestDistanceAlongMeters = 50f;
    public const float TestLateralOffsetMeters = 1.5f;
}

// ---------------------------------------------------------------------------
// LANE WIDTH (task #3)
// ---------------------------------------------------------------------------
public static class LaneWidthConfig
{
    // >>> INPUT 4: default HALF-width in meters for untagged paths.
    // Context doc suggests a 2.2 m lane → 1.1 m half-width.
    public const float DefaultHalfWidth = 1.1f;

    // >>> INPUT 5: OSM width overrides (from the Overpass query: cycleway / path "width" tags).
    // Each entry: distance range along the route (meters, from the resampled centerline)
    // and the FULL width from OSM. Leave empty until the Overpass result is in.
    public static readonly List<(float fromS, float toS, float fullWidth)> OsmWidthOverrides =
        new List<(float fromS, float toS, float fullWidth)>
    {
        // (0f, 120f, 3.0f),   // example: first 120 m tagged width=3
    };
}

// ---------------------------------------------------------------------------
// RIDE SETTINGS
// ---------------------------------------------------------------------------
public static class RideConfig
{
    // >>> INPUT 6: tuning values (defaults are reasonable, change if needed).
    public const float SampleSpacingMeters = 1.1f;   // resample step
    public const int TangentSmoothingWindow = 5;      // moving average over 3-5 samples (odd number)
    public const float BikeSpeedMetersPerSecond = 4.0f; // 14.4 km/h; distance = speed * time
}

// ---------------------------------------------------------------------------
// Vector3 shim so the same code compiles in Unity and plain .NET
// ---------------------------------------------------------------------------
public static class V
{
#if UNITY_5_3_OR_NEWER
    public static Vector3 Up => Vector3.up;
    public static Vector3 Norm(Vector3 v) => v.normalized;
    public static float Len(Vector3 v) => v.magnitude;
    public static float X(Vector3 v) => v.x;
    public static float Z(Vector3 v) => v.z;
#else
    public static Vector3 Up => Vector3.UnitY;
    public static Vector3 Norm(Vector3 v) { float l = v.Length(); return l > 1e-6f ? v / l : Vector3.Zero; }
    public static float Len(Vector3 v) => v.Length();
    public static float X(Vector3 v) => v.X;
    public static float Z(Vector3 v) => v.Z;
#endif
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
}

public struct Pose
{
    public Vector3 Position;
    public Vector3 Forward;
    public Pose(Vector3 position, Vector3 forward) { Position = position; Forward = forward; }
}

// ---------------------------------------------------------------------------
// CENTERLINE
// ---------------------------------------------------------------------------
public class RouteCenterline
{
    const double R = 6371000.0;

    public readonly double Lat0, Lng0;     // projection origin (first route point)
    public readonly Vector3[] Points;      // evenly spaced samples, local meters
    public readonly float[] S;             // cumulative distance at each sample
    public readonly Vector3[] Forward;     // smoothed unit tangent
    public readonly Vector3[] Right;       // Cross(up, forward)
    public readonly float[] HalfWidth;     // task #3: per-sample lane half-width

    public float TotalLength => S[S.Length - 1];

    public RouteCenterline(IList<(double lat, double lng)> route,
                           float spacing = RideConfig.SampleSpacingMeters,
                           int smoothWindow = RideConfig.TangentSmoothingWindow)
    {
        if (route == null || route.Count < 2)
            throw new ArgumentException("Route needs at least 2 points.");
        foreach (var p in route)
            if (p.lat == 0.0 && p.lng == 0.0)
                throw new ArgumentException("Route still contains placeholder (0, 0) points — fill in MockRoute.Points.");

        Lat0 = route[0].lat;
        Lng0 = route[0].lng;

        // 1. Project to local meters.
        var local = new List<Vector3>(route.Count);
        foreach (var p in route) local.Add(ToLocal(p.lat, p.lng, Lat0, Lng0));

        // 2. Resample at even spacing, carrying leftover distance across segment boundaries.
        var pts = Resample(local, spacing);
        int n = pts.Count;
        Points = pts.ToArray();

        // 3. Per-sample data.
        S = new float[n];
        for (int i = 1; i < n; i++) S[i] = S[i - 1] + V.Len(Points[i] - Points[i - 1]);

        var raw = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int a = Math.Min(i, n - 2);
            raw[i] = V.Norm(Points[a + 1] - Points[a]);
        }

        Forward = new Vector3[n];
        Right = new Vector3[n];
        int half = Math.Max(0, smoothWindow / 2);
        for (int i = 0; i < n; i++)
        {
            Vector3 sum = raw[i] * 0f;
            for (int k = Math.Max(0, i - half); k <= Math.Min(n - 1, i + half); k++) sum += raw[k];
            Forward[i] = V.Norm(sum);
            Right[i] = V.Norm(Vector3.Cross(V.Up, Forward[i]));
        }

        // 4. Lane half-widths (task #3).
        HalfWidth = new float[n];
        for (int i = 0; i < n; i++)
        {
            HalfWidth[i] = LaneWidthConfig.DefaultHalfWidth;
            foreach (var o in LaneWidthConfig.OsmWidthOverrides)
                if (S[i] >= o.fromS && S[i] <= o.toS) HalfWidth[i] = o.fullWidth * 0.5f;
        }
    }

    public static Vector3 ToLocal(double lat, double lng, double lat0, double lng0)
    {
        double x = (lng - lng0) * Math.PI / 180 * R * Math.Cos(lat0 * Math.PI / 180);
        double z = (lat - lat0) * Math.PI / 180 * R;
        return new Vector3((float)x, 0, (float)z); // x = east, z = north
    }

    static List<Vector3> Resample(List<Vector3> pts, float spacing)
    {
        var outPts = new List<Vector3> { pts[0] };
        float carry = 0f; // distance already travelled since the last emitted sample
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            float segLen = V.Len(b - a);
            if (segLen < 1e-6f) continue;
            float d = spacing - carry; // distance into this segment of the next sample
            while (d <= segLen)
            {
                outPts.Add(Vector3.Lerp(a, b, d / segLen));
                d += spacing;
            }
            carry = segLen - (d - spacing);
        }
        // Keep the true endpoint so the total length is exact.
        if (carry > 1e-3f) outPts.Add(pts[pts.Count - 1]);
        return outPts;
    }

    // Position + forward on the centerline at distance s. Drives the automatic bike movement.
    public Pose GetPoseAtDistance(float s)
    {
        s = V.Clamp(s, 0f, TotalLength);
        int i = FindSegment(s);
        float segLen = S[i + 1] - S[i];
        float t = segLen > 1e-6f ? (s - S[i]) / segLen : 0f;
        return new Pose(Vector3.Lerp(Points[i], Points[i + 1], t),
                        V.Norm(Vector3.Lerp(Forward[i], Forward[i + 1], t)));
    }

    // Nearest centerline sample → distance along path and signed sideways offset (+ = right).
    // Pass the previous frame's index as hint to search a small window instead of every sample.
    public (float s, float lateral) Project(Vector3 pos, int hint = -1, int window = 30)
    {
        int i = NearestIndex(pos, hint, window);
        Vector3 d = pos - Points[i];
        float s = V.Clamp(S[i] + Vector3.Dot(d, Forward[i]), 0f, TotalLength);
        return (s, Vector3.Dot(d, Right[i]));
    }

    public int NearestIndex(Vector3 pos, int hint = -1, int window = 30)
    {
        int lo = 0, hi = Points.Length - 1;
        if (hint >= 0) { lo = Math.Max(0, hint - window); hi = Math.Min(Points.Length - 1, hint + window); }
        int best = lo;
        float bestD = float.MaxValue;
        for (int i = lo; i <= hi; i++)
        {
            float dd = V.Len(pos - Points[i]);
            if (dd < bestD) { bestD = dd; best = i; }
        }
        return best;
    }

    // Task #3: clamp a position to the lane and rebuild it from the centerline.
    public Vector3 ClampToLane(Vector3 pos, int hint = -1)
    {
        var (s, lateral) = Project(pos, hint);
        int i = NearestIndex(pos, hint);
        lateral = V.Clamp(lateral, -HalfWidth[i], HalfWidth[i]);
        Pose p = GetPoseAtDistance(s);
        Vector3 right = V.Norm(Vector3.Cross(V.Up, p.Forward));
        return p.Position + right * lateral;
    }

    int FindSegment(float s)
    {
        int lo = 0, hi = S.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (S[mid] <= s) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
}

// ---------------------------------------------------------------------------
// OpenRouteService route loader — drop-in for MockRoute.Points.
// Request (cycling profile, GeoJSON output):
//   POST https://api.openrouteservice.org/v2/directions/cycling-regular/geojson
//   Header: Authorization: <ORS API key>
//   Body:   {"coordinates":[[lngA,latA],[lngB,latB]]}      <-- ORS order is [lng, lat]!
// Save the response to a .json file and pass its text to FromGeoJson().
// No JSON library needed, so it works the same in Unity and plain .NET.
// ---------------------------------------------------------------------------
public static class OrsRoute
{
    public static (List<(double lat, double lng)> Points, double DistanceMeters) FromGeoJson(string json)
    {
        int geom = json.IndexOf("\"geometry\"", StringComparison.Ordinal);
        int key = geom < 0 ? -1 : json.IndexOf("\"coordinates\"", geom, StringComparison.Ordinal);
        if (key < 0) throw new FormatException("No features[0].geometry.coordinates in ORS response (did you call the /geojson endpoint?).");

        // Walk the [[lng,lat(,elev)], ...] array.
        var points = new List<(double lat, double lng)>();
        int i = json.IndexOf('[', key) + 1;
        while (true)
        {
            while (i < json.Length && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
            if (json[i] == ']') break;
            int close = json.IndexOf(']', i);
            string[] parts = json.Substring(i + 1, close - i - 1).Split(',');
            double lng = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            double lat = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            points.Add((lat, lng));
            i = close + 1;
        }

        double distance = 0;
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"summary\"\\s*:\\s*\\{[^}]*\"distance\"\\s*:\\s*([0-9.eE+-]+)");
        if (m.Success) distance = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return (points, distance);
    }
}

// ---------------------------------------------------------------------------
// OSM lane widths (task #3) — turns an Overpass response into OsmWidthOverrides.
// 1. BuildQuery(route) → Overpass QL for every highway way in a box around the route.
// 2. POST it to https://overpass-api.de/api/interpreter (form field "data"), save the JSON.
// 3. ParseOverpass(json) → ways with their width tags, then MatchToRoute() → (fromS, toS, fullWidth)
//    ranges. Each route sample takes the nearest roughly-parallel way within maxDist, so
//    crossing streets at intersections don't leak their (much larger) road widths in.
// ---------------------------------------------------------------------------
public static class OsmWidths
{
    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    public class Way
    {
        public long Id;
        public string Highway, Name, WidthTag;
        public float? Width;            // full width in meters, null if untagged/unparseable
        public List<Vector3> Local = new List<Vector3>();
    }

    public static string BuildQuery(IList<(double lat, double lng)> route, double padDeg = 0.0003) // ~30 m
    {
        double s = double.MaxValue, w = double.MaxValue, n = double.MinValue, e = double.MinValue;
        foreach (var p in route)
        {
            s = Math.Min(s, p.lat); n = Math.Max(n, p.lat);
            w = Math.Min(w, p.lng); e = Math.Max(e, p.lng);
        }
        return string.Format(Inv, "[out:json][timeout:25];way[\"highway\"]({0:F6},{1:F6},{2:F6},{3:F6});out tags geom;",
                             s - padDeg, w - padDeg, n + padDeg, e + padDeg);
    }

    public static List<Way> ParseOverpass(string json, double lat0, double lng0)
    {
        var RX = System.Text.RegularExpressions.RegexOptions.None;
        var ways = new List<Way>();
        string[] chunks = System.Text.RegularExpressions.Regex.Split(json, "\"type\"\\s*:\\s*\"way\"", RX);
        for (int k = 1; k < chunks.Length; k++)
        {
            string c = chunks[k];
            var way = new Way
            {
                Id = long.Parse(Tag(c, "id", "(\\d+)") ?? "0"),
                Highway = Tag(c, "highway"),
                Name = Tag(c, "name"),
                WidthTag = Tag(c, "width") ?? Tag(c, "cycleway:width") ?? Tag(c, "est_width"),
            };
            way.Width = ParseWidth(way.WidthTag);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         c, "\"lat\"\\s*:\\s*(-?[0-9.]+)\\s*,\\s*\"lon\"\\s*:\\s*(-?[0-9.]+)"))
                way.Local.Add(RouteCenterline.ToLocal(double.Parse(m.Groups[1].Value, Inv),
                                                      double.Parse(m.Groups[2].Value, Inv), lat0, lng0));
            if (way.Local.Count >= 2) ways.Add(way);
        }
        return ways;
    }

    static string Tag(string chunk, string key, string valuePattern = "\"([^\"]*)\"")
    {
        var m = System.Text.RegularExpressions.Regex.Match(chunk, "\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\"\\s*:\\s*" + valuePattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    // OSM width values: "3", "2.5 m", "2,5", "8'" / "8 ft" (feet).
    public static float? ParseWidth(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        string t = tag.Trim().ToLowerInvariant().Replace(',', '.');
        bool feet = t.EndsWith("'") || t.EndsWith("ft");
        t = t.TrimEnd('\'', 'm', 'f', 't', ' ');
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, Inv, out double v) || v <= 0) return null;
        return (float)(feet ? v * 0.3048 : v);
    }

    public static List<(float fromS, float toS, float fullWidth)> MatchToRoute(
        RouteCenterline c, List<Way> ways, float maxDist = 6f, Action<string> log = null)
    {
        int n = c.Points.Length;
        var match = new Way[n];
        var metersOn = new Dictionary<Way, float>();
        for (int i = 0; i < n; i++)
        {
            float best = maxDist;
            foreach (var w in ways)
                for (int j = 0; j < w.Local.Count - 1; j++)
                {
                    Vector3 a = w.Local[j], ab = w.Local[j + 1] - a;
                    float len2 = Vector3.Dot(ab, ab);
                    if (len2 < 1e-6f) continue;
                    if (Math.Abs(Vector3.Dot(V.Norm(ab), c.Forward[i])) < 0.7f) continue; // not parallel (> ~45°)
                    float t = V.Clamp(Vector3.Dot(c.Points[i] - a, ab) / len2, 0f, 1f);
                    float d = V.Len(c.Points[i] - (a + ab * t));
                    if (d < best) { best = d; match[i] = w; }
                }
            if (match[i] != null)
            {
                float step = i < n - 1 ? c.S[i + 1] - c.S[i] : 0f;
                metersOn[match[i]] = (metersOn.TryGetValue(match[i], out float m) ? m : 0f) + step;
            }
        }

        if (log != null)
        {
            log($"Overpass ways in box: {ways.Count}, matched to route: {metersOn.Count}");
            foreach (var kv in metersOn)
                log($"  way {kv.Key.Id} [{kv.Key.Highway}] \"{kv.Key.Name ?? "-"}\": {kv.Value:F0} m of route, " +
                    $"width tag: {kv.Key.WidthTag ?? "none"}{(kv.Key.Width.HasValue ? $" → {kv.Key.Width.Value.ToString("F2", Inv)} m" : "")}");
            int unmatched = 0;
            foreach (var w in match) if (w == null) unmatched++;
            if (unmatched > 0) log($"  {unmatched} samples matched no way (default width applies)");
        }

        // Merge consecutive samples with the same tagged width into ranges.
        var ranges = new List<(float fromS, float toS, float fullWidth)>();
        for (int i = 0; i < n; i++)
        {
            float? wdt = match[i]?.Width;
            if (!wdt.HasValue) continue;
            int j = i;
            while (j + 1 < n && match[j + 1]?.Width == wdt) j++;
            ranges.Add((c.S[i], c.S[j], wdt.Value));
            i = j;
        }
        return ranges;
    }

    // Paste-ready lines for LaneWidthConfig.OsmWidthOverrides.
    public static string ToCSharp(List<(float fromS, float toS, float fullWidth)> ranges)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in ranges)
            sb.AppendLine(string.Format(Inv, "        ({0:F1}f, {1:F1}f, {2:F2}f),", r.fromS, r.toS, r.fullWidth));
        return sb.ToString();
    }
}

// ---------------------------------------------------------------------------
// Encoded polyline decoder — only needed if you call the ORS /json endpoint
// (routes[0].geometry is an encoded string) instead of /geojson. ORS uses the
// same algorithm at 1e5 precision; with elevation=true each point has a third
// value (meters * 100), so pass hasElevation: true.
// ---------------------------------------------------------------------------
public static class PolylineDecoder
{
    public static List<(double lat, double lng)> Decode(string encoded, bool hasElevation = false)
    {
        var result = new List<(double lat, double lng)>();
        int index = 0, lat = 0, lng = 0;
        while (index < encoded.Length)
        {
            lat += Next(encoded, ref index);
            lng += Next(encoded, ref index);
            if (hasElevation) Next(encoded, ref index);
            result.Add((lat / 1e5, lng / 1e5));
        }
        return result;
    }

    static int Next(string s, ref int index)
    {
        int shift = 0, value = 0, b;
        do
        {
            b = s[index++] - 63;
            value |= (b & 0x1f) << shift;
            shift += 5;
        } while (b >= 0x20);
        return (value & 1) != 0 ? ~(value >> 1) : (value >> 1);
    }
}

// ---------------------------------------------------------------------------
// SANITY CHECK — call RouteSanityCheck.Run() from a console app, a test, or a Unity Start().
// ---------------------------------------------------------------------------
public static class RouteSanityCheck
{
    // Mock route: Run().  ORS route: Run(OrsRoute.FromGeoJson(json)).
    public static void Run(Action<string> log = null) =>
        Run((MockRoute.Points, MockRoute.ReferenceDistanceMeters), log);

    public static void Run((List<(double lat, double lng)> Points, double DistanceMeters) route, Action<string> log = null)
    {
        log = log ?? Console.WriteLine;
        var c = new RouteCenterline(route.Points);
        double reference = route.DistanceMeters;

        log($"Input points: {route.Points.Count}, samples: {c.Points.Length}, resampled length: {c.TotalLength:F1} m");
        if (reference > 0)
        {
            double err = Math.Abs(c.TotalLength - reference) / reference * 100;
            log($"Reference: {reference:F1} m, diff {err:F2}% {(err <= 1.0 ? "OK" : "CHECK (>1%)")}");
        }
        else log("No reference distance — skipping length comparison.");

        // Known side offset: build a point and see if Project() recovers it.
        Pose p = c.GetPoseAtDistance(MockRoute.TestDistanceAlongMeters);
        Vector3 right = V.Norm(Vector3.Cross(V.Up, p.Forward));
        Vector3 test = p.Position + right * MockRoute.TestLateralOffsetMeters;
        var (s, lateral) = c.Project(test);
        log($"Offset test: expected s={MockRoute.TestDistanceAlongMeters:F2}, lateral={MockRoute.TestLateralOffsetMeters:F2} " +
            $"→ got s={s:F2}, lateral={lateral:F2}");
        log($"Forward at test point (x=east, z=north): ({V.X(p.Forward):F2}, {V.Z(p.Forward):F2}); right = ({V.X(right):F2}, {V.Z(right):F2})");

        Vector3 clamped = c.ClampToLane(p.Position + right * 10f);
        log($"Clamp test: 10 m right → {c.Project(clamped).lateral:F2} m (half-width {c.HalfWidth[c.NearestIndex(clamped)]:F2})");
    }
}
