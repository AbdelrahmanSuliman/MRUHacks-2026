# Calgary Cycling Simulator — source project and assembly guide

**Note: This "main_" branch is where our code is working out of, not "main".**

## 1. Exact dependency baseline

Start here. These are deliberately pinned versions, not a claim that they are the newest releases.

| Dependency | Version | Role |
|---|---|---|
| Unity Editor | **6000.0.66f2**, Unity 6.0 LTS | Editor and Android player |
| Universal Render Pipeline and Shader Graph | **17.0.4** | Rendering; aligned with the Unity 6.0 package family |
| Cesium for Unity | **1.18.1** | Cesium ion 3D Tiles streaming and georeference |
| Meta XR Core SDK | **77.0.0** | Meta SDK installation and project tooling |
| Unity OpenXR Plugin | **1.14.0** | The active XR provider, Touch profiles and headset tracking |
| XR Plug-in Management | **4.4.0** | Android XR initialization |
| XR Core Utilities | **2.3.0** | XROrigin |
| Input System | **1.11.2** | Desktop, Touch and tracked poses |
| Unity Mathematics | **1.3.2** | Cesium dependency |
| Unity UI (uGUI) | **2.0.0** | World-space controls |
| Unity Test Framework | **1.4.5** | Explicit package baseline; provided core checks use an editor menu |

`Packages/manifest.json` includes these dependencies and Cesium's scoped registry. Other dependencies resolve through Unity. Commit Unity's resulting `packages-lock.json` after successful resolution; none is fabricated in this archive. Android SDK, NDK and OpenJDK must be the modules installed for this editor by Unity Hub, not independently chosen versions.

This is a source implementation and scene generator, **not a verified or headset-certified build**. C# syntax was parsed here, and key Cesium 1.18.1, Input System 1.11.2 and XR Core Utilities 2.3.0 APIs were inspected in source. No Unity editor, C# compiler, Cesium ion token or Quest device was available for an end-to-end test. Resolve packages, compile, run the included checks, and perform the device checklist before presenting. Package metadata compatibility is not equivalent to a tested full-stack combination.

The documented assembly path is Windows or macOS. Meta Core 77 has a reported Linux editor configuration issue; this project has not been imported or run in Unity on Linux. For a Quest on a Linux laptop, the reliable Linux-side option is to install and launch the Android APK on the headset with ADB; the headset renders the experience itself. This is not laptop-rendered PCVR. Running PCVR from Linux additionally requires a Linux-compatible Unity Editor and OpenXR runtime/headset setup, which is not validated for this project. Quest Link PCVR is not the same as installing the standalone APK.

## 2. First project setup

