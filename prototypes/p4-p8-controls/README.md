# P4/P8 isolated control core

This directory contains a pure C# steering and comfort command core. It has no
`UnityEngine`, XR, Input System, scene, or rendering dependency. It does not
move any transform. The provisional demo steering source is the thumbstick;
two-hand steering requires Quest calibration before use.

## Public contract

- `RideSettings` takes steering deadzone `[0,1)`, maximum handlebar angle
  `(0,90]` degrees, minimum horizontal hand separation in metres, steering
  response in seconds, maximum normalized steering change per second, cruise
  speed in m/s, and positive acceleration in m/s². All values must be finite;
  separation, rate, cruise, and acceleration must be positive. There are no
  baked-in demo speed, ramp, or deadzone values.
- `BikeLocalPoint` uses a stable bicycle-root frame: +X right, +Y up, +Z
  forward. Both controller positions must be converted into that frame by the
  existing Unity input adapter. No camera-relative position is accepted.
- `SteeringSample` contains one thumbstick value, both hand positions, and
  both tracking flags. `RideCore.SelectSteeringSource` selects which source
  `Step` reads. A source change preserves the filtered steering command;
  `AwaitingSourceSample` indicates that a fresh selected-source sample is
  needed. A nonfinite selected input, lost tracking, reversed hands, or
  insufficient horizontal hand separation pauses the ride and commands zero.
- Two-hand right steering is positive: for the projected right-minus-left
  vector `(dx,dz)`, angle is `atan2(-dz,dx)` in degrees. The signed angle
  divided by the configured limit is clamped to `[-1,1]`; deadzone remaps
  the remaining range to `[-1,1]`.
- Steering obeys the continuous-time law
  `ds/dt = sign(target-s) min(maxRate, abs(target-s)/responseSeconds)`.
  With zero response, it uses the rate limit alone. Valid-to-valid source
  changes obey the same law. Safety stops intentionally override the rate
  bound to command zero immediately.
- `RideCore.Command` and every state method return a `RideCommand` with
  state, source, steering status, normalized steering, speed in m/s, and
  transition-mask gates. Treat `SteeringIsValid == false` as a reason not
  to consume held steering during a source change.

## State transitions

| Current state | Operation | Next state and command |
| --- | --- | --- |
| Stopped, Paused | `Start()` | Starting, zero speed and steering |
| Starting | `Step(sample, seconds)` | Bounded acceleration; Moving at cruise |
| Starting, Moving | `Pause()` | Paused, zero speed and steering immediately |
| Any except Transitioning | `Restart()` | Restarting, zero commands |
| Restarting | `AcknowledgeStationaryPose()` | Stopped |
| Any except Restarting/Transitioning | `BeginTransition()` | Transitioning, zero commands, opaque mask requested |
| Transitioning | `ConfirmMaskOpaque()` | `MayReposition=true` |
| Transitioning with confirmed mask | `AcknowledgeRepositioned()` | Holds mask and zero commands |
| Transitioning after reposition acknowledgement | `FinishTransition()` | Stopped |

Invalid selected steering input enters Paused and preserves its diagnostic
`SteeringStatus`. `Pause()` during Restarting or Transitioning keeps those
gates intact and holds zero movement. Invalid state transitions throw
`InvalidOperationException`; invalid settings or elapsed time throw
`ArgumentOutOfRangeException`.
`MayReposition` is consumed by `AcknowledgeRepositioned()` while the
opaque-mask requirement remains active until `FinishTransition()`.

## Independent test run

The test file is a dependency-free console test runner. On a machine with
`mcs` and `mono` on `PATH`, from the repository root:

```sh
mcs -warnaserror -out:/tmp/p4-p8-tests.exe prototypes/p4-p8-controls/RideCore.cs prototypes/p4-p8-controls/RideCoreTests.cs
mono /tmp/p4-p8-tests.exe
```

The implementation can also be compiled alone with
`mcs -target:library -warnaserror -out:/tmp/p4-p8-controls.dll prototypes/p4-p8-controls/RideCore.cs`.
Tests use independently calculated geometry, deadzone, smoothing, and
acceleration values, including 72 Hz and 90 Hz schedules.

## Unity integration boundary

Only the team's existing input bridge should sample Quest controls and convert
tracked positions into the bicycle-root frame. Only the existing bicycle/world
movement owner should consume `RideCommand`. Apply the synchronous zero
command returned by `Pause`, `Restart`, or `BeginTransition` immediately,
before another movement update. The existing renderer must confirm a fully
opaque frame before using `MayReposition`; acknowledge repositioning only
after the route and pose are stable. Neither this core nor a future adapter
may directly overwrite the tracked headset pose.

Unity compilation, actual input bindings, XR rig hierarchy, render masking,
and Quest 2 comfort and frame timing remain unverified until the team project
and device are available.
