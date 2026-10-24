using System;

namespace MrU.Hacks.Controls
{
    public enum SteeringSource { Thumbstick, TwoHand }
    public enum SteeringStatus
    {
        Inactive, AwaitingSourceSample, Valid, InvalidThumbstick, TrackingLost, InvalidControllerPosition,
        InsufficientSeparation, ReversedHandOrder
    }
    public enum RideState { Stopped, Starting, Moving, Paused, Restarting, Transitioning }

    /// <summary>Bicycle-root local metres: +X right, +Y up, +Z forward. Never camera-local.</summary>
    public struct BikeLocalPoint
    {
        public readonly double X, Y, Z;
        public BikeLocalPoint(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Fresh input snapshot; Step reads only the selected steering source.</summary>
    public struct SteeringSample
    {
        public readonly double Thumbstick;
        public readonly BikeLocalPoint LeftHand, RightHand;
        public readonly bool LeftTracked, RightTracked;
        public SteeringSample(double thumbstick, BikeLocalPoint leftHand, BikeLocalPoint rightHand,
            bool leftTracked, bool rightTracked)
        {
            Thumbstick = thumbstick; LeftHand = leftHand; RightHand = rightHand;
            LeftTracked = leftTracked; RightTracked = rightTracked;
        }
    }

    /// <summary>Immutable settings. The integration owner chooses and validates demo values on device.</summary>
    public sealed class RideSettings
    {
        public readonly double SteeringDeadzone, MaxHandlebarAngleDegrees;
        public readonly double MinimumHandSeparationMeters, SteeringResponseSeconds;
        public readonly double MaxSteeringChangePerSecond, CruiseMetersPerSecond;
        public readonly double PositiveAccelerationMetersPerSecondSquared;

        public RideSettings(double deadzone, double maxHandlebarAngleDegrees,
            double minimumHandSeparationMeters, double steeringResponseSeconds,
            double maxSteeringChangePerSecond, double cruiseMetersPerSecond,
            double positiveAccelerationMetersPerSecondSquared)
        {
            if (!Finite(deadzone) || deadzone < 0 || deadzone >= 1)
                throw new ArgumentOutOfRangeException(nameof(deadzone));
            if (!Finite(maxHandlebarAngleDegrees) || maxHandlebarAngleDegrees <= 0 || maxHandlebarAngleDegrees > 90)
                throw new ArgumentOutOfRangeException(nameof(maxHandlebarAngleDegrees));
            if (!Finite(minimumHandSeparationMeters) || minimumHandSeparationMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumHandSeparationMeters));
            if (!Finite(steeringResponseSeconds) || steeringResponseSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(steeringResponseSeconds));
            if (!Finite(maxSteeringChangePerSecond) || maxSteeringChangePerSecond <= 0 ||
                !Finite(maxSteeringChangePerSecond * steeringResponseSeconds))
                throw new ArgumentOutOfRangeException(nameof(maxSteeringChangePerSecond));
            if (!Finite(cruiseMetersPerSecond) || cruiseMetersPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(cruiseMetersPerSecond));
            if (!Finite(positiveAccelerationMetersPerSecondSquared) ||
                positiveAccelerationMetersPerSecondSquared <= 0)
                throw new ArgumentOutOfRangeException(nameof(positiveAccelerationMetersPerSecondSquared));

            SteeringDeadzone = deadzone;
            MaxHandlebarAngleDegrees = maxHandlebarAngleDegrees;
            MinimumHandSeparationMeters = minimumHandSeparationMeters;
            SteeringResponseSeconds = steeringResponseSeconds;
            MaxSteeringChangePerSecond = maxSteeringChangePerSecond;
            CruiseMetersPerSecond = cruiseMetersPerSecond;
            PositiveAccelerationMetersPerSecondSquared = positiveAccelerationMetersPerSecondSquared;
        }

        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }

    /// <summary>Desired artificial motion only; no transform, XR, input, or rendering side effects.</summary>
    public struct RideCommand
    {
        public readonly RideState State;
        public readonly SteeringSource SelectedSteeringSource;
        public readonly SteeringStatus SteeringStatus;
        public readonly double NormalizedSteering, SpeedMetersPerSecond;
        public readonly bool RequiresOpaqueMask, MayReposition;
        public bool SteeringIsValid { get { return SteeringStatus == SteeringStatus.Valid; } }