1. Install Unity Hub, sign in, and install **6000.0.66f2** from Unity's release archive if it is not in Hub's normal list. For standalone Quest APKs, include **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK**. For a Windows desktop player, include **Windows Build Support (IL2CPP)**. Editor Play mode does not require either player target's build modules. Activate a Unity license suitable for your use.
2. Extract this archive to a writable folder. In Unity Hub choose **Add > Add project from disk** and choose the project folder containing `Assets`, `Packages` and `ProjectSettings` (for this archive: `MRUHacks-2026-1`).
3. Open it with the pinned editor. Unity generates normal missing settings and `.meta` files. Allow package downloads and imports to finish. The first import can take several minutes.
4. If prompted to enable the new Input System, accept and restart. Otherwise choose **Edit > Project Settings > Player > Other Settings > Active Input Handling > Input System Package (New)** and restart. Do not choose the old input manager.
5. The manifest installs Cesium from `https://unity.pkg.cesium.com`, scope `com.cesium.unity`. If your package manager cannot reach that registry, download the **release `.tgz`**, not the GitHub source ZIP, from [Cesium 1.18.1](https://github.com/CesiumGS/cesium-unity/releases/tag/v1.18.1), then use Package Manager > + > Add package from tarball. Update the manifest to that local tarball reference. Source ZIPs do not contain the built native libraries.
6. In the Unity menu select **Calgary Cycling > Create Demo Scene**. Save any existing scene if prompted. The command creates `Assets/Cycling/Scenes/Calgary.unity`, the settings asset and URP renderer/pipeline assets. It adds Calgary to the build scene list. Do this after scripts have compiled.
7. Keep the scene's root at position zero, rotation zero and scale one. The only Inspector reference required is `RideApp > Settings`, which the generator assigns automatically.
8. Open **Project Settings > XR Plug-in Management**. For keyboard-only desktop development, leave the desktop XR provider disabled. For Quest Link PCVR in the Windows Editor, enable **OpenXR** on the desktop tab and **Initialize XR on Startup**; connect the Quest with Link and make Meta Quest Link the active OpenXR runtime. On Android, enable **OpenXR** and **Initialize XR on Startup** for standalone builds.
9. Under Android **OpenXR**, add **Oculus Touch Controller Profile** to Interaction Profiles and enable **Meta Quest Support** in the feature groups. Use **Single Pass Instanced** rendering. For Windows PCVR, add the Touch profile in the desktop OpenXR settings too. Fix relevant errors in OpenXR Project Validation. Menu wording varies slightly across editor/package versions.
10. The manifest includes Meta XR Core. Do not add an OVRCameraRig or an OVRManager camera to this scene: `RiderCamera` already creates an OpenXR XROrigin and tracked camera. The application uses Unity OpenXR input APIs, not OVRInput. Meta's Interaction SDK, XR Interaction Toolkit, passthrough, hand tracking and Platform SDK are not dependencies of this implementation.
11. Run **Calgary Cycling > Run Core Checks**. Expect one `PASS` Console message. A failure must be fixed before proceeding.
12. Configure Cesium ion access below, then press Play. Wait for the loading message to finish and click **Start Ride**.

No downloaded art, paid bicycle model, prefab or hand-wired UI is required. The source creates a basic bicycle representation, deck, menu and camera. The scenery is streamed from Cesium ion; it is separate from the authored bicycle and route.

### Editor quick start

For a keyboard-only test, leave desktop XR disabled, open
`Assets/Cycling/Scenes/Calgary.unity`, click the **Game** view, and press **Play**.
Wait for scenery loading to finish, then click **Start Ride**. Use **A/D** or the
left/right arrow keys to steer and **Space** to pause or resume. The Editor test
requires the project to compile and the Cesium credentials to be configured, but
does not require an Android build or a headset.

## 3. Cesium ion configuration and credentials

Create a Cesium ion account and an access token that can access the terrain asset selected by `cesiumIonAssetId`, imagery asset selected by `cesiumIonImageryAssetId`, and Cesium ion OSM Buildings asset **96188**. Defaults are **1** (Cesium World Terrain) and **2** (Cesium World Imagery). The imagery layer adds aerial color/texture over the terrain while OSM Buildings supplies 3D building shapes. This is the best current no-Google-key option in this project; it is not Google's photorealistic 3D mesh. Review your account's applicable usage limits and terms.

The token is not included in source control. Create a local file named `cycling-secrets.json` containing:

```json
{
  "cesiumIonAccessToken": "YOUR_CESIUM_ION_ACCESS_TOKEN",
  "cesiumIonAssetId": 1,
  "cesiumIonImageryAssetId": 2,
  "openRouteServiceApiKey": "OPTIONAL_OPENROUTESERVICE_KEY"
}
```

Replace the Cesium token placeholder with your token. The imagery ID can be set to `0` to disable the imagery overlay. The route-planner key is optional; it is only needed for Calgary address search and real cycling routes. Obtain it from OpenRouteService if you want that feature. Keep the file outside the project and source control. Do not paste credentials into an issue, screenshot, Inspector asset or README.

**Desktop:** use **Calgary Cycling > Show Credential Folder**, then create the file in the folder opened. The code reads `Application.persistentDataPath`. Alternatively, set `CALGARY_CESIUM_ION_TOKEN` for scenery and/or `CALGARY_ORS_API_KEY` for route planning before launching Unity; each environment variable takes precedence over its matching file value.

**Quest APK:** install and launch the APK once, then close it. With ADB connected, run from the directory containing your local secrets file:

```bash
adb shell mkdir -p /sdcard/Android/data/ca.calgarycycling.simulator/files
adb push cycling-secrets.json /sdcard/Android/data/ca.calgarycycling.simulator/files/cycling-secrets.json
```

Launch again or choose **Retry Scenery**. This device file is readable on a developer-controlled device; it is not a secret vault. The token is used by Cesium at runtime and may appear in SDK diagnostics. Inspect and redact Unity/ADB logs before sharing. Revoke the demonstration token when finished. A public release requires a separately reviewed credential-delivery and authorization design; it is outside this local demonstration build.

`SceneryService` creates Cesium ion terrain and OSM Buildings tilesets plus an imagery overlay. Google Photorealistic 3D Tiles is not enabled: it needs a Google Maps Platform key and billing, and its current pricing/terms should be checked before choosing it. Cesium imagery and terrain still depend on the token's asset access and account terms.

There is no offline scenery mode. The application needs internet access throughout the ride. It does not download a permanent scenery pack. Temporary SDK streaming caches are not an offline distribution mechanism. Follow Cesium ion and underlying data-provider terms and attribution requirements.

## 4. Calgary origin and route

The georeference uses **latitude 51.05389, longitude -114.07883**, the published Peace Bridge location. This is a verified landmark coordinate, not a claimed surveyed trail endpoint. The initial **1040 m ellipsoid height is an estimate** and must be checked on the actual streamed view. Cesium height is above WGS84 ellipsoid, not above mean sea level; a normal map altitude cannot be substituted without considering the vertical datum.

The default five local waypoints describe a roughly 100 m, gently curving **authored demonstration route near the landmark**. They are not claimed to match the bridge deck or a public path. A dark procedural deck makes the synthetic route explicit. It is independent authored geometry, not geometry traced from streamed tiles. The UI identifies this distinction.

Before a live demonstration:

1. Open `Assets/Cycling/Settings/Calgary.asset` outside Play mode.
2. Keep the verified latitude/longitude, or supply another independently verified location. Set ellipsoid height from an appropriately licensed independent source or your own survey, and check for obvious clipping in the streamed scene.
3. Edit `Waypoints`: X is east, Y is up, Z is north, in metres from the origin. The last waypoint is the destination. Points must be finite, consecutive points at least 0.5 m apart, and the sampled route between 2 and 1000 m.
4. Keep turns broad, slopes gentle and the corridor clear. Hermite smoothing can overshoot abrupt corners; do not use hairpins, self-intersections or closely packed points. Review the entire route, not just its start.
5. Use the authored deck for the initial demonstration. If replacing it with a real cycling alignment, use independently licensed trail/height data and validate the route; this archive does not include such surveyed data. Disable `Show Demo Deck` only when the independently authored alignment is ready.
6. Press Play, inspect in first person, and press **V** to inspect from behind on desktop. Stop Play before changing persistent settings. Runtime speed/sensitivity buttons alter a clone and do not save back to the asset.

The root remains fixed for the short ride, so floating-origin movement is unnecessary. Do not add CesiumOriginShift, move the georeference mid-ride, or rescale it without also redesigning route coordinates.

### How steering actually works

The route supplies centreline position C(s), forward tangent T(s), and right vector R(s). The bicycle position is:

`P = C(s) + lateralOffset * R(s)`

Forward progress increases automatically. A smoothed stick value determines a small heading offset; its sine changes lateral position and its cosine advances route distance. Outward lateral speed fades near the corridor boundary. Releasing the stick holds the lateral position. Opposite input moves back across the corridor. This is constrained route steering, not free-roaming rigidbody bicycle simulation.

Both the bicycle and XR origin inherit this actual position. Since the bicycle is under the georeference, a lateral change corresponds to a change in geographic location. Steering is not only visual. Yaw follows the direction of displacement with a rate limit, with zero artificial roll/pitch. Direction alignment can lag slightly to limit camera rotation. Use broad curves so that lag remains small.

Cruise acceleration and destination braking are bounded. Pause stops immediately for comfort/control; resume accelerates smoothly. Restart intentionally resets position while paused. No gravity, wheel-ground collision or unstable Rigidbody is used. Distance/progress report **centreline distance**, not the slightly longer path resulting from lateral weaving.

## 5. Controls and interface

| Action | Desktop | Quest Touch |
|---|---|---|
| Start ride | Click Start Ride | Look at Start Ride, press right trigger |
| Steer | A / D or left / right arrows | Left thumbstick horizontal by default |
| Pause / resume | Space | X on left controller |
| Restart, then wait for resume | R | Y on left controller |
| Main menu | Escape | B on right controller |
| Recenter (pauses first) | C | A on right controller |
| Optional third-person debug | V | Disabled in VR |
| UI interaction | Mouse click | Gaze cursor + right trigger |
| Speed / sensitivity | Click minus/plus | Gaze at minus/plus + trigger |
| Quit | Quit button | Quit button or headset system menu |

Set `Use Right Stick` on the settings asset if preferred. The control instructions say left stick is the default. Keyboard and Touch both feed `BicycleMotor.Tick`. Touch buttons use OpenXR primary/secondary controls, not reserved system buttons. This UI intentionally uses gaze targeting plus a physical controller trigger; it does not implement a tracked hand laser pointer. All settings use buttons so dragging a slider is unnecessary.

In desktop mode the mouse controls the world-space menu through uGUI's Input System module. In VR, the camera centre ray targets uGUI buttons and the right trigger invokes their click events. A cyan dot identifies the gaze direction. Keep your head still briefly over the intended button. The menu is attached to the bicycle, not the headset, so you can look around normally.

The headset camera uses TrackedPoseDriver with Update and Before Render pose updates. Only the parent rig receives simulated cycling movement. Device tracking origin is requested and the camera offset supplies seated eye height. If the runtime refuses recenter, use Meta's system recenter and the on-screen message. Do not overwrite the tracked camera rotation with steering input.

Start is disabled until the current view has produced a tile and the reported loading estimate remains near completion for two seconds. This is a heuristic, not proof that every visible tile is at maximum resolution. A loading timeout, loss of connectivity or tileset failure stops the ride and enables Retry. The static failure callback is filtered to this tileset. Individual child-tile failures may not trigger that callback; prolonged incomplete loading also times out. Short stalls may show coarse cached geometry. Retry resets the streaming session; it does not auto-resume a ride.

## 6. Live attribution in VR

`CreditDisplay` retains Cesium's built-in live credit collection and official imagery/provider strings. Its UI Toolkit document is rendered into a 1600×300 RenderTexture displayed on a small world-space panel under the headset view. This avoids the common issue of a desktop overlay missing from stereo rendering. An additional label distinguishes streamed scenery from the authored bicycle/deck.

Do not hide this panel during a ride or replace provider strings with a fixed copyright year. Before presenting, inspect both eyes for legibility, complete logos, current provider attribution and clipping at your headset's render resolution. The fixed-size panel must be enlarged if a particular view's credits do not fit. `showCreditsOnScreen` is forced on so provider attribution is not hidden behind a nonfunctional popup. Review the current Cesium ion and data-provider attribution requirements before distribution.

## 7. Android and Quest 2 build

1. In **File > Build Profiles**, add/select Android and switch the active platform. This editor baseline may label the platform Android rather than Meta Quest. Use Android for standalone Quest.
2. Verify that only the generated `Calgary.unity` scene is enabled. The scene generator sets package identifier **ca.calgarycycling.simulator**, IL2CPP, ARM64 only, Linear colour, minimum API 29 and Internet permission. Target API is Automatic/highest installed for local sideloading. Store submission requirements are separate and can change.
3. In Player > Other Settings, set graphics APIs explicitly to **Vulkan** for the initial build; disable Auto Graphics API. If the device/driver exhibits a verified rendering problem, test a separate OpenGLES3 build and compare. Use the Unity-installed JDK/SDK/NDK in External Tools.
4. Check Android OpenXR is enabled, Meta Quest Support enabled, Oculus Touch profile added, and initialization on startup enabled. Run Project Validation again after switching platforms. Do not enable both the Oculus and OpenXR loaders.
5. In the generated URP asset, verify 2× MSAA, HDR off, render scale 0.85, and no additional renderer effects. Disable main/additional light shadows and extra depth/opaque textures if a package/default has enabled them. Cesium tile materials are normally unlit. Keep the runtime Unlit shader in Graphics > Always Included Shaders; the scene generator adds it for the procedural objects.
6. Use a **Development Build** for initial profiling and troubleshooting. Release builds should be tested separately before the event. Do not enable Script Debugging in the final performance measurement.
7. Register/verify a Meta developer account. In the paired **Meta Horizon mobile app**, choose your headset > Headset Settings > Developer Mode and enable it. Account/organization enrollment screens vary; complete the requirements shown in Meta's developer dashboard.
8. Connect a USB-C **data** cable. Wear the headset and authorize USB debugging for your trusted computer. Install Meta's ADB USB driver on Windows if needed; macOS generally does not need that driver. In Meta Quest Developer Hub (MQDH), verify the device is active.
9. Run `adb devices`: the device must show `device`, not `unauthorized` or `offline`. `adb` is under the Unity Android SDK's `platform-tools` directory; use that executable or add its directory to PATH.
10. Choose the Quest as **Run Device**, then **Build and Run**. Save output as `Builds/CalgaryCycling.apk`. The first IL2CPP build can be slow. The headset may initially show the missing-key error; this is expected before provisioning credentials.
11. Copy the secrets file using the commands above. Relaunch and confirm real Calgary scenery loads. Try steering, every menu command, and recenter on the actual headset.

Alternative APK installation:

```bash
adb install -r Builds/CalgaryCycling.apk
adb shell monkey -p ca.calgarycycling.simulator -c android.intent.category.LAUNCHER 1
```

You can also install the APK with MQDH's device/app installation controls and launch it from the device's Unknown Sources app list. Do not uninstall an existing build casually: uninstall removes its app data, including the secrets file. A signature mismatch requires either the original signing key or an intentional uninstall/reprovision.

### APK creation and VR deployment pipeline

The APK is the Android application package produced by Unity. The normal flow is:

1. Unity imports and compiles the project, then builds the enabled Unity scene for
   Android using the Android SDK, NDK and OpenJDK installed through Unity Hub.
2. Unity's **Build** or **Build And Run** command produces an `.apk`, such as
   `Builds/CalgaryCycling.apk`. **Build And Run** also installs that APK on the
   selected Android device.
3. For a physical Quest, the APK is transferred and installed with ADB, MQDH, or
   Unity's **Build And Run** option. The Quest's Android/OpenXR runtime then
   launches the application and renders the VR experience.

BlueStacks is an Android emulator, not an APK compiler. It can install an APK
created by Unity for a desktop smoke test, but it does not provide the physical
Quest's headset tracking, controller input, OpenXR runtime, or standalone VR
performance. Therefore:

- Use **BlueStacks** to check that the Android package installs and starts when a
  compatible emulator configuration is available.
- Use a **Quest headset** for actual VR, OpenXR, controller, comfort, performance,
  and Cesium streaming validation.
- Installing an APK in BlueStacks does not automatically send it to a Quest or
  any other VR system. Build the APK with Unity, then install that same APK
  separately on the target headset.

The existing `1.apk` and `build1.apk` are already-built Android packages. They
can be installed for testing, but changing Unity scenes, scripts, packages, or
build settings requires rebuilding the APK from the Unity project.

### Windows laptop with Quest Link (PCVR)

1. Install the pinned Unity Editor and the Meta Quest Link PC app on Windows. Pair the Quest 2, then connect through Link (USB or Air Link) and enter the Link environment.
2. In Quest Link settings, make Meta Quest Link the active OpenXR runtime. In Unity's **XR Plug-in Management > Standalone**, enable OpenXR and initialize XR on startup; add the Oculus Touch Controller Profile.
3. Press Play in the Editor to test, or select the Windows desktop build profile and build a Windows player. Keep the headset connected and inside the Link environment while launching the player. Desktop XR disabled means keyboard-only mode, not PCVR.
4. Provision `cycling-secrets.json` in the Windows player's `Application.persistentDataPath` (or the Editor's path shown by **Calgary Cycling > Show Credential Folder**). Press Play, wait for scenery, then use the dashboard's **Plan a Route**.
5. For real bicycle directions, add your OpenRouteService key to the secrets file. Enter both Calgary addresses with the keyboard, search each, then calculate. The app follows the returned route continuously; it does not instantly jump between route waypoints. Without that key, use the included short demonstration route.

