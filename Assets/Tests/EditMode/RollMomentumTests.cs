using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The roll's momentum on its own: it starts at the incoming speed, a throw (harder from a run) lifts it to a clamped
// entry speed, drag and brakes decay it through the phases without a step, a floor keeps a standing roll moving until
// the recovery, the recovery settles, walks on or keeps the speed, and the hand-back is the speed itself.
public class RollMomentumTests {
    private const float Frame = 1f / 60f;
    private const float RunSpeed = 9f;
    private const float WalkSpeed = 3.5f;

    private static RollMomentumSettings Settings => new RollMomentumSettings();

    // The default roll's phase lengths (dive, a typical hand contact, roll, recovery).
    private static readonly (RollMotionPhase phase, float duration)[] Timeline = {
        (RollMotionPhase.Dive, 0.22f), (RollMotionPhase.HandContact, 0.15f), (RollMotionPhase.Roll, 0.42f), (RollMotionPhase.Recover, 0.3f),
    };

    // The speed every frame of a whole roll started at the incoming speed.
    private static List<float> Simulate(float incoming, RollRecoverIntent intent, RollMomentumSettings settings) {
        RollLaunch launch = RollMomentum.Launch((incoming, RunSpeed), settings);
        float speed = RollMomentum.StartSpeed(launch, settings);
        List<float> speeds = new List<float> { speed };
        float time = 0f;
        foreach ((RollMotionPhase phase, float duration) part in Timeline) {
            for (float elapsed = 0f; elapsed < part.duration - 1e-5f; elapsed += Frame) {
                speed = RollMomentum.Advance(speed, (launch, time, part.phase, intent, WalkSpeed), Frame, settings);
                time += Frame;
                speeds.Add(speed);
            }
        }
        return speeds;
    }

    [Test]
    public void TheRollStartsAtTheIncomingSpeedAndTheThrowRisesFromThereWithoutASnap() {
        RollMomentumSettings settings = Settings;
        foreach (float incoming in new[] { 0f, WalkSpeed, RunSpeed }) {
            RollLaunch launch = RollMomentum.Launch((incoming, RunSpeed), settings);
            float start = RollMomentum.StartSpeed(launch, settings);
            Assert.That(start, Is.EqualTo(incoming), "continuous at u_in");
            List<float> speeds = Simulate(incoming, RollRecoverIntent.Settle, settings);
            for (int frame = 1; frame < speeds.Count; frame++) {
                Assert.That(Mathf.Abs(speeds[frame] - speeds[frame - 1]), Is.LessThanOrEqualTo(launch.ThrowAcceleration * Frame + 1e-4f), $"from {incoming}: frame {frame}");
            }
            int throwFrames = Mathf.CeilToInt(settings.throwTime / Frame);
            Assert.That(speeds[throwFrames], Is.EqualTo(launch.Target).Within(launch.Target * settings.diveDrag * Frame + 1e-3f), $"from {incoming}: thrown up to the entry speed");
        }
    }

    [Test]
    public void TheThrowRisesFromStandingToRunningAndKeepsAShareOfTheIncomingSpeed() {
        RollMomentumSettings settings = Settings;
        RollLaunch standing = RollMomentum.Launch((0f, RunSpeed), settings);
        RollLaunch walking = RollMomentum.Launch((WalkSpeed, RunSpeed), settings);
        RollLaunch running = RollMomentum.Launch((RunSpeed, RunSpeed), settings);
        Assert.That(standing.Throw, Is.EqualTo(settings.standingThrow));
        Assert.That(running.Throw, Is.EqualTo(settings.runningThrow));
        Assert.That(walking.Throw, Is.GreaterThan(standing.Throw).And.LessThan(running.Throw));
        Assert.That(walking.RunShare, Is.EqualTo(WalkSpeed / RunSpeed).Within(1e-5f));
        Assert.That(walking.Inherited, Is.EqualTo(settings.inheritShare * WalkSpeed).Within(1e-5f));
        Assert.That(walking.Target, Is.EqualTo(walking.Inherited + walking.Throw).Within(1e-5f));
        Assert.That(standing.Target, Is.EqualTo(settings.standingThrow), "a standing roll still launches");

        float previous = -1f;
        for (float incoming = 0f; incoming <= RunSpeed; incoming += 0.25f) {
            float target = RollMomentum.Launch((incoming, RunSpeed), settings).Target;
            Assert.That(target, Is.GreaterThan(previous), $"faster in, faster roll ({incoming})");
            previous = target;
        }
    }

