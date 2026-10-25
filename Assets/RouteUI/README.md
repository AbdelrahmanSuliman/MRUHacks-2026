# Task 7: route preview and active-path HUD

Extends `RoutePlannerUI` and `VeloUIManager`; no second UI, scene changes, package
changes, or motor changes. Work branch: `feature/route-planning-ui`; target: `Project`.

## Behavior

- Dashboard reuses the existing EventSystem (normally created by Cesium credits).
  It creates one only if absent and adds an Input System module only if no module exists.
- Select Route includes a saved route preview and retains all online address-search
  and route-calculation actions. The planner is modal relative to the dashboard;
  Close returns to the menu. Gaze targets the open planner's raycaster and actual
  Button/InputField objects rather than their text children.
- `Resources/RouteUI/OfflineRoute.json` is the unchanged export from the locally
  cached `origin/feature/cycling-routing:routing/route.json`. It contains 65 vertices,
  12 steps, 1421 metres and 320.4 seconds. No credentials are included.
- The offline route is **preview only**, visibly labelled. It does not change the
  bicycle path. Close and Start Ride still ride the current motor path.
- HUD shows distance remaining on `Motor.Route`, speed, distance ridden and active
  ride time. Fixed cadence/calories were removed from the riding HUD. Timer resets
  for new rides/restarts and does not advance while paused. Existing training cards
  and summary/history telemetry remain placeholders outside this change.
- `OfflineRouteSummary` parses only endpoint/summary metadata. JsonUtility ignores
  the nested coordinate arrays. Missing endpoint names fall back to latitude,
  longitude labels. Full geometry and steps remain preserved in the JSON asset.

## Verification

Automated externally: all runtime C# compiled with Unity's bundled Roslyn compiler
and the generated project's assembly references. Runtime and Editor sources also
compile together for semantic validation. This is not a Unity import, Play mode,
IL2CPP build, or headset test. Scoped whitespace checks and export integrity/schema
checks run separately. No automatic Unity runtime test has been executed.

In Unity, exit Play mode and allow script import/compilation to finish:

1. Check Console for compilation errors. Run Calgary Cycling > Run Route UI Data
   Checks; expect PASS. Run Core Checks again; expect PASS.
2. Open the existing Calgary scene. Enter Play mode. Search Hierarchy for
   `t:EventSystem`; expect exactly one active component. Repeat Play twice and
   confirm the duplicate warning does not return. Check attribution remains visible.
3. Open Select Route. Expect OFFLINE PREVIEW, endpoint coordinates, 1.42 km,
   5.3 min, source credit, and the explicit statement that the route is not applied.
   Dashboard buttons should be hidden behind the planner. Close and reopen;
   click Preview Offline Route and confirm it restores the saved preview.
4. Online routing: with existing credentials configured privately, search both
   endpoints and calculate a route. Confirm online statistics and the existing
   apply behavior still work. Click offline preview afterward; it must not replace
   that active motor path. Missing routing credentials should leave offline preview usable.
5. Close planner and Start Ride. Remaining distance must decrease along the ridden
   path, not start at the JSON total. Pause freezes ride time/distance; Resume
   continues; Restart/new Start resets ride time and progress. HUD toggle still works.
   Check arrival and menu transitions; verify text fits at the current UI scale.
6. Quest validation is outstanding: gaze/right-trigger clicks on both canvases,
   planner text entry for online search, comfortable HUD size/placement, attribution
   visibility and no duplicate actions. Desktop success does not establish VR success.

## Integration boundary / next milestone

Do not show JSON navigation on the existing demo path. Coordinate with the route
conversion owner before adding saved-route activation. Agree on:

- An active route identity and a ready/failed signal before Start Ride.
- Longitude-first geographic vertices, terrain heights, and local converted geometry.
- Original vertex indices mapped to cumulative distances on the actual motor path,
  accounting for RoutePath smoothing/resampling, so step `wayPoints` match progress.
- Which layer replaces/rebuilds the visible road and handles scenery readiness.

Then add active-route navigation state under RouteUI, consume motor distance in
that same distance basis, and show the active instruction/upcoming maneuver distance.
Clear navigation when another route is loaded. Until this contract exists, HUD
contains no turn instructions. `RideApp.ApplyOnlineRoute` currently uses approximate
flat conversion and does not rebuild the authored road; this pre-existing behavior
is unchanged.

Coordinate edits to the two existing UI scripts with their owner. Do not change
RideApp, steering, lane constraints, XR configuration or Calgary.unity for this
handoff. Keep package/settings and IDE diffs out of the UI commit and PR.