This is laptop-rendered PCVR, distinct from installing the standalone Android APK. It still needs Windows, the Link runtime and network access. This environment is Linux, so I cannot run or certify the Windows Editor/Quest Link session here.

## 8. Performance and comfort validation

The current defaults are a starting point: screen-space error 24, 128 MiB Cesium cache setting per tileset, four simultaneous tile loads, parent tiles preloaded to reduce holes during detail changes, no sibling preloads or tile collision meshes, 1.5 km camera far clip, 2× MSAA and render scale 0.85. The cache setting does **not** cap total application memory: visible tiles, textures, native allocations, UI, driver resources and the runtime need additional memory.

Use MQDH/OVR Metrics and Unity's device profiler for a full ride plus at least ten minutes of repeated rides. Measure CPU/GPU frame time, dropped frames, total memory and thermal behavior. At 72 Hz the frame budget is approximately **13.9 ms**; do not advertise 72 Hz unless sustained on the actual Quest 2. This code does not force the runtime's display refresh rate. Measure the selected runtime rate and meet its budget.

If GPU time is too high, try screen-space error 32 or 48, render scale 0.75, a shorter far clip, and fewer expensive views; compare visual readability. If streaming or memory stalls dominate, shorten the route, reduce concurrency or cache settings carefully, and use reliable Wi-Fi. Lower cache can also increase churn. Change one setting at a time and record the result.