    [Test]
    public void TheEntrySpeedNeverExceedsItsCap() {
        RollMomentumSettings settings = Settings;
        settings.maxEntrySpeed = 10f;
        Assert.That(RollMomentum.Launch((RunSpeed, RunSpeed), settings).Target, Is.EqualTo(10f), "0.9 × 9 + 4 is cut to the cap");
        foreach (float incoming in new[] { 0f, 5f, RunSpeed, 10f }) {
            Assert.That(Simulate(incoming, RollRecoverIntent.Along, settings), Has.All.LessThanOrEqualTo(10f + 1e-4f), $"from {incoming}");
        }

        RollLaunch tooFast = RollMomentum.Launch((14f, RunSpeed), settings);
        Assert.That(tooFast.Target, Is.EqualTo(10f));
        Assert.That(tooFast.RunShare, Is.EqualTo(1f));
        List<float> braked = Simulate(14f, RollRecoverIntent.Along, settings);
        Assert.That(braked[0], Is.EqualTo(14f), "still continuous at u_in");
        Assert.That(braked[Mathf.CeilToInt(settings.throwTime / Frame)], Is.LessThanOrEqualTo(10f + 1e-4f), "brought down to the cap over the throw");
    }

    [Test]
    public void AfterTheThrowTheSpeedOnlyDecaysAndPhaseChangesMakeNoStep() {
        RollMomentumSettings settings = Settings;
        foreach (RollRecoverIntent intent in new[] { RollRecoverIntent.Settle, RollRecoverIntent.Along }) {
            foreach (float incoming in new[] { WalkSpeed, 6f, RunSpeed }) {
                List<float> speeds = Simulate(incoming, intent, settings);
                int throwFrames = Mathf.CeilToInt(settings.throwTime / Frame);
                float largestBrake = settings.contactBrake + settings.settleBrake;
                float largestDrag = Mathf.Max(settings.diveDrag, Mathf.Max(settings.rollDrag, settings.recoverDrag));
                for (int frame = throwFrames + 1; frame < speeds.Count; frame++) {
                    float drop = speeds[frame - 1] - speeds[frame];
                    Assert.That(drop, Is.GreaterThanOrEqualTo(-1e-5f), $"{intent} from {incoming}: never speeds up after the throw (frame {frame})");
                    Assert.That(drop, Is.LessThanOrEqualTo((largestDrag * speeds[frame - 1] + largestBrake) * Frame + 1e-4f), $"{intent} from {incoming}: no step at frame {frame}");
                }
                Assert.That(speeds[speeds.Count - 1], Is.LessThan(speeds[throwFrames]), "it decays");
            }
        }
    }

    [Test]
    public void TheRollNeverTravelsSlowerThanItsFloorUntilTheRecovery() {
        RollMomentumSettings settings = Settings;
        settings.rollDrag = 5f;
        RollLaunch launch = RollMomentum.Launch((0f, RunSpeed), settings);
        float speed = RollMomentum.StartSpeed(launch, settings);
        float time = 0f;
        foreach (RollMotionPhase phase in new[] { RollMotionPhase.Dive, RollMotionPhase.HandContact, RollMotionPhase.Roll }) {
            for (int frame = 0; frame < 60; frame++) {
                speed = RollMomentum.Advance(speed, (launch, time, phase, RollRecoverIntent.Settle, WalkSpeed), Frame, settings);
                time += Frame;
                if (time > settings.throwTime) {
                    Assert.That(speed, Is.GreaterThanOrEqualTo(settings.minRollSpeed), $"{phase} at {time:F2} s");
                }
            }
        }
        Assert.That(speed, Is.EqualTo(settings.minRollSpeed).Within(1e-4f), "held at the floor");
        speed = RollMomentum.Advance(speed, (launch, time, RollMotionPhase.Recover, RollRecoverIntent.Settle, WalkSpeed), Frame, settings);
        Assert.That(speed, Is.LessThan(settings.minRollSpeed), "the recovery may go below it");
    }

