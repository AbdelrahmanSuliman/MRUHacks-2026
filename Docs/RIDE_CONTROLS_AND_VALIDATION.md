# Ride controls and verification

## Quest controls

Hold a controller in each hand like bicycle grips. With both controllers tracked
and your hands facing straight ahead, the handlebars centre automatically. Turn the pair
horizontally: move the left hand forward and right hand back to steer right;
reverse to steer left. Hand height, wrist pitch and wrist roll are ignored.

Losing either controller's tracking disengages the gesture. Starting or resuming a
ride resets calibration. The selected thumbstick's horizontal axis remains
available as a fallback when both controllers are unavailable. Vertical thumbstick
movement has no steering effect.

- A or right trigger: select the button you are looking at.
- X: pause/resume.
- Y: restart.
- B: return to the menu.
- Desktop: A/D or left/right arrows steer; Space pauses/resumes.

## Route clearance

Imported vegetation, rocks, buildings and props are checked using the bounds of
their transformed meshes against every segment of the current route. Placement
reserves the ride corridor plus a one-metre margin. A spatial index keeps these
checks local even on longer address routes. Conflicting placements are moved to
a clear location or omitted. New checks run each time a route is rebuilt.

The OpenRouteService centreline stays unchanged. Synthetic water that previously
followed every route bend was removed; river geography is supplied by Cesium.
Imported mesh bases are seated on the ground rather than relying on their pivots.

## Repeatable checks

Run **Calgary Cycling > Validation > Check Ride Product** (Ctrl+Shift+F9).
The report is written to `Temp/CyclingProductChecks.txt`.

This exercises actual imported prefabs on a tight return-bend route in all three
zones, replays tracked controller positions through `RideInput`, and
runs the existing motor and route checks. It verifies left/right steering,
vertical-motion rejection, centred hand positions, tracking loss, horizontal
thumbstick fallback, route preservation, acceleration, corridor limits and arrival.
A GeoJSON regression also checks that address names remain paired with the
coordinates from their own feature, independent of JSON property order.

In Play Mode, run **Calgary Cycling > Validation > Inspect Active Ride**
(Ctrl+Shift+F10) to count actual imported objects and corridor overlaps in the
active route. Its report is `Temp/CyclingActiveRide.txt`.

Editor regression results: all checks passed, including no unexpected Unity
errors. The authored Peace Bridge loop instantiated 70 imported objects with
zero corridor overlaps during the Game view test. Physical Quest grip comfort,
tracking and frame rate still require validation on the headset after rebuilding
and installing the APK.

The corrected live Peace Bridge to 1300 Zoo Road NE route was also tested through
address selection, route building and riding: 4,101.6 metres, 165 instantiated
imported objects, zero corridor overlaps. The earlier cross-feature address parser
could incorrectly route this selection toward a different result; this is now
covered by the GeoJSON regression.

## Quest credentials

Keep the local `cycling-secrets.json` in `Assets/StreamingAssets` when building an
APK. It is ignored by Git. On Android, the loader reads this file inside the APK
with UnityWebRequest; the Windows persistent-data directory does not transfer to
the headset.

The packaged file uses a UTF-8 byte-order mark. UnityWebRequest preserves that
marker in its text response, whereas File.ReadAllText removes it. Both credential
parsers now remove the marker before JSON parsing. This fixes the false missing-key
message seen only in the headset.

Run **Calgary Cycling > Validation > Check Android Credentials** (Ctrl+Shift+F8).
It loads the actual StreamingAssets file with UnityWebRequest and exercises both
production credential parsers without printing keys. The same BOM-containing file
failed both checks before the fix and passed both afterward. The report is
`Temp/CyclingCredentialChecks.txt`.

**Calgary Cycling > Build > Build Quest APK** (Ctrl+Shift+F7) builds enabled scenes
to `Builds/CalgaryCycling-Quest.apk`. The build result is recorded in
`Temp/CyclingQuestBuild.txt`.

The corrected APK built with zero errors and was installed over the existing app
on the connected Quest 2 using `adb install -r`. Archive checks confirmed both the
BOM-containing credentials file and the corrected parsers in that APK. A full
headset scenery check was pending because the Quest was asleep after installation.

## Quest controller fix and device verification

The Android Oculus Touch Controller Profile was disabled (`m_enabled: 0`) even
though the OpenXR loader was enabled. It is now enabled for Quest 2. Android
builds also validate this setting before building, including normal Build And Run.

`RideInput` now accepts native controller devices without requiring the optional
HeldInHand flag, and polls A, trigger, X, Y and B from both native XR features and
the Input System. The menu says when the right controller is missing and displays
a clear ready message after scenery loads. Menu selection remains gaze based:
look at the desired button, then press A or the right trigger.

The corrected APK built with zero errors and was installed on the connected
Quest 2. Device logs then recorded `[RideInput] Right-controller select button
detected.` followed by `[RideApp] Ride started.` This verifies actual headset
button delivery and the transition into riding, beyond simulated input checks.

**Calgary Cycling > Validation > Check VR Start Button** verifies the Android
profile, replays A/trigger and hold/release states through the production input
reader, exercises the actual Start Ride button callback, and checks the ready
message. These checks passed. Edit Mode does not exercise the rendered spatial
gaze raycast; that path was confirmed by the actual Quest logs above. The report
is `Temp/CyclingVRMenuChecks.txt`.

Editor checks can also be queued without keyboard automation by writing one of
`vr-menu`, `product`, `credentials` or `build-quest` to
`Temp/CyclingEditorCommand.txt`. The Editor refreshes scripts before executing it.