        internal RideCommand(RideState state, SteeringSource source, SteeringStatus status,
            double steering, double speed, bool requiresMask, bool mayReposition)
        {
            State = state; SelectedSteeringSource = source; SteeringStatus = status;
            NormalizedSteering = steering; SpeedMetersPerSecond = speed;
            RequiresOpaqueMask = requiresMask; MayReposition = mayReposition;
        }
    }

    /// <summary>
    /// Pure, single-threaded control state machine. Positive steering turns right.
    /// Invalid selected input cancels both commands and pauses; that safety stop
    /// intentionally overrides the normal steering-rate bound.
    /// </summary>
    public sealed class RideCore
    {
        private readonly RideSettings _settings;
        private RideState _state = RideState.Stopped;
        private SteeringSource _source = SteeringSource.Thumbstick;
        private SteeringStatus _status = SteeringStatus.Inactive;
        private double _steering, _speed;
        private bool _maskOpaque, _repositioned;

        public RideCore(RideSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _settings = settings;
        }

        public SteeringSource SelectedSteeringSource { get { return _source; } }
        public RideCommand Command
        {
            get
            {
                bool transitioning = _state == RideState.Transitioning;
                return new RideCommand(_state, _source, _status, _steering, _speed,
                    transitioning, transitioning && _maskOpaque && !_repositioned);
            }
        }

        /// <summary>Changes the next target without resetting the current filtered command.</summary>
        public void SelectSteeringSource(SteeringSource source)
        {
            if (source != SteeringSource.Thumbstick && source != SteeringSource.TwoHand)
                throw new ArgumentOutOfRangeException(nameof(source));
            if (source == _source) return;
            _source = source;
            if (_state == RideState.Starting || _state == RideState.Moving)
                _status = SteeringStatus.AwaitingSourceSample;
        }

        public RideCommand Start()
        {
            if (_state != RideState.Stopped && _state != RideState.Paused)
                throw new InvalidOperationException("Start requires Stopped or Paused.");
            _state = RideState.Starting;
            ClearMovement();
            return Command;
        }

        /// <summary>Emits a zero-speed command synchronously, before another Step.</summary>
        public RideCommand Pause()
        {
            if (_state == RideState.Transitioning || _state == RideState.Restarting)
                return Command; // Both already hold zero; preserve their acknowledgement gates.
            _state = RideState.Paused;
            ClearMovement();
            return Command;
        }

        public RideCommand Restart()
        {
            if (_state == RideState.Transitioning)
                throw new InvalidOperationException("Finish the masked transition before restarting.");
            _state = RideState.Restarting;
            ClearMovement();
            return Command;
        }

        /// <summary>Call only after the existing pose owner reports a known stationary pose.</summary>
        public RideCommand AcknowledgeStationaryPose()
        {
            if (_state != RideState.Restarting)
                throw new InvalidOperationException("No restart pose is pending.");
            _state = RideState.Stopped;
            return Command;
        }

        public RideCommand BeginTransition()
        {
            if (_state == RideState.Restarting || _state == RideState.Transitioning)
                throw new InvalidOperationException("A pose operation is already pending.");
            _state = RideState.Transitioning;
            _maskOpaque = false;
            _repositioned = false;
            ClearMovement();
            return Command;
        }

        /// <summary>Call after rendering confirms a fully opaque frame, not after requesting a fade.</summary>
        public RideCommand ConfirmMaskOpaque()
        {
            if (_state != RideState.Transitioning)
                throw new InvalidOperationException("No route transition is pending.");
            _maskOpaque = true;
            return Command;
        }

        /// <summary>Call after the existing owner has repositioned and the new route is stable under the mask.</summary>
        public RideCommand AcknowledgeRepositioned()
        {
            if (_state != RideState.Transitioning || !_maskOpaque)
                throw new InvalidOperationException("Repositioning requires an opaque mask.");
            _repositioned = true;
            return Command;
        }

        public RideCommand FinishTransition()
        {
            if (_state != RideState.Transitioning || !_maskOpaque || !_repositioned)
                throw new InvalidOperationException("Wait for masked repositioning before revealing the route.");
            _state = RideState.Stopped;
            _maskOpaque = false;
            _repositioned = false;
            return Command;
        }

