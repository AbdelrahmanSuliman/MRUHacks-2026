# File-by-file assembly and testing

All paths below are relative to the extracted `CalgaryCycling` project root. Files are already in the correct directories; do not create duplicate copies. Each C# filename matches its primary class. The complete contents are in `COMPLETE_SOURCE.md` and in the actual files.

| File | Responsibility and references | How to test in Unity |
|---|---|---|
| `Packages/manifest.json` | Exact direct dependencies and Cesium registry. No Inspector reference. | Package Manager resolves the pinned versions without errors. |
| `ProjectSettings/ProjectVersion.txt` | Editor baseline. Other Unity settings are generated on opening and by the scene builder. | Hub selects 6000.0.66f2. |
| `.gitignore` | Excludes Unity caches, builds, IDE files and credential filename. | Ensure secrets are outside the repo; review staged files before committing. |
| `Assets/Cycling/Scripts/RideSettings.cs` | Serialized editable configuration; no secrets. Generator creates `Settings/Calgary.asset`. | Outside Play, change speed; start and inspect speed. Check right-stick setting and invalid route handling. |
| `Assets/Cycling/Scripts/RoutePath.cs` | Pure route sampling and local distance queries. Constructed by RideApp; no component or Inspector setup. | Run Core Checks; verify straight path distance, endpoint clamping and invalid points. |
| `Assets/Cycling/Scripts/RideInput.cs` | Disposable shared Input System action collection. Created by RideApp, no PlayerInput asset required. | Keyboard and Touch yield the same left/right motion; confirm X/Y/A/B and trigger on device. |
| `Assets/Cycling/Scripts/BicycleMotor.cs` | Actual bicycle translation, smoothed steering and arrival. Runtime component on Bicycle; Initialize assigns settings/path. | Run Core Checks; use V on desktop and watch x/lateral position change under steering. |
| `Assets/Cycling/Scripts/RiderCamera.cs` | Runtime XROrigin, Camera Offset, Main Camera and TrackedPoseDriver. Initialize supplies eye height. | Desktop works without headset; Quest tracks natural head motion while motor is paused; C/A requests recenter. |
| `Assets/Cycling/Scripts/SceneryService.cs` | Credential loading, Cesium tileset configuration, readiness estimate and recovery. Initialize receives settings and georeference. | Missing/malformed key, HTTP failure, timeout, successful load and Retry; interrupt network mid-ride. |
| `Assets/Cycling/Scripts/CreditDisplay.cs` | Built-in Cesium UIDocument routed to stereo-visible texture. Initialize receives active camera; package prefab required. | Real Google/provider credits visible, not clipped, in desktop and both headset eyes. |
| `Assets/Cycling/Scripts/RideUI.cs` | Builds all uGUI controls, mouse interaction, gaze/trigger targeting and HUD. Initialize receives RideApp. | Exercise every button on both platforms, including speed/sensitivity and error Retry. |
| `Assets/Cycling/Scripts/RideApp.cs` | State machine and runtime composition, bicycle/deck meshes, focus pause and setup error display. Only serialized reference is Settings. | Start, pause, resume, restart, destination, menu, focus loss and quit transitions. |
| `Assets/Cycling/Editor/CreateDemoScene.cs` | Menu command builds scene, settings and URP assets, sets player defaults and retains runtime shader. Editor-only directory. | Run Create Demo Scene; inspect created assets and build scene list. Existing settings assets are reused. |
| `Assets/Cycling/Editor/CoreChecks.cs` | In-editor assertions for route and motor invariants; destroys temporary objects in finally. | Calgary Cycling > Run Core Checks gives PASS or a precise thrown failure. Not a headset test. |
| `README.md` | Dependencies, beginner assembly, secrets, route calibration, deployment, troubleshooting and demonstration gate. | Follow in a fresh extracted project. |
| `Docs/FILE_GUIDE.md` | This inventory. | Match filenames to archive. |
| `Docs/COMPLETE_SOURCE.md` | Full code/configuration listing. | Read or diff against the executable files; not an extra script. |
| `Docs/VALIDATION.md` | What was actually verified here versus outstanding local/device work. | Fill out the unrun items after testing. |

## Generated Unity assets

`Calgary Cycling > Create Demo Scene` creates:

- `Assets/Cycling/Settings/Calgary.asset`: editable RideSettings; assigned to the scene root's RideApp.
- `Assets/Cycling/Settings/CyclingRenderer.asset`: Universal Renderer data.
- `Assets/Cycling/Settings/CyclingURP.asset`: URP asset, assigned to Graphics and all Quality levels.
- `Assets/Cycling/Scenes/Calgary.unity`: scene with one application root.

These assets are intentionally generated inside Unity instead of supplying guessed YAML/GUIDs. Unity also generates `.meta`, additional ProjectSettings and `packages-lock.json`. Preserve them after a successful import to make your local project reproducible.

At runtime, the application adds Calgary Georeference, Google Photorealistic 3D Tiles, Bicycle, XR Origin/Camera Offset/Main Camera, authored deck segments, Ride UI, Map Attribution and an Event System. Scenes/Settings directories hold persistent Unity assets. Scripts contains focused runtime classes. Editor contains tools excluded from player builds. Prefabs, Materials and UI asset folders are unnecessary here because the corresponding simple objects are generated in source; Cesium owns its own package resources.

## Manual scene equivalent

If you prefer manual assembly, create an empty scene, remove the template camera, create an empty root at identity, add RideApp, create a RideSettings asset through Create > Calgary Cycling > Ride Settings and assign it. Assign a valid URP pipeline to Graphics and every Quality level, include its Unlit shader in Always Included Shaders, apply the player/OpenXR settings from README, save the scene and add it to Build Profiles. Do not manually add the runtime-generated rig/UI/tileset as well.
