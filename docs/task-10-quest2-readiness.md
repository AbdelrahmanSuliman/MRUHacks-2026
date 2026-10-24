# Task 10 — Proposed Quest 2 development readiness report

Evidence collected 2026-10-24 on an Apple Silicon Mac. Target: Meta Quest 2. This is a host-tool and deployment-procedure assessment, not a Unity build or headset acceptance result. Status terms: **VERIFIED** means directly observed locally; **NOT VERIFIED** means evidence was not obtained; **NOT AVAILABLE** means the inspected location or ADB listing did not contain it; **REQUIRES UNITY PROJECT** means the test cannot run against this repository yet.

## Readiness decision

**Host toolchain: GO for preparing a future Android build. End-to-end Quest deployment: BLOCKED.** Unity 6000.0.66f2 and its Android toolchain execute locally. ADB currently lists no device. The repository contains no Unity project, so no package resolution, XR validation, APK build, controller test, or on-device performance result can be asserted. No Unity project, packages, settings, or credentials were created or changed.

## Evidence and readiness matrix

Repository root: `/Users/adam47/Documents/GitHub/MRUHacks-2026`; branch `main`; HEAD `5aadfb7e050a991d0430df6e29745b8545018fbe`. Before this report, `git status --porcelain=v1 --untracked-files=all` returned no lines. The only repository files were `README.md` and the three files under `prototypes/p4-p8-controls/`; `docs/` did not exist. No `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, Unity scene, or input-action file was found. The P4/P8 core exists in the repository, but it is not evidence of a Unity/Quest integration.

| Item | Status | Direct result or required evidence |
| --- | --- | --- |
| Host and pinned Unity Editor | VERIFIED | `uname -m` → `arm64`; Unity executable is Mach-O arm64; `Unity -version -batchmode -nographics -quit` → `6000.0.66f2` (exit 0). |
| Other installed Editor | VERIFIED | `/Applications/Unity/Hub/Editor/6000.6.7f1` exists; it is not the pinned build target. |
| Android Build Support | VERIFIED | `6000.0.66f2/PlaybackEngines/AndroidPlayer` contains SDK, NDK, OpenJDK and IL2CPP tooling. Installation does not prove project compilation. |
| Bundled Android SDK | VERIFIED | Platforms API 34, 35, 36; Build Tools 36.0.0; command-line tools 16.0; Platform Tools 36.0.0. Unity 6.0's documented development baseline is API 35. |
| Bundled NDK and JDK | VERIFIED | NDK `27.2.12479018` (`r27c`); Temurin OpenJDK `17.0.9+9` executes. The JDK binary is x86_64 on this arm64 host; execution succeeded, but an actual Gradle build remains untested. |
| Bundled ADB | VERIFIED | ADB `1.0.41`, Platform Tools `36.0.0-13206524`; universal arm64/x86_64 binary. |
| Meta Quest Developer Hub | NOT AVAILABLE | No matching app found under `/Applications` at search depth 4; `/Users/adam47/Applications` does not exist. Installation elsewhere is not ruled out. MQDH is optional for the manual ADB workflow. |
| Unity command-line build capability | VERIFIED | The pinned Editor executable accepts `-version -batchmode -nographics -quit`; Unity documents `-projectPath`, `-buildTarget` and `-build`/`-executeMethod` for a project build. No build command was run without a project. |
| Quest 2 ADB transport | NOT AVAILABLE | `adb devices -l` exited 0 and printed only `List of devices attached`. This means no ADB-visible device at the time of inspection; it does **not** prove the physical headset is absent. |
| Device developer mode / USB authorization / exact model | NOT VERIFIED | Requires a connected headset and in-headset approval. No serial or model was obtained. |
| Unity project, package manifest and XR settings | REQUIRES UNITY PROJECT | No Unity project is present in this repository. Proposed package versions are not installed or compatibility-tested here. |
| APK build, install, tracking, controls and performance | REQUIRES UNITY PROJECT | Also requires an ADB-authorized Quest 2 for the real-device tests. |

### Commands and observed outcomes

Set `TASK10_EDITOR_DIR=/Applications/Unity/Hub/Editor/6000.0.66f2` and `TASK10_ANDROID_DIR="$TASK10_EDITOR_DIR/PlaybackEngines/AndroidPlayer"` for the paths below. These variables are only shorthand for the inspected absolute paths.

| Inspection command | Outcome |
| --- | --- |
| `"$TASK10_EDITOR_DIR/Unity.app/Contents/MacOS/Unity" -version -batchmode -nographics -quit -logFile /private/tmp/task10-unity-version.log` | Exit 0; `6000.0.66f2`. No project opened. |
| `file "$TASK10_EDITOR_DIR/Unity.app/Contents/MacOS/Unity" "$TASK10_ANDROID_DIR/OpenJDK/bin/java" "$TASK10_ANDROID_DIR/SDK/platform-tools/adb"` | Unity arm64; Java x86_64; ADB universal arm64/x86_64. |
| `"$TASK10_ANDROID_DIR/OpenJDK/bin/java" -version` | Exit 0; OpenJDK `17.0.9`, Temurin `17.0.9+9`. |
| `cat "$TASK10_ANDROID_DIR/NDK/source.properties"` | `Pkg.Revision = 27.2.12479018`; `Pkg.ReleaseName = r27c`. |
| `find "$TASK10_ANDROID_DIR/SDK/platforms" -maxdepth 2 -name source.properties -exec rg 'Pkg.Revision|AndroidVersion.ApiLevel' {} +` | API levels 34, 35 and 36 are installed. |
| `env JAVA_HOME="$TASK10_ANDROID_DIR/OpenJDK" "$TASK10_ANDROID_DIR/SDK/cmdline-tools/16.0/bin/sdkmanager" --version` | Exit 0; `16.0`. Without `JAVA_HOME`, this command exited 1 because it could not locate a Java runtime; the bundled JDK resolves it. |
| `"$TASK10_ANDROID_DIR/SDK/build-tools/36.0.0/aapt2" version` | `Android Asset Packaging Tool (aapt) 2.20-13193326`. |
| `"$TASK10_ANDROID_DIR/SDK/platform-tools/adb" version` | Exit 0; ADB `1.0.41`, Platform Tools `36.0.0-13206524`. |
| `"$TASK10_ANDROID_DIR/SDK/platform-tools/adb" devices -l` | Exit 0; header only, no device rows. The initial sandboxed attempt could not bind the localhost ADB daemon (`Operation not permitted`); the permitted host-level retry succeeded. This sandbox failure is not evidence of a device fault. |
| `find /Applications -maxdepth 4 -iname '*quest*developer*hub*.app' -print` | Exit 0; no matches. `test -d /Users/adam47/Applications` exited 1. |

The Unity 6.0 [system requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html) identify Android SDK API 35, NDK r27c and OpenJDK 17 for development. Local file and executable checks above match those major baselines; no Android build was run. Unity's [command-line build guide](https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html) requires a project path and recommends batch mode, a log file and an explicit target/profile. The Editor executable is available, but a build command cannot be meaningfully exercised here.

## Confirmed guidance versus proposed project settings

All settings in this table are **PROPOSED checks for the future team project**, not existing project values. Apply them through the project's owner and inspect its manifest, lockfile and XR validation before any change.

| Future check | Official basis and proposed verification |
| --- | --- |
| Build target and scenes | In Unity 6000.0, use Android in **File > Build Profiles** if no Meta Quest profile is offered; include the intended startup scene. Build output is an APK. [Meta build guide](https://developers.meta.com/vr/documentation/unity/unity-build/). |
| XR provider and controller interaction | Under Android XR Plug-in Management, check OpenXR, **Meta Quest Support** and **Oculus Touch Controller Profile**. Inspect the team's Input System actions and actual bindings; an interaction profile alone does not wire bicycle controls. [Unity OpenXR 1.14 Quest setup](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/metaquest.html), [controller profile](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/oculustouchcontrollerprofile.html). |
| Scripting and CPU target | Check **IL2CPP** and **ARM64**, with ARMv7 disabled. Meta's publishing guidance and Cesium's Android requirements agree. [Meta settings](https://developers.meta.com/vr/documentation/unity/unity-prepare-for-publish/), [Cesium platforms](https://cesium.com/learn/cesium-unity/ref-doc/supported-platforms.html). |
| Android API and network | Meta states minimum API 29 and target API Automatic (highest installed) for publishing. This SDK contains API 36, so confirm the selected target and Quest OS compatibility in the actual build. If Cesium is used, check Internet Access = **Require**. [Meta settings](https://developers.meta.com/vr/documentation/unity/unity-prepare-for-publish/), [Cesium platforms](https://cesium.com/learn/cesium-unity/ref-doc/supported-platforms.html). |
| Graphics | Unity OpenXR 1.14 documents Meta Quest as Android arm64 with **Vulkan** as preferred API. Check Vulkan and render pipeline in the actual project; assess OpenGLES3 only through project validation and a real build/device test. Unity's automatic graphics selection tries Vulkan then GLES3. [OpenXR runtime table](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/index.html), [Android Player settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-PlayerSettingsAndroid.html). |
| XR validation | Run Android OpenXR **Project Validation**, resolve reported errors with project owner, then build. Check runtime logs and headset rendering; an Editor setting alone proves neither. [Unity OpenXR 1.14 validation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/index.html). |

### Package-version evidence and risk

The proposed `Cesium 1.18.1`, `OpenXR 1.14.0`, `Meta XR Core 77.0.0` combination is **NOT VERIFIED** as a resolved Unity dependency graph or an executable APK. Unity's [OpenXR 1.14.0 documentation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/index.html) explicitly lists Meta Quest Android arm64/Vulkan HMD and controller support. Cesium's [change log](https://cesium.com/learn/cesium-unity/ref-doc/changes.html) records 1.18.1, and its current [platform guide](https://cesium.com/learn/cesium-unity/ref-doc/supported-platforms.html) lists Android/Quest, but that current guide is not an exact-version integration test. Meta has a [Core SDK v77.0 release](https://developers.meta.com/vr/downloads/package/meta-xr-core-sdk/77.0/) and currently [recommends OpenXR 1.15.1 with Meta XR SDK v74+](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/); 1.14.0 is below that current recommendation. The v77 release notes flag Project Setup Tool fixes that may fail to serialize, so any such fixes require inspection in the actual project. No local `manifest.json`, `packages-lock.json`, imported Meta SDK, or Unity build log exists to establish the proposed three-way compatibility. When the project arrives, inspect actual package identities and versions, resolution errors, native libraries, OpenXR validation, and a Quest 2 APK run before accepting this combination. Do not silently upgrade it.

## Reproducible device connection and deployment checklist

1. Confirm access to a **Meta developer team and verified developer account**. Pair the Quest 2 in the Meta Horizon mobile app. On the app's headset page, open **Headset Settings > Developer Mode** and enable it. This account/device state was not inspected here. [Meta device setup](https://developers.meta.com/vr/documentation/native/android/mobile-device-setup/).
2. Power on and wear the Quest 2. Connect it to the Mac with a known **USB-C data cable**. In headset Developer settings, enable the MTP notification if needed and approve the **Allow USB debugging** prompt for this computer. Mac does not need Meta's Windows-only Oculus ADB driver. [Meta device setup](https://developers.meta.com/vr/documentation/native/android/mobile-device-setup/).
3. In a terminal, set `TASK10_ADB=/Applications/Unity/Hub/Editor/6000.0.66f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`; run `"$TASK10_ADB" devices -l`. Expected: one line ending in `device`. If several devices appear, use `"$TASK10_ADB" -s <serial> ...` for subsequent commands. Run `"$TASK10_ADB" shell getprop ro.product.model` and confirm the connected device is the intended Quest 2. Avoid publishing the serial.
4. Once the team project exists, open it with **Unity 6000.0.66f2**. Inspect its Editor version, package manifest/lockfile, Android settings, scene list, XR provider/features/interaction profile, input actions and OpenXR Project Validation. Do not assume the alternate installed Editor is equivalent. [Unity Quest workflow](https://docs.unity3d.com/6000.0/Documentation/Manual/xr-meta-quest-develop.html), [Meta build guide](https://developers.meta.com/vr/documentation/unity/unity-build/).
5. In **File > Build Profiles**, select Android (or Meta Quest if available), choose the intended scene and connected Run Device, enable **Development Build** for diagnostics, then **Build and Run** to an APK output path. Expected: Unity completes without compile/Gradle/XR validation errors, ADB installs the APK and it launches inside the Quest 2. A **Build** without device only establishes APK generation. Meta Link is Windows-only, so it cannot substitute for standalone Android deployment on this Mac. [Meta build guide](https://developers.meta.com/vr/documentation/unity/unity-build/).
6. For an already produced APK, `"$TASK10_ADB" install -r /absolute/path/to/build.apk`; expected terminal result `Success`. Launch it from the headset's app library/Unknown Sources as applicable. Capture `"$TASK10_ADB" logcat -d` or filtered Unity/Android logs if startup fails. `adb install -r` does not fix a signing-key mismatch. [Meta ADB guide](https://developers.meta.com/vr/documentation/native/android/ts-adb/), [Meta quick start](https://developers.meta.com/vr/essentials/quick-start/).

### ADB state interpretation and diagnostics

| `adb devices -l` result | Meaning and next check |
| --- | --- |
| Header only, no rows (observed) | No ADB transport visible. Check power, data-capable cable, USB port, developer mode and in-headset prompt; retry with the bundled ADB. It does not establish whether a physical headset is nearby. |
| `unauthorized` | USB transport is visible but host authorization is pending/denied. Wear headset, accept the USB-debugging prompt, reconnect and recheck. |
| `device` | ADB authorization is active; confirm model with `getprop`, then attempt the build/install. This alone does not prove the app runs. |
| `offline` | Transport exists but is not ready; reconnect the cable/headset, then retry. If persistent, collect ADB and device logs before changing anything. |
| Multiple rows | Select the intended Quest by serial with `adb -s`; do not deploy to an arbitrary device. |

If Unity fails before APK creation, retain the Editor and Gradle logs and check exact Editor/package resolution, SDK/NDK/JDK paths, Android API level and OpenXR validation. If install reports `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, inspect package identity and signing ownership; do not automatically uninstall team data. If the app opens flat, black or without controller input, inspect Android OpenXR loader/features, Oculus Touch profile, input bindings and runtime `logcat`. If tiles fail, check network permission and runtime requests. These are **diagnostic hypotheses**, not observed failures.

