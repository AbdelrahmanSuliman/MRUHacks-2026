# Unity / Cesium route handoff

Send `route.json` with this document. It is a precomputed cycling route; Unity
loads it without an API key or network request to the routing provider.

## Fields

| Field | Meaning |
| --- | --- |
| `origin`, `destination` | Requested WGS84 `longitude` and `latitude` in degrees, plus an optional name. Geometry endpoints may snap to nearby accessible paths. |
| `distanceMeters` | Total cycling route length in metres. |
| `durationSeconds` | Provider's estimated travel time, not the player's elapsed time. |
| `encodedPolyline` | Provider's two-dimensional encoded polyline (precision 5). Unity can use the decoded array instead. |
| `coordinatesLonLat` | Ordered route vertices as `[longitude, latitude]` arrays, from start to finish. No height is supplied. |
| `coordinatesLatLng` | Legacy latitude-first array. Prefer `coordinatesLonLat`. |
| `steps` | Ordered navigation instructions. |
| `source`, `travelMode` | Attribution/source label and bicycle travel mode. Preserve applicable provider/OSM attribution in the demo. |

Each step contains `instruction`, road/path `name`, `maneuverType` (the provider's
numeric instruction type), `distanceMeters`, `durationSeconds`, and `wayPoints`.
`wayPoints` are zero-based start/end vertex indices in the geometry; adjoining
steps can share an endpoint. Use these to associate instructions with progress
along the route. Step duration/distance describes that step, not remaining distance.
Show instruction text directly; arrival steps can have zero distance.

## Load and draw

1. Put the JSON in a Unity asset location such as `Resources/route.json` and load
   it as a `TextAsset`. Deserialize with Unity's Newtonsoft JSON package if available.
   `JsonUtility` does not support the nested arrays in `coordinatesLonLat` directly.
   A C# model should use `double[][] coordinatesLonLat`, endpoint objects with
   `double longitude/latitude`, and a step array with matching field names.
2. For each `[longitude, latitude]`, obtain an appropriate terrain/road height.
   The route intentionally has no altitude. Cesium uses height above the WGS84
   ellipsoid; do not treat zero as Calgary's ground elevation or mix height datums.
3. Convert each longitude/latitude/height using
   `CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(double3)`.
   Pass the resulting ECEF position to the scene's
   `CesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(double3)`.
   Keep geographic/ECEF calculations in doubles; convert to floats only for the
   final Unity positions. The latter returns positions relative to the georeference.
4. Place a `LineRenderer` under that georeference with identity local transform,
   set `useWorldSpace = false`, set `positionCount`, and assign the local positions
   in route order. If using world space or another parent, transform those positions
   accordingly. Use a small display offset above sampled terrain to avoid z-fighting.
   Refresh positions if the georeference origin moves; cached local positions will
   otherwise become stale.
5. Check the line against rendered streets and terrain. Sparse vertices on a slope
   may need interpolation and additional terrain samples. OSM geometry, terrain,
   and 3D city imagery may differ; the JSON does not guarantee visual alignment.

A `CesiumGlobeAnchor.SetPositionLongitudeLatitudeHeight(lon, lat, height)` is also
useful for start/end markers. Ground-height handling belongs in the Unity project.

Cesium references: [placing objects](https://cesium.com/learn/unity/unity-placing-objects/),
[georeference conversion](https://cesium.com/learn/cesium-unity/ref-doc/classCesiumForUnity_1_1CesiumGeoreference.html),
[globe anchor](https://cesium.com/learn/cesium-unity/ref-doc/classCesiumForUnity_1_1CesiumGlobeAnchor.html).

## Regenerate

In this routing folder, with `HEIGIT_API_KEY` already set privately:

```bash
python3 route_export.py --start-lon -114.0631 --start-lat 51.0443 --end-lon -114.0680 --end-lat 51.0540 --output route.json
```

No flags uses the Calgary Tower → Prince's Island Park presets. Regeneration
requires the API key; playing the exported route does not. The included route was regenerated successfully through the live API on
2026-10-24: 65 points, 12 steps, 1,421.0 m and an estimated 320.4 s. JSON parsing,
encoded/decoded coordinate agreement, Calgary bounds, and step indices passed
validation. Terrain sampling and visual alignment remain Unity integration tasks.
