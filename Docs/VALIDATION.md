# Validation record

Prepared 24 October 2026.

## Completed in this environment

- Read the user's full specification.
- Inspected pinned Cesium 1.18.1 source for tileset events, loading progress, failure details, georeference and UI Toolkit credit implementation.
- Inspected Input System 1.11.2 TrackedPoseDriver and XR Core Utilities 2.3.0 XROrigin public properties.
- Checked package registry metadata for Cesium 1.18.1, Meta XR Core 77.0.0, OpenXR 1.14.0 and Input System 1.11.2. URP package family checked against Unity documentation/source; recent URP packages are editor-family dependent.
- Parsed all 11 C# files with the tree-sitter C# grammar: no syntax errors or missing nodes.
- Parsed manifest JSON and checked the archive for integrity.
- Reviewed lifecycle cleanup, credential omission, failure callback filtering, actual lateral steering, endpoint handling and generated references in source.

## Not executed here

- Unity import or package resolution inside the editor.
- C# semantic compilation or IL2CPP/Gradle build.
- The provided in-editor CoreChecks command.
- Real Google tile access, credential restrictions or coverage/alignment.
- RenderTexture attribution appearance and gaze interaction in a headset.
- Controller input, XR recenter, Android installation or Quest performance.

There is no APK, Unity screenshot, frame-rate measurement or verified on-device demo included. The default local route and ellipsoid height are approximate authored inputs, not surveyed path data. Complete README's checklist before treating the application as demonstration-ready.