Route sampling and deck generation happen at startup. Per-frame movement reuses route arrays; UI/streaming text updates are throttled. No scenery physics mesh generation or per-frame object spawning occurs. The credit RenderTexture consumes a few MiB plus UI resources. Check GC allocation in the real profiler; third-party packages may still allocate.

Keep speed low for a first-time rider, let the judge sit, and explain that a button pauses movement immediately. Keep tracking active during pauses and loading. Do not add camera bob, roll, motion blur or camera smoothing that delays the headset pose. The avatar has no artificial pedaling mode. If performance cannot stay comfortable, use the working desktop demonstration while continuing device optimization; do not describe it as a validated standalone VR result.

## 9. Troubleshooting

| Symptom | What to inspect / action |
|---|---|
| No Calgary Cycling menu | Wait for imports; resolve the first Console compile error, confirm exact editor/package pins. |
| Cesium native library / Reinterop missing | Install the registry package or release tarball, not source code. Confirm Android ARM64 and correct platform binaries. |
| Package unavailable | Check registry/proxy access. URP 17 packages are supplied with the Unity 6 editor family; reopen in the pinned editor. Do not downgrade Shader Graph to Cesium's older minimum. |
| Meta Core error on Linux | Use the documented Windows/macOS editor path; see the known Meta 77 issue in Sources. |
| Missing credential | Check `cesiumIonAccessToken` in `cycling-secrets.json`, persistentDataPath, and whether `CALGARY_CESIUM_ION_TOKEN` overrides the file. Retry. |
| HTTP 401/403 | Check the Cesium ion token, its asset permissions, and the selected terrain/imagery asset IDs. |
| HTTP 429 | Check Cesium ion account limits and reduce repeated retries/concurrency. |
| Loading stuck / blank view | Network, timeout, coverage, altitude, graphics materials and far clip. A root tile response alone is not sufficient. |
| Route appears buried or floating | Ellipsoid-vs-sea-level height mismatch or estimated route. Correct settings using independent height/alignment data; do not use streamed mesh extraction. |
| Pink procedural bicycle | URP assigned in Graphics and every Quality level? Unlit shader included in build? Re-run generator and inspect shader settings. |
| Pink/black streamed tiles | Confirm Cesium URP material resources, URP/Shader Graph versions and graphics backend; isolate with a desktop frame capture. |
| Controller steering missing | Android OpenXR loader, Oculus Touch profile, focus and left/right setting. Input Debugger should show thumbstick on a Touch device. |
| Keyboard missing | Click Game view; Active Input Handling must be Input System Package (New). |
| Gaze cannot click | Use right trigger, not grip; controller must be tracked/active. Gaze target must be in the menu plane. Confirm only one EventSystem. |
| Head tracking wrong/double | Only one Main Camera, one XROrigin and one TrackedPoseDriver. Remove other imported camera rigs. Recenter while seated. |
| Credits missing in headset | Verify CesiumCreditSystem UIDocument, cloned panel target texture, active Map Attribution canvas and materials; do not present until fixed. |
| Menu beneath credits / text clipped | Adjust canvas placement and credit texture/panel size; verify headset and desktop aspect ratios. |
| `adb unauthorized` | Wear headset and approve USB debugging. Reconnect cable; revoke old authorizations if necessary. |
| No device in ADB | Data cable, Developer Mode, USB port, Windows Meta driver; MQDH active status. |
| Gradle / SDK / NDK errors | Unity Hub Android modules, External Tools paths and the FIRST build error. Do not swap toolchains at random. |
| APK install signature mismatch | Use same signing key or deliberately uninstall/reinstall and re-provision credentials. |
| Ride resumes after focus loss | It should not: app pauses and requires explicit resume. If this fails, block presentation and debug focus callbacks. |

