# Task 7: route preview and active-path HUD

Extends `RoutePlannerUI` and `VeloUIManager`; no second UI, scene changes, package
changes, or motor changes. Work branch: `feature/route-planning-ui`; target: `Project`.

## Behavior

- Select Route defaults to predefined cards in the existing canvas. Cards use the
  existing Button and gaze/mouse system, with selection text and a green highlight.
- **Peace Bridge Demo** is the sole rideable predefined route. Distance comes from
  the actual startup motor path; duration is an estimate at configured cruise speed
  (excluding acceleration/steering). Start Ride restarts that exact path through
  RideApp.StartRide; selecting a card does not change motor geometry.
- **Saved Calgary Route** is selectable for preview only: 1.42 km / 5.3 min from
  the bundled JSON. Start Ride is disabled for this selection, including the main
  menu Start button. No other route data is invented.
- The selector captures the startup RoutePath reference. It rechecks exact identity
  with Motor.Route both for button availability and immediately before starting.
  If online routing or another component replaces the active path, the demo card
  becomes unavailable. Restart Play/the application to restore the authored demo.
  The UI does not call Motor.Initialize or change movement/conversion code.
- Online Routing remains a secondary panel with existing search/calculate behavior.
  Its existing ApplyOnlineRoute call still replaces the motor path; this is not a
  verified predefined route. Return to predefined selection afterward to see its
  unavailable status. Route restoration/online ride activation needs the geometry
  handoff; the predefined Start guard never claims an online path is the demo.
- Training now links to the real route selector instead of showing fake route cards.
- Dashboard reuses the existing EventSystem. HUD remaining distance still uses
  Motor.Route.Length minus Motor.Distance, clamped at zero. No HUD, steering,
  lane, Cesium, scene or XR changes are part of the predefined route increment.
- The saved export is unchanged from the locally cached routing branch: 65 vertices,
  12 steps, 1421 metres, 320.4 seconds. Credentials are not required for the preview.
  OfflineRouteSummary parses endpoint/summary metadata; nested geometry and steps
  are preserved in JSON for teammate integration. Summary/history telemetry remains
  placeholder data outside this increment.

## Verification

Automated externally: all runtime C# compiled with Unity's bundled Roslyn compiler
and the generated project's assembly references. Runtime and Editor sources also
compile together for semantic validation. This is not a Unity import, Play mode,
IL2CPP build, or headset test. Scoped whitespace checks and export integrity/schema
checks run separately. No automatic Unity runtime test has been executed.

In Unity, exit Play mode and allow script compilation to finish:

1. Run Calgary Cycling > Run Route UI Data Checks and Run Core Checks; expect PASS.
2. Open Calgary and enter Play. Select Route should show two cards with demo selected.
   Verify name, actual demo distance, approximate cruise duration and rideability.
3. Select Saved Calgary Route. Expect green selection highlight and disabled Start.
   Close: main-menu Start must also be disabled. Reopen and select Peace Bridge Demo.
4. Once scenery is ready or offline fallback is active, Start must enable. Start the
   demo and verify the bicycle follows the existing road and HUD remaining distance
   decreases over that same short path. Pause/resume/restart and arrival must work.
5. Main-menu Start and Training > Select Route must use the same selected route.
   No City Cruise/Forest Trail/Mountain Climb placeholder choices should remain.
6. Optional online test with privately configured credentials: Online Routing still
   searches/calculates. After applying a route, return to cards: the demo must show
   unavailable and Start must remain disabled, even if the online path has a similar
   length. Exit/reenter Play to restore the demo. Never treat this as validated
   exported-route activation or terrain alignment.
7. Repeat Play and check one active EventSystem, no duplicate warning, readable cards,
   and attribution visibility. Missing offline JSON should disable only that card.
8. Quest 2 testing remains manual: on the existing Quest build setup, verify gaze
   and right-trigger select both cards; selected state and disabled Start are clear;
   Start enters the demo; steering/pause/restart, HUD and attribution remain usable
   in both eyes. Test seated readability and comfort. No Quest compatibility result
   is claimed by compilation or desktop testing.

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
