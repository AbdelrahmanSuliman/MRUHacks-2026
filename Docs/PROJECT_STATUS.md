# Calgary Cycling Simulator — Project Status

## Current stage

The project is at a functional prototype/demo stage. It contains a Unity VR cycling experience centered on Calgary scenery, route selection, bicycle movement, and real address-based route planning.

## Implemented

- Unity VR ride experience with an XR camera rig and bicycle controller.
- Cesium-based Calgary georeference and streamed 3D scenery support.
- Automatic forward cycling with steering, acceleration, pausing, restarting, recentering, and arrival handling.
- World-space VR dashboard and route-planning UI.
- `SAMPLE ROUTES` menu with three curated demo routes:
  - Riverfront Loop — Peace Bridge views.
  - Downtown Discovery — city landmark showcase.
  - Bow Scenic Ride — longest scenic showcase.
- Selecting a sample route changes the actual simulated ride path and rebuilds its visible route overlay.
- Address search for Calgary locations using OpenRouteService geocoding.
- Up to five address suggestions displayed for each start and finish search.
- Real cycling route calculation between selected addresses using OpenRouteService.
- Online route geometry follows the returned road coordinates without smoothing across corners.
- The visible route overlay is rebuilt when an online route is selected, preventing the old demo route from remaining visible.
- Invalid, blank, malformed, or unknown addresses fail safely with a user-facing message instead of crashing.
- Missing or placeholder API keys are detected safely.
- Runtime stylized environment builder with riverfront, downtown, and evergreen zone dressing.
- Warm directional stylized sunlight, graphic fog, faceted trees, rocks, flowers, water, and colorful building forms.
- Optional stylized quality profile for environment budgets and Quest tuning.
- Optional external bicycle prefab integration with named visual mount-point fallbacks.

## External configuration

The application needs valid credentials for online scenery and real route planning:

```json
{
  "cesiumIonAccessToken": "YOUR_CESIUM_ION_TOKEN",
  "cesiumIonAssetId": 1,
  "openRouteServiceApiKey": "YOUR_OPENROUTESERVICE_KEY"
}
```

The secrets file is read from Unity's `Application.persistentDataPath` as `cycling-secrets.json`. The route planner also supports the environment variable `CALGARY_ORS_API_KEY`; Cesium supports `CALGARY_CESIUM_ION_TOKEN`.

Credentials must not be committed to the repository or shared publicly. Internet access, valid permissions, and available service quota are required.

## Route behavior

Sample routes are authored local demo paths near the Peace Bridge origin. They are intended to showcase movement and scenery and are not surveyed public cycling routes.

Address-selected routes come from OpenRouteService's cycling router. The selected route is converted from WGS84 longitude/latitude coordinates into the local Cesium georeference and used as the bicycle's ride path.

## Validation completed

- Source-level route and UI changes reviewed.
- `git diff --check` passes.
- Unity-compatible numeric parsing was used for route response data.
- Invalid-address and malformed-response paths are guarded.
- No real Unity Editor, headset, Android build, or live API run was available in this environment.

## Remaining work

- Run the project in Unity and resolve any package/import issues.
- Test Cesium scenery with a valid token and asset permissions.
- Test OpenRouteService geocoding and cycling requests with a valid key.
- Verify the three sample routes visually in the headset and adjust their authored coordinates if needed.
- Verify route alignment against streamed 3D scenery and tune the origin elevation.
- Test the complete workflow on the target Android/Quest device.
- Add production-quality route rendering and route-length/performance limits for very long online routes.
- Supply and optimize the final external stylized bicycle asset.
- Replace procedural placeholder environment pieces with authored hero assets where desired.
- Validate the stylized Cesium blend and lighting visually in headset.

## Important project note

The repository currently shows an unrelated pre-existing deletion of `README.md` in Git status. This status summary does not restore or modify that file.