## 10. Live judge checklist

- [ ] Packages resolve and Unity compiles with no errors; keep the resulting lockfile.
- [ ] Run Core Checks reports PASS.
- [ ] Missing-key, malformed-file, wrong-key and timeout paths show actionable errors.
- [ ] Retry recovers after correcting credentials/network, without auto-moving.
- [ ] Landmark is recognizable; route height and all points are reviewed; explain the authored path.
- [ ] Start advances automatically; no pedaling selection exists.
- [ ] Left and right input visibly change bicycle position, including in third-person desktop inspection.
- [ ] Holding either direction stays within the corridor and does not spin/fall.
- [ ] Dead zone suppresses drift; speed and sensitivity controls work.
- [ ] Pause, resume, restart, menu and quit work from mouse and Quest controls.
- [ ] Destination stops motion; restart resets progress and lateral position.
- [ ] Physical head tracking is unaffected by menu or pause; recenter is comfortable.
- [ ] Cesium live provider credits appear fully in BOTH eyes and the desktop view.
- [ ] Wi-Fi loss and app focus loss stop the ride safely; reconnect/retry behavior is tested.
- [ ] Full route and repeated rides sustain the selected headset refresh rate without memory/thermal failure.
- [ ] Final APK is installed and tested after any profiling/build-setting changes.
- [ ] Venue connection and Cesium ion access are checked; headset/controllers are charged; USB cable and desktop backup are ready.

