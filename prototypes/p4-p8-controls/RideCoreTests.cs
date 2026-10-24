using System;
using MrU.Hacks.Controls;

internal static class RideCoreTests
{
    private static int _passed;
    private static int _failed;

    private static RideSettings Settings(double deadzone = 0.0, double response = 0.2,
        double steeringRate = 10.0, double cruise = 1.0, double acceleration = 0.5)
    {
        return new RideSettings(deadzone, 45.0, 0.1, response, steeringRate, cruise, acceleration);
    }

    private static SteeringSample Stick(double value)
    {
        return new SteeringSample(value, default(BikeLocalPoint), default(BikeLocalPoint), false, false);
    }

    private static SteeringSample Hands(BikeLocalPoint left, BikeLocalPoint right,
        bool leftTracked = true, bool rightTracked = true)
    {
        return new SteeringSample(0.0, left, right, leftTracked, rightTracked);
    }

    private static RideCore Riding(RideSettings settings)
    {
        var core = new RideCore(settings);
        core.Start();
        return core;
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    private static void Near(double expected, double actual, double tolerance, string message)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            throw new Exception(message + ": expected " + expected + " ± " + tolerance + ", got " + actual);
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception(message);
    }

    private static void Test(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void NeutralAndDefaultSource()
    {
        var core = Riding(Settings());
        Equal(SteeringSource.Thumbstick, core.SelectedSteeringSource, "demo source");
        var command = core.Step(Stick(0.0), 0.1);
        Near(0.0, command.NormalizedSteering, 0.0, "neutral steering");
        Equal(SteeringStatus.Valid, command.SteeringStatus, "valid neutral input");
    }

    private static void HandSignAndLimit()
    {
        var neutral = Riding(Settings(response: 0.0));
        neutral.SelectSteeringSource(SteeringSource.TwoHand);
        Near(0.0, neutral.Step(Hands(new BikeLocalPoint(-0.3, 0.1, 0.2),
            new BikeLocalPoint(0.3, 0.1, 0.2)), 1.0).NormalizedSteering,
            0.0, "neutral hands");

        var left = new BikeLocalPoint(-0.3, 0.0, 0.15);
        var right = new BikeLocalPoint(0.3, 0.0, -0.15);
        var positive = Riding(Settings(response: 0.0));
        positive.SelectSteeringSource(SteeringSource.TwoHand);
        Near(0.5903344706017332, positive.Step(Hands(left, right), 1.0).NormalizedSteering,
            1e-12, "right-positive signed angle");

        var negative = Riding(Settings(response: 0.0));
        negative.SelectSteeringSource(SteeringSource.TwoHand);
        Near(-0.5903344706017332,
            negative.Step(Hands(new BikeLocalPoint(-0.3, 0.0, -0.15),
                new BikeLocalPoint(0.3, 0.0, 0.15)), 1.0).NormalizedSteering,
            1e-12, "left-negative signed angle");

        var full = Riding(Settings(response: 0.0));
        full.SelectSteeringSource(SteeringSource.TwoHand);
        Near(1.0, full.Step(Hands(new BikeLocalPoint(-0.3, 0, 0.4),
            new BikeLocalPoint(0.3, 0, -0.4)), 1.0).NormalizedSteering,
            1e-12, "angle clamps to full steering");
    }

    private static void DeadzoneAndBounds()
    {
        var core = Riding(Settings(deadzone: 0.15, response: 0.0));
        Near(0.0, core.Step(Stick(0.15), 0.1).NormalizedSteering, 0.0, "deadzone boundary");
        Near(0.5, core.Step(Stick(0.575), 1.0).NormalizedSteering, 1e-12, "rescaled half range");
        Near(1.0, core.Step(Stick(3.0), 1.0).NormalizedSteering, 1e-12, "high input bounded");
        Near(-1.0, core.Step(Stick(-3.0), 1.0).NormalizedSteering, 1e-12, "low input bounded");
    }

    private static void InvalidInputCannotLeaveStaleSteering()
    {
        var core = Riding(Settings());
        core.Step(Stick(1.0), 1.0);
        var invalid = core.Step(Stick(double.NaN), 0.01);
        Equal(SteeringStatus.InvalidThumbstick, invalid.SteeringStatus, "NaN status");
        Near(0.0, invalid.NormalizedSteering, 0.0, "NaN clears command");
        Near(0.0, invalid.SpeedMetersPerSecond, 0.0, "NaN cancels movement");
        Equal(RideState.Paused, invalid.State, "invalid input pauses ride");
        Near(0.0, core.Step(Stick(0.0), 0.01).NormalizedSteering, 0.0, "no stale command");
        core.Start();
        Near(0.0, core.Step(Stick(double.PositiveInfinity), 0.01).NormalizedSteering,
            0.0, "infinite input rejected");
    }

    private static void InvalidHandsCannotLeaveStalePose()
    {
        var core = Riding(Settings());
        core.SelectSteeringSource(SteeringSource.TwoHand);
        var left = new BikeLocalPoint(-0.3, 0, 0.15);
        var right = new BikeLocalPoint(0.3, 0, -0.15);
        core.Step(Hands(left, right), 1.0);
        var lost = core.Step(Hands(left, right, false, true), 0.01);
        Equal(SteeringStatus.TrackingLost, lost.SteeringStatus, "tracking status");
        Near(0.0, lost.NormalizedSteering, 0.0, "tracking loss clears output");
        Near(0.0, lost.SpeedMetersPerSecond, 0.0, "tracking loss cancels movement");
        core.Start();
        Equal(SteeringStatus.InsufficientSeparation,
            core.Step(Hands(left, left), 0.01).SteeringStatus, "coincident hands");
        core.Start();
        Equal(SteeringStatus.ReversedHandOrder,
            core.Step(Hands(right, left), 0.01).SteeringStatus, "reversed hands");
        core.Start();
        Equal(SteeringStatus.InvalidControllerPosition,
            core.Step(Hands(left, new BikeLocalPoint(double.NaN, 0, 0)), 0.01).SteeringStatus,
            "nonfinite position");
        Near(0.0, core.Command.NormalizedSteering, 0.0, "invalid pose cannot steer");
    }

    private static void TimeConsistentSmoothing()
    {
        var one = Riding(Settings());
        var many = Riding(Settings());
        var oneCommand = one.Step(Stick(1.0), 0.1);
        for (int i = 0; i < 10; i++) many.Step(Stick(1.0), 0.01);
        Near(0.3934693402873666, oneCommand.NormalizedSteering, 1e-12,
            "independent exponential reference");
        Near(oneCommand.NormalizedSteering, many.Command.NormalizedSteering, 1e-12,
            "partition-independent smoothing");
    }

    private static void SourceChangeIsRateBounded()
    {
        var core = Riding(Settings(steeringRate: 0.5));
        core.Step(Stick(1.0), 0.2);
        var before = core.Command.NormalizedSteering;
        core.SelectSteeringSource(SteeringSource.TwoHand);
        Near(before, core.Command.NormalizedSteering, 0.0, "source selection does not jump");
        Equal(SteeringStatus.AwaitingSourceSample, core.Command.SteeringStatus,
            "new source is not valid before a fresh sample");
        True(!core.Command.SteeringIsValid, "pending source cannot claim valid input");
        var after = core.Step(Hands(new BikeLocalPoint(-0.3, 0, -0.4),
            new BikeLocalPoint(0.3, 0, 0.4)), 0.1);
        True(Math.Abs(after.NormalizedSteering - before) <= 0.050000000001,
            "source switch obeys 0.5 per second bound");
    }

    private static void RateLimitComposesAcrossTimesteps()
    {
        var once = Riding(Settings(steeringRate: 0.5));
        var many = Riding(Settings(steeringRate: 0.5));
        var oneCommand = once.Step(Stick(1.0), 2.0);
        for (int i = 0; i < 200; i++) many.Step(Stick(1.0), 0.01);
        Near(0.9632120558828557, oneCommand.NormalizedSteering, 1e-12,
            "linear segment then exponential tail");
        Near(oneCommand.NormalizedSteering, many.Command.NormalizedSteering, 1e-10,
            "rate-limited filter composes over time");
    }

    private static void StationaryStartAndAcceleration()
    {
        var core = new RideCore(Settings());
        Equal(RideState.Stopped, core.Command.State, "initial state");
        Near(0.0, core.Command.SpeedMetersPerSecond, 0.0, "initial speed");
        core.Start();
        Equal(RideState.Starting, core.Command.State, "start state");
        Near(0.0, core.Command.SpeedMetersPerSecond, 0.0, "start remains stationary");
        Near(0.5, core.Step(Stick(0), 1.0).SpeedMetersPerSecond, 1e-12,
            "0.5 m/s² after one second");
        var cruise = core.Step(Stick(0), 1.0);
        Near(1.0, cruise.SpeedMetersPerSecond, 1e-12, "cruise speed");
        Equal(RideState.Moving, cruise.State, "moving state");
    }

    private static void PauseIsImmediateAndResumeRamps()
    {
        var core = Riding(Settings());
        core.Step(Stick(1), 1.0);
        var paused = core.Pause();
        Equal(RideState.Paused, paused.State, "paused state");
        Near(0.0, paused.SpeedMetersPerSecond, 0.0, "speed cancelled in pause call");
        Near(0.0, paused.NormalizedSteering, 0.0, "steering cancelled in pause call");
        Near(0.0, core.Step(Stick(1), 1.0).SpeedMetersPerSecond, 0.0, "pause holds zero");
        core.Start();
        Near(0.0, core.Command.SpeedMetersPerSecond, 0.0, "resume starts stationary");
        Near(0.25, core.Step(Stick(0), 0.5).SpeedMetersPerSecond, 1e-12,
            "resume follows acceleration bound");
    }

    private static void RestartWaitsForAcknowledgement()
    {
        var core = Riding(Settings());
        core.Step(Stick(1), 1.0);
        var restarting = core.Restart();
        Equal(RideState.Restarting, restarting.State, "restart state");
        Near(0.0, restarting.SpeedMetersPerSecond, 0.0, "restart speed");
        Near(0.0, restarting.NormalizedSteering, 0.0, "restart steering");
        Throws<InvalidOperationException>(() => core.Start(), "cannot start before pose acknowledgement");
        Near(0.0, core.Step(Stick(1), 1.0).SpeedMetersPerSecond, 0.0, "restart holds zero");
        Equal(RideState.Stopped, core.AcknowledgeStationaryPose().State, "acknowledged stationary");
        Throws<InvalidOperationException>(() => core.AcknowledgeStationaryPose(),
            "duplicate acknowledgement rejected");
    }

    private static void TransitionRequiresOpaqueMask()
    {
        var core = Riding(Settings());
        core.Step(Stick(1), 1.0);
        var transition = core.BeginTransition();
        Equal(RideState.Transitioning, transition.State, "transition state");
        True(transition.RequiresOpaqueMask, "mask requested");
        True(!transition.MayReposition, "cannot reposition before mask");
        Near(0.0, transition.SpeedMetersPerSecond, 0.0, "transition speed");
        Near(0.0, transition.NormalizedSteering, 0.0, "transition steering");
        Throws<InvalidOperationException>(() => core.AcknowledgeRepositioned(),
            "reposition acknowledgement before opaque mask rejected");
        Throws<InvalidOperationException>(() => core.FinishTransition(),
            "cannot reveal before repositioning");
        True(core.ConfirmMaskOpaque().MayReposition, "reposition permitted under opaque mask");
        True(core.Step(Stick(1), 1.0).RequiresOpaqueMask, "mask held through update");
        Near(0.0, core.Command.SpeedMetersPerSecond, 0.0, "transition holds movement at zero");
        var repositioned = core.AcknowledgeRepositioned();
        True(!repositioned.MayReposition, "reposition permission consumed after acknowledgement");
        True(repositioned.RequiresOpaqueMask, "mask stays opaque through acknowledgement");
        Equal(RideState.Stopped, core.FinishTransition().State, "transition finishes stationary");
    }

    private static void RepeatedTransitionsAreDeterministic()
    {
        var core = new RideCore(Settings());
        for (int i = 0; i < 3; i++)
        {
            core.Start();
            core.Step(Stick(1), 0.25);
            core.Pause();
            core.Restart();
            core.AcknowledgeStationaryPose();
            Equal(RideState.Stopped, core.Command.State, "repeat stationary state");
            Near(0.0, core.Command.SpeedMetersPerSecond, 0.0, "repeat speed");
            Near(0.0, core.Command.NormalizedSteering, 0.0, "repeat steering");
        }
    }

    private static RideCommand RunAtFrequency(int hertz)
    {
        var core = Riding(Settings(steeringRate: 0.5));
        for (int frame = 0; frame < 2 * hertz; frame++)
            core.Step(Stick(1), 1.0 / hertz);
        return core.Command;
    }

    private static void FrameRatesAgree()
    {
        var at72 = RunAtFrequency(72);
        var at90 = RunAtFrequency(90);
        Near(1.0, at72.SpeedMetersPerSecond, 1e-12, "72 Hz cruise");
        Near(1.0, at90.SpeedMetersPerSecond, 1e-12, "90 Hz cruise");
        Near(0.9632120558828557, at72.NormalizedSteering, 1e-10, "72 Hz steering");
        Near(at72.NormalizedSteering, at90.NormalizedSteering, 1e-10,
            "72/90 Hz steering agreement");
    }

    private static void InvalidTimingAndConfigurationRejected()
    {
        Throws<ArgumentOutOfRangeException>(() => new RideSettings(0, 45, 0.1, 0.2, 1, -1, 0.5),
            "negative cruise rejected");
        Throws<ArgumentOutOfRangeException>(() => new RideSettings(0, 45, 0.1, 0.2, 1, 1, 0),
            "zero acceleration rejected");
        Throws<ArgumentOutOfRangeException>(() => new RideSettings(1, 45, 0.1, 0.2, 1, 1, 0.5),
            "full deadzone rejected");
        Throws<ArgumentOutOfRangeException>(() => new RideSettings(0, 45, 0.1, double.NaN, 1, 1, 0.5),
            "nonfinite response rejected");
        var core = Riding(Settings());
        Throws<ArgumentOutOfRangeException>(() => core.Step(Stick(0), -0.01), "negative dt rejected");
        Throws<ArgumentOutOfRangeException>(() => core.Step(Stick(0), double.PositiveInfinity),
            "infinite dt rejected");
    }

    public static int Main()
    {
        Test("neutral and provisional thumbstick source", NeutralAndDefaultSource);
        Test("two-hand sign and angle limit", HandSignAndLimit);
        Test("deadzone boundaries and normalized bounds", DeadzoneAndBounds);
        Test("invalid thumbstick clears stale steering", InvalidInputCannotLeaveStaleSteering);
        Test("invalid or missing controller pose clears steering", InvalidHandsCannotLeaveStalePose);
        Test("timestep-consistent smoothing", TimeConsistentSmoothing);
        Test("steering source change is rate bounded", SourceChangeIsRateBounded);
        Test("rate-limited smoothing composes over timesteps", RateLimitComposesAcrossTimesteps);
        Test("stationary start and acceleration bound", StationaryStartAndAcceleration);
        Test("immediate pause and bounded resume", PauseIsImmediateAndResumeRamps);
        Test("restart requires stationary pose acknowledgement", RestartWaitsForAcknowledgement);
        Test("route transition requires opaque mask", TransitionRequiresOpaqueMask);
        Test("repeated transitions are deterministic", RepeatedTransitionsAreDeterministic);
        Test("72 Hz and 90 Hz frame-rate consistency", FrameRatesAgree);
        Test("invalid timing and configuration rejected", InvalidTimingAndConfigurationRejected);
        Console.WriteLine("RESULT " + _passed + " passed, " + _failed + " failed");
        return _failed == 0 ? 0 : 1;
    }
}
