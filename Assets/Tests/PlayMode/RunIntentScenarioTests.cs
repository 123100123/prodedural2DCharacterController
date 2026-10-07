using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Shift as a persistent run intent on the real character: the walk runs as soon as it can; Shift with no direction does
// nothing; a tapped crouch gives way to the intent (ramping up to the run, no snap) while a held one or a low ceiling
// does not; a roll, a landing and a climb hand their momentum on and the run takes over; aiming holds it to a walk.
// The F1 telemetry names the state, why it cannot run, and the last switch.
public class RunIntentScenarioTests : ScenarioFixture {
    private StrideController Stride => Stage.Stride;
    private Vector2 Body => Stride.transform.position;
    private float Speed => Mathf.Abs(Stride.GroundSpeed);
    private ICharacterState State => Stage.Character.CurrentState;
    private LocomotionTelemetry Telemetry => Stage.Inspector.ReadTelemetry();

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    private IEnumerator Tap(Key key) {
        Stage.Press(key);
        for (int frame = 0; frame < 3; frame++) {
            yield return null;
        }
        Stage.Release(key);
    }

    private IEnumerator LatchCrouch() {
        yield return Tap(Key.LeftCtrl);
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "a tap latched the crouch");
    }

    [UnityTest]
    public IEnumerator ShiftWhileWalkingRunsAndTheTelemetrySaysWhy() {
        Stage.Press(Key.D);
        yield return Hold(2f);
        LocomotionTelemetry walking = Telemetry;
        Assert.That(Speed, Is.EqualTo(Stride.Walk.speed).Within(0.3f), "walking");
        Assert.That(walking.RunIntent, Is.False);
        Assert.That(walking.StateName, Is.EqualTo("Walk"));
        Assert.That(walking.Readiness.CanRun, Is.True, "the walk could run");

        Stage.Press(Key.LeftShift);
        yield return Hold(1.5f);
        LocomotionTelemetry running = Telemetry;
        Report($"walk {Stride.Walk.speed:F2} -> {Speed:F2} u/s with Shift");
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs");
        Assert.That(running.RunHeld && running.RunIntent, Is.True);
        Assert.That(running.Readiness.CanRun, Is.True);
        Assert.That(running.RunIntentPending, Is.False, "fulfilled");
    }

    [UnityTest]
    public IEnumerator ShiftWithNoDirectionLeavesATappedCrouchAlone() {
        yield return LatchCrouch();
        Stage.Press(Key.LeftShift);
        yield return Hold(1.5f);
        LocomotionTelemetry held = Telemetry;
        Assert.That(held.CrouchShare, Is.GreaterThan(0.95f), "still crouched");
        Assert.That(Speed, Is.LessThan(0.1f), "standing still");
        Assert.That(held.RunHeld, Is.True);
        Assert.That(held.RunIntent, Is.False, "no direction, no intent");
        Assert.That(held.RunIntentPending, Is.False);
        Assert.That(held.Readiness.Block, Is.EqualTo(RunBlock.CrouchLatched));

        Stage.Release(Key.LeftShift);
        yield return Hold(0.5f);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "the latch is still on");
    }

    [UnityTest]
    public IEnumerator ShiftAndADirectionCancelATappedCrouchAndRampUpToARunWithoutASnap() {
        yield return LatchCrouch();
        Stage.Press(Key.LeftShift);
        yield return Hold(0.3f);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "Shift alone keeps it");

        Stage.Press(Key.D);
        float worstAcceleration = 0f;
        float previous = Speed;
        float firstFrameSpeed = -1f;
        for (int frame = 0; frame < 150; frame++) {
            yield return null;
            float speed = Speed;
            firstFrameSpeed = firstFrameSpeed < 0f ? speed : firstFrameSpeed;
            worstAcceleration = Mathf.Max(worstAcceleration, (speed - previous) / ScenarioStage.FrameTime);
            previous = speed;
        }
        LocomotionTelemetry after = Telemetry;
        Report($"crouch -> run: first frame {firstFrameSpeed:F2} u/s, worst acceleration {worstAcceleration:F2} u/s² (run {Stride.Run.acceleration:F2}), speed {previous:F2}, crouch share {after.CrouchShare:F2}");
        Assert.That(worstAcceleration, Is.LessThanOrEqualTo(Stride.Run.acceleration * 1.1f), "ramps up: no snap");
        Assert.That(firstFrameSpeed, Is.LessThan(0.5f), "starts from where it stood");
        Assert.That(previous, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs");
        Assert.That(after.CrouchShare, Is.LessThan(0.05f), "stood up");
        Assert.That(after.Readiness.CanRun, Is.True);

        Stage.Release(Key.D, Key.LeftShift);
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "the latch was cancelled, not just overridden");
    }

    [UnityTest]
    public IEnumerator AHeldCrouchWinsOverShiftUntilItIsLetGo() {
        Stage.Press(Key.LeftCtrl, Key.D, Key.LeftShift);
        yield return Hold(3f);
        LocomotionTelemetry crouching = Telemetry;
        Assert.That(Speed, Is.EqualTo(Stride.CrouchGait.speed).Within(0.25f), "crouch walks");
        Assert.That(crouching.CrouchShare, Is.GreaterThan(0.95f));
        Assert.That(crouching.Readiness.Block, Is.EqualTo(RunBlock.CrouchHeld));
        Assert.That(crouching.RunIntentPending, Is.True, "the intent waits");

        Stage.Release(Key.LeftCtrl);
        yield return Hold(2f);
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs once the crouch is let go");
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f));
    }

    [UnityTest]
    public IEnumerator UnderALowCeilingTheIntentCrouchWalksOutThenStandsAndRuns() {
        float startX = Body.x;
        // Clear enough to crouch under, too low to stand under (as in the crouch scenarios); it ends at startX + 18.
        Stage.AddCeiling((startX + 4f, 4.1f, 14f));
        Stage.Press(Key.LeftCtrl, Key.D);
        yield return Hold(5f);
        Stage.Release(Key.LeftCtrl);
        Stage.Press(Key.LeftShift);
        yield return Hold(1f);
        LocomotionTelemetry under = Telemetry;
        Assert.That(Body.x, Is.GreaterThan(startX + 5f).And.LessThan(startX + 16f), "under the ceiling");
        Assert.That(under.IsHeadroomBlocked, Is.True);
        Assert.That(under.Readiness.Block, Is.EqualTo(RunBlock.NoHeadroom));
        Assert.That(under.RunIntentPending, Is.True);
        Assert.That(Speed, Is.EqualTo(Stride.CrouchGait.speed).Within(0.3f), "crouch walks under it");

        yield return WaitUntil(() => Body.x > startX + 18.5f, 10f);
        yield return Hold(2f);
        Report($"out at {Body.x - startX:F2}, speed {Speed:F2}, crouch share {Telemetry.CrouchShare:F2}");
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "stands in the open");
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "and runs");
    }

    // Rolls from the current motion and returns how many frames the roll lasted and the speed it handed to the walk.
    private IEnumerator Roll(System.Action<(int frames, float handed)> result) {
        Stage.Press(Key.LeftAlt);
        yield return WaitUntil(() => State is RollState, 0.3f);
        Stage.Release(Key.LeftAlt);
        Assert.That(State, Is.InstanceOf<RollState>(), "rolls");
        int frames = 0;
        while (State is RollState && frames < 180) {
            if (frames == 2) {
                Assert.That(Telemetry.Readiness.Block, Is.EqualTo(RunBlock.RollCommitted), "the dive is committed");
            }
            yield return null;
            frames++;
        }
        Assert.That(State, Is.InstanceOf<WalkState>(), "back on its feet");
        result((frames, Mathf.Abs(Telemetry.CarriedSpeed)));
    }

    [UnityTest]
    public IEnumerator ARollWithTheIntentAlongItHandsOverEarlierAtTheRollsSpeedAndRuns() {
        RollSettings roll = Stage.Character.Parts.Roll;
        Stage.Press(Key.D);
        yield return Hold(1.5f);
        (int frames, float handed) walked = (0, 0f);
        yield return Roll(r => walked = r);
        yield return Hold(1f);

        Stage.Press(Key.LeftShift);
        yield return Hold(1.5f);
        (int frames, float handed) ran = (0, 0f);
        yield return Roll(r => ran = r);
        Report($"roll without Shift: {walked.frames} frames, handed {walked.handed:F2} u/s; with Shift + D: {ran.frames} frames, handed {ran.handed:F2} u/s (roll speed {roll.rollSpeed:F2}, recovery {roll.recoverSpeed:F2})");
        Assert.That(ran.frames, Is.LessThan(walked.frames), "hands over earlier");
        Assert.That(ran.handed, Is.GreaterThanOrEqualTo(roll.rollSpeed * 0.9f), "at the roll's travel speed");
        Assert.That(ran.handed, Is.GreaterThan(walked.handed), "more than the recovery hands over");
        Assert.That(Telemetry.PreviousStateName, Is.EqualTo("Roll"));

        yield return Hold(1.5f);
        Assert.That(State, Is.InstanceOf<WalkState>());
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs on");
    }

    [UnityTest]
    public IEnumerator ARunningJumpWithShiftHeldLandsAndRunsOnWithItsSpeed() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2f);
        int changesBefore = Telemetry.StateChanges;
        Stage.Press(Key.Space);
        yield return WaitUntil(() => State is AirborneState, 1f);
        yield return Hold(0.15f);
        Stage.Release(Key.Space);
        LocomotionTelemetry flying = Telemetry;
        Assert.That(flying.StateName, Is.EqualTo("Airborne"));
        Assert.That(flying.Readiness.Block, Is.EqualTo(RunBlock.Airborne));
        Assert.That(flying.RunIntentPending, Is.True, "the intent waits for the landing");
        Assert.That(flying.PreviousStateName, Is.EqualTo("JumpWindup"));

        yield return WaitUntil(() => State is WalkState, 3f);
        LocomotionTelemetry landed = Telemetry;
        Report($"landed carrying {landed.CarriedSpeed:F2} u/s (run {Stride.Run.speed:F2}), {landed.StateChanges - changesBefore} switches");
        Assert.That(landed.StateName, Is.EqualTo("Walk"));
        Assert.That(landed.PreviousStateName, Is.EqualTo("Airborne"));
        Assert.That(landed.SecondsSinceStateChange, Is.LessThan(0.1f));
        Assert.That(landed.StateChanges - changesBefore, Is.EqualTo(3), "walk -> windup -> airborne -> walk");
        Assert.That(Mathf.Abs(landed.CarriedSpeed), Is.GreaterThan(Stride.Run.speed * 0.8f), "the landing carried the speed");

        yield return Hold(1f);
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs on");
    }

    [UnityTest]
    public IEnumerator FinishingAClimbWithShiftAndTowardTheTopStartsMovingAndRuns() {
        float ledgeTop = Stage.AddLedge((Body.x + 1.6f, 2.5f, 30f));
        Stage.Press(Key.D);
        yield return Hold(1f);
        yield return Tap(Key.Space);
        yield return WaitUntil(() => State is ClimbState, 1f);
        Assert.That(State, Is.InstanceOf<ClimbState>(), "climbs");
        Stage.Press(Key.LeftShift);
        yield return Hold(0.1f);
        Assert.That(Telemetry.Readiness.Block, Is.EqualTo(RunBlock.Climbing));
        Assert.That(Telemetry.RunIntentPending, Is.True);

        yield return WaitUntil(() => State is WalkState, 6f);
        LocomotionTelemetry standing = Telemetry;
        Report($"stood on top carrying {standing.CarriedSpeed:F2} u/s");
        Assert.That(standing.PreviousStateName, Is.EqualTo("Climb"));
        Assert.That(standing.CarriedSpeed, Is.GreaterThan(0f), "starts moving toward the top");
        Assert.That(Stride.StanceHeight, Is.EqualTo(ledgeTop).Within(0.05f), "on top");

        yield return Hold(2f);
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs on");
        Assert.That(Stride.StanceHeight, Is.EqualTo(ledgeTop).Within(0.1f), "still on top");
    }

    [UnityTest]
    public IEnumerator AimingWithShiftWalksAndSaysSo() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(1.5f);
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "running");

        LocomotionTelemetry aiming = null;
        for (float elapsed = 0f; elapsed < 2.5f; elapsed += ScenarioStage.FrameTime) {
            Stage.AimAt(Body + new Vector2(6f, 2f));
            yield return null;
        }
        aiming = Telemetry;
        Assert.That(Speed, Is.EqualTo(Stride.Walk.speed).Within(0.4f), "aiming slows to a walk");
        Assert.That(aiming.Readiness.Block, Is.EqualTo(RunBlock.Aiming));
        Assert.That(aiming.RunIntentPending, Is.True);

        Stage.StopAiming();
        yield return Hold(1.5f);
        Assert.That(Speed, Is.GreaterThan(Stride.Run.speed * 0.9f), "runs again");
    }
}