## 11. Architecture and full file contents

See **Docs/FILE_GUIDE.md** for file roles, Inspector setup and tests. The executable source of truth is under `Assets/Cycling`; `Docs/COMPLETE_SOURCE.md` is a historical appendix and may not match the current scripts. Do not copy its code over the executable files. Generated Unity scenes/assets are created by the included editor command, rather than hand-authored fragile YAML.

## 12. Sources and version verification

Consulted 24 October 2026. The implementation targets the pinned release source; current reference documentation can describe newer APIs.

- [Unity editor 6000.0.66f2 release](https://unity.com/releases/editor/whats-new/6000.0.66f2).
- [Cesium 1.18.1 release](https://github.com/CesiumGS/cesium-unity/releases/tag/v1.18.1) and [package metadata](https://github.com/CesiumGS/cesium-unity/blob/v1.18.1/package.json).
- [Cesium installation registry](https://cesium.com/learn/unity/unity-quickstart/).
- [Pinned Cesium3DTileset source](https://github.com/CesiumGS/cesium-unity/blob/v1.18.1/Runtime/Cesium3DTileset.cs): ComputeLoadProgress, tile-created event and static load failure callback.
- [Pinned credit source](https://github.com/CesiumGS/cesium-unity/blob/v1.18.1/Runtime/CesiumCreditSystem.cs) and [UI implementation](https://github.com/CesiumGS/cesium-unity/blob/v1.18.1/Runtime/CesiumCreditSystemUI.cs).
- [Cesium georeference conventions](https://cesium.com/learn/cesium-unity/ref-doc/classCesiumForUnity_1_1CesiumGeoreference.html).
- [Cesium ion access tokens](https://cesium.com/learn/ion/ion-access-tokens/), [Cesium ion assets](https://cesium.com/learn/ion/ion-assets/), and [OpenRouteService API keys](https://openrouteservice.org/dev/#/signup).
- [Unity OpenXR 1.14 Touch controller profile](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/oculustouchcontrollerprofile.html) and [input](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/input.html).
- [Meta software/device setup](https://developers.meta.com/vr/documentation/unity/unity-software-setup/), [deployment](https://developers.meta.com/vr/documentation/unity/unity-deploy-and-test/), and [OpenXR compatibility](https://developers.meta.com/vr/documentation/unity/unity-and-openxr-compatibility/).
- [Meta Core 77 Linux issue](https://developers.meta.com/vr/feedback/vr/investigations/25168645556071591/).
- [Peace Bridge coordinate reference](https://en.wikipedia.org/wiki/Peace_Bridge_(Calgary)) and [bridge architect's project page](https://calatrava.com/projects/peace-bridge-calgary.html). Coordinate rounding is suitable for landmark placement, not surveying.