        /// <summary>Advances commands using finite, nonnegative elapsed seconds and a fresh input snapshot.</summary>
        public RideCommand Step(SteeringSample sample, double elapsedSeconds)
        {
            if (!RideSettings.Finite(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (_state != RideState.Starting && _state != RideState.Moving)
                return Command;

            double target;
            SteeringStatus status = ReadTarget(sample, out target);
            if (status != SteeringStatus.Valid)
            {
                _state = RideState.Paused;
                ClearMovement();
                _status = status;
                return Command;
            }

            _status = SteeringStatus.Valid;
            _steering = FilterSteering(_steering, target, elapsedSeconds);
            double secondsToCruise = (_settings.CruiseMetersPerSecond - _speed) /
                _settings.PositiveAccelerationMetersPerSecondSquared;
            if (elapsedSeconds >= secondsToCruise)
            {
                _speed = _settings.CruiseMetersPerSecond;
                _state = RideState.Moving;
            }
            else _speed += _settings.PositiveAccelerationMetersPerSecondSquared * elapsedSeconds;
            return Command;
        }

        private void ClearMovement()
        {
            _speed = 0;
            _steering = 0;
            _status = SteeringStatus.Inactive;
        }

        private SteeringStatus ReadTarget(SteeringSample sample, out double target)
        {
            target = 0;
            double raw;
            if (_source == SteeringSource.Thumbstick)
            {
                if (!RideSettings.Finite(sample.Thumbstick)) return SteeringStatus.InvalidThumbstick;
                raw = Clamp(sample.Thumbstick, -1, 1);
            }
            else
            {
                if (!sample.LeftTracked || !sample.RightTracked) return SteeringStatus.TrackingLost;
                if (!FinitePoint(sample.LeftHand) || !FinitePoint(sample.RightHand))
                    return SteeringStatus.InvalidControllerPosition;
                double acrossX = sample.RightHand.X - sample.LeftHand.X;
                double acrossZ = sample.RightHand.Z - sample.LeftHand.Z;
                if (!RideSettings.Finite(acrossX) || !RideSettings.Finite(acrossZ))
                    return SteeringStatus.InvalidControllerPosition;
                if (acrossX < 0) return SteeringStatus.ReversedHandOrder;
                if (acrossX < _settings.MinimumHandSeparationMeters)
                    return SteeringStatus.InsufficientSeparation;
                // Right grip toward -Z gives positive steering in bicycle-local XZ.
                double degrees = Math.Atan2(-acrossZ, acrossX) * (180.0 / Math.PI);
                raw = Clamp(degrees / _settings.MaxHandlebarAngleDegrees, -1, 1);
            }
            double magnitude = Math.Abs(raw);
            if (magnitude <= _settings.SteeringDeadzone) return SteeringStatus.Valid;
            target = Math.Sign(raw) * (magnitude - _settings.SteeringDeadzone) /
                (1.0 - _settings.SteeringDeadzone);
            return SteeringStatus.Valid;
        }

        private double FilterSteering(double current, double target, double elapsedSeconds)
        {
            if (elapsedSeconds == 0 || current == target) return current;
            double difference = target - current;
            double distance = Math.Abs(difference);
            double direction = Math.Sign(difference);
            double rate = _settings.MaxSteeringChangePerSecond;
            double response = _settings.SteeringResponseSeconds;
            double result;
            if (response == 0)
                result = elapsedSeconds >= distance / rate ? target : current + direction * rate * elapsedSeconds;
            else
            {
                // Exact solution of ds/dt = sign(target-s) * min(rate, |target-s|/response).
                double linearBand = rate * response;
                if (distance > linearBand)
                {
                    double linearSeconds = (distance - linearBand) / rate;
                    result = elapsedSeconds <= linearSeconds
                        ? current + direction * rate * elapsedSeconds
                        : target - direction * linearBand *
                            Math.Exp(-(elapsedSeconds - linearSeconds) / response);
                }
                else result = target - difference * Math.Exp(-elapsedSeconds / response);
            }
            return Clamp(result, -1, 1);
        }

        private static bool FinitePoint(BikeLocalPoint point)
        {
            return RideSettings.Finite(point.X) && RideSettings.Finite(point.Y) &&
                RideSettings.Finite(point.Z);
        }

        private static double Clamp(double value, double low, double high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }
}