    [Test]
    public void TheRecoverySettlesToAStopWalksOnAtTheWalkSpeedOrOnlyDrags() {
        RollMomentumSettings settings = Settings;
        (RollMotionPhase phase, RollRecoverIntent intent, float walkSpeed) settle = (RollMotionPhase.Recover, RollRecoverIntent.Settle, WalkSpeed);
        (RollMotionPhase phase, RollRecoverIntent intent, float walkSpeed) walk = (RollMotionPhase.Recover, RollRecoverIntent.Walk, WalkSpeed);
        (RollMotionPhase phase, RollRecoverIntent intent, float walkSpeed) along = (RollMotionPhase.Recover, RollRecoverIntent.Along, WalkSpeed);

        float settling = 6f;
        float walkingDown = 7f;
        float walkingUp = 1.5f;
        float running = 6f;
        for (int frame = 0; frame < 90; frame++) {
            settling = RollMomentum.Decay(settling, settle, Frame, settings);
            walkingDown = RollMomentum.Decay(walkingDown, walk, Frame, settings);
            walkingUp = RollMomentum.Decay(walkingUp, walk, Frame, settings);
            running = RollMomentum.Decay(running, along, Frame, settings);
            Assert.That(settling, Is.GreaterThanOrEqualTo(0f));
            Assert.That(walkingDown, Is.GreaterThanOrEqualTo(WalkSpeed), "walk input never brakes below the walk speed");
            Assert.That(walkingUp, Is.LessThanOrEqualTo(WalkSpeed), "nor pulls past it");
        }
        Assert.That(settling, Is.EqualTo(0f), "no input: comes to a stop");
        Assert.That(walkingDown, Is.EqualTo(WalkSpeed).Within(1e-4f), "walk input: down to the walk speed");
        Assert.That(walkingUp, Is.EqualTo(WalkSpeed).Within(1e-4f), "walk input: up to the walk speed");
        Assert.That(running, Is.EqualTo(6f * Mathf.Exp(-settings.recoverDrag * 1.5f)).Within(1e-3f), "run intent along: only the drag");

        Assert.That(RollMomentum.Resistance((RollMotionPhase.Recover, RollRecoverIntent.Along), settings), Is.EqualTo((settings.recoverDrag, 0f)));
        Assert.That(RollMomentum.Resistance((RollMotionPhase.Recover, RollRecoverIntent.Settle), settings), Is.EqualTo((settings.recoverDrag, settings.settleBrake)));
        Assert.That(RollMomentum.Resistance((RollMotionPhase.HandContact, RollRecoverIntent.Settle), settings), Is.EqualTo((settings.diveDrag, settings.contactBrake)));
    }

    [Test]
    public void TheHandBackIsTheSpeedAlongTheSurfaceAndTheIncomingSpeedIsReadAlongIt() {
        Vector2 flat = SlopeMath.AlongSurface(0f, 1);
        Vector2 uphillLeft = SlopeMath.AlongSurface(-0.5f, -1);
        Assert.That(RollMomentum.HandBack(5.25f, flat), Is.EqualTo(new Vector2(5.25f, 0f)));
        Assert.That(RollMomentum.HandBack(5.25f, uphillLeft).magnitude, Is.EqualTo(5.25f).Within(1e-4f));
        Assert.That(RollMomentum.HandBack(5.25f, uphillLeft).x, Is.LessThan(0f), "along the roll");

        Assert.That(RollMomentum.IncomingSpeed(new Vector2(4f, 0f), flat), Is.EqualTo(4f));
        Assert.That(RollMomentum.IncomingSpeed(new Vector2(-4f, 0f), flat), Is.EqualTo(0f), "moving against the roll counts as standing");
        Vector2 upTheRamp = uphillLeft * 3f;
        Assert.That(RollMomentum.IncomingSpeed(upTheRamp, uphillLeft), Is.EqualTo(3f).Within(1e-4f));
    }

    [Test]
    public void TheDiveArcRisesAndFallsBackAndIsHigherAndThePitchLeadsSoonerFromARun() {
        RollMomentumSettings settings = Settings;
        Assert.That(RollMomentum.DiveLift(0f, 1f, settings), Is.EqualTo(0f).Within(1e-5f));
        Assert.That(RollMomentum.DiveLift(1f, 1f, settings), Is.EqualTo(0f).Within(1e-5f), "back to the dive's own height by its end");
        float standingPeak = 0f;
        float runningPeak = 0f;
        float peakAt = 0f;
        for (float progress = 0f; progress <= 1f; progress += 0.01f) {
            standingPeak = Mathf.Max(standingPeak, RollMomentum.DiveLift(progress, 0f, settings));
            float lift = RollMomentum.DiveLift(progress, 1f, settings);
            peakAt = lift > runningPeak ? progress : peakAt;
            runningPeak = Mathf.Max(runningPeak, lift);
        }
        Assert.That(standingPeak, Is.EqualTo(settings.standingLift).Within(1e-3f));
        Assert.That(runningPeak, Is.EqualTo(settings.runningLift).Within(1e-3f));
        Assert.That(peakAt, Is.LessThan(0.5f), "a quick rise, a longer descent");

        Assert.That(RollMomentum.PitchLeadScale(0f), Is.EqualTo(1f));
        Assert.That(RollMomentum.PitchLeadScale(1f), Is.LessThan(RollMomentum.PitchLeadScale(0.5f)).And.GreaterThan(0.5f));
    }

    [Test]
    public void WithoutAThrowTimeTheRollStartsAtItsEntrySpeed() {
        RollMomentumSettings settings = Settings;
        settings.throwTime = 0f;
        RollLaunch launch = RollMomentum.Launch((WalkSpeed, RunSpeed), settings);
        Assert.That(RollMomentum.StartSpeed(launch, settings), Is.EqualTo(launch.Target));
        Assert.That(float.IsPositiveInfinity(launch.ThrowAcceleration), Is.True);
    }
}