## Real Quest 2 acceptance still required

On a directly observed Quest 2: record headset firmware/OS, APK/package version, project commit, actual package versions, graphics API and refresh rate. Verify stereo XR startup and tracked head pose; both controllers report tracking and intended thumbstick bindings; P4/P8 steering and pause/restart/route-mask behaviour are integrated without moving the tracked camera directly; network tiles load; repeated launch and sleep/resume work. Profile on-device frame time and thermal behaviour during representative Calgary scenes with Unity/Meta tools. At 72 Hz the frame budget is about 13.9 ms, and at 90 Hz about 11.1 ms; these are arithmetic budgets, not measured performance. No such device, build, or profiling test occurred in this investigation.

## Official sources

All references were retrieved **2026-10-24**. Primary sources used: [Unity 6.0 system requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html); [Unity command-line build](https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html); [Unity OpenXR 1.14 package and validation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/index.html); [Unity OpenXR Quest setup](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/metaquest.html); [Unity Oculus Touch profile](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/oculustouchcontrollerprofile.html); [Unity Android Player settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-PlayerSettingsAndroid.html); [Meta device setup](https://developers.meta.com/vr/documentation/native/android/mobile-device-setup/); [Meta XR provider guidance](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/); [Meta Core SDK v77](https://developers.meta.com/vr/downloads/package/meta-xr-core-sdk/77.0/); [Meta build guide](https://developers.meta.com/vr/documentation/unity/unity-build/); [Meta publishing settings](https://developers.meta.com/vr/documentation/unity/unity-prepare-for-publish/); [Meta ADB guide](https://developers.meta.com/vr/documentation/native/android/ts-adb/); [Cesium supported platforms](https://cesium.com/learn/cesium-unity/ref-doc/supported-platforms.html); [Cesium change log](https://cesium.com/learn/cesium-unity/ref-doc/changes.html).
