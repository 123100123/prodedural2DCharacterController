using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Crouching on the real character: sinking on planted feet, a crouch gait with its own speed, transitions with the
// walk and run, uneven ground, a low ceiling, aiming and shooting, and jumping from a crouch.
public class CrouchScenarioTests : ScenarioFixture {
    private static readonly (float height, float width, float spacing) Bumps = (0.15f, 1f, 2.2f);
    private const float UnevenGroundFootTolerance = 0.15f;

    private LocomotionTelemetry Telemetry => Stage.Inspector.ReadTelemetry();
    private float CrouchDepth => Stage.Character.Parts.Crouch.Settings.depth;

    [UnityTest]
    public IEnumerator HoldingCrouchSinksThePelvisOnPlantedFeetAndReleasingStandsUp() {
        yield return Hold(1.5f);
        GaitMetrics standing = Measure("standing", 0.5f, Recorder.Now);

        Stage.Press(Key.LeftCtrl);
        float crouchStart = Recorder.Now;
        yield return Hold(2f);
        GaitMetrics crouched = Measure("crouched still", crouchStart + 1f, Recorder.Now);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f));
        Assert.That(standing.MeanBodyAboveStance - crouched.MeanBodyAboveStance, Is.GreaterThan(CrouchDepth * 0.85f), "the pelvis sinks by the crouch depth");
        Assert.That(crouched.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance), "the feet stay on the ground");

        Stage.Release(Key.LeftCtrl);
        yield return Hold(2f);
        GaitMetrics standingAgain = Measure("standing again", Recorder.Now - 1f, Recorder.Now);
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f));
        Assert.That(standingAgain.MeanBodyAboveStance, Is.EqualTo(standing.MeanBodyAboveStance).Within(0.08f), "back to the standing height");
    }

    [UnityTest]
    public IEnumerator ATapLatchesCrouchOnAndAnotherTapReleasesIt() {
        yield return Tap(Key.LeftCtrl);
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "a tap latches crouch on");

        yield return Tap(Key.LeftCtrl);
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "a second tap stands up");
    }

    [UnityTest]
    public IEnumerator CrouchWalkingMovesAtTheCrouchSpeedWithAnEvenLowGait() {
        Stage.Press(Key.LeftCtrl, Key.D);
        yield return Hold(1f);
        float start = Recorder.Now;
        yield return Hold(4f);
        GaitMetrics crouchWalk = Measure("crouch walk", start, Recorder.Now);

        Assert.That(crouchWalk.MeanSpeed, Is.EqualTo(Stage.Stride.CrouchGait.speed).Within(0.15f));
        Assert.That(crouchWalk.LiftCount, Is.GreaterThan(4), "it steps");
        AssertEvenGait((crouchWalk, SteadyAsymmetry));
        Assert.That(crouchWalk.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance));
        Assert.That(crouchWalk.MeanContactInterval, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator ACrouchWalkTakesShorterSlowerStepsThanAWalk() {
        Stage.Press(Key.D);
        yield return Hold(3f);
        GaitMetrics walk = Measure("walk", 1.5f, Recorder.Now);

        Stage.Press(Key.LeftCtrl);
        yield return Hold(3f);
        GaitMetrics crouchWalk = Measure("crouch walk", Recorder.Now - 2f, Recorder.Now);

        Assert.That(crouchWalk.MeanSpeed, Is.LessThan(walk.MeanSpeed * 0.7f));
        float walkStep = walk.MeanSpeed * walk.MeanContactInterval;
        float crouchStep = crouchWalk.MeanSpeed * crouchWalk.MeanContactInterval;
        Assert.That(crouchStep, Is.LessThan(walkStep), "shorter steps");
        Assert.That(walk.MeanBodyAboveStance - crouchWalk.MeanBodyAboveStance, Is.GreaterThan(CrouchDepth * 0.7f), "carried lower");
    }

    [UnityTest]
    public IEnumerator CrouchingWhileWalkingAndStandingBackUpKeepsTheGaitEven() {
        Stage.Press(Key.D);
        yield return Hold(2f);
        float down = Recorder.Now;
        Stage.Press(Key.LeftCtrl);
        yield return Hold(2.5f);
        float up = Recorder.Now;
        Stage.Release(Key.LeftCtrl);
        yield return Hold(2.5f);

        GaitMetrics going = Measure("walk -> crouch walk", down, up);
        GaitMetrics coming = Measure("crouch walk -> walk", up, Recorder.Now);
        AssertEvenGait((going, TransitionAsymmetry));
        AssertEvenGait((coming, TransitionAsymmetry));
        Assert.That(going.PhaseResyncs, Is.Zero);
        Assert.That(coming.PhaseResyncs, Is.Zero);
        Assert.That(going.EndsGrounded && coming.EndsGrounded, Is.True);
    }

    [UnityTest]
    public IEnumerator CrouchingFromARunSlowsThroughTheRampWithoutSlidingOrSnapping() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(3f);
        float runSpeed = Mathf.Abs(Stage.Stride.GroundSpeed);
        Assert.That(runSpeed, Is.GreaterThan(Stage.Stride.Run.speed * 0.9f), "running");

        Stage.Press(Key.LeftCtrl);
        float worstDeceleration = 0f;
        float previous = runSpeed;
        float start = Recorder.Now;
        for (int frame = 0; frame < 150; frame++) {
            yield return null;
            float speed = Mathf.Abs(Stage.Stride.GroundSpeed);
            worstDeceleration = Mathf.Max(worstDeceleration, (previous - speed) / ScenarioStage.FrameTime);
            previous = speed;
        }
        GaitMetrics slowing = Measure("run -> crouch", start, Recorder.Now);

        Assert.That(previous, Is.EqualTo(Stage.Stride.CrouchGait.speed).Within(0.25f), "ends at the crouch speed");
        Assert.That(worstDeceleration, Is.LessThanOrEqualTo(Stage.Stride.Run.deceleration * 1.1f), "slows through the ramp: no snap");
        Assert.That(slowing.RepeatedLegLifts, Is.Zero);
        Assert.That(slowing.EndsGrounded, Is.True);
    }

    [UnityTest]
    public IEnumerator TurningAroundWhileCrouchedStepsEvenly() {
        Stage.Press(Key.LeftCtrl, Key.D);
        yield return Hold(3f);
        Stage.Release(Key.D);
        Stage.Press(Key.A);
        float turned = Recorder.Now;
        yield return Hold(4f);

        GaitMetrics afterTurn = Measure("crouch turn", turned, Recorder.Now);
        Assert.That(Stage.Stride.GroundSpeed, Is.LessThan(-1f), "heading left");
        Assert.That(afterTurn.RepeatedLegLifts, Is.Zero);
        Assert.That(afterTurn.EndsGrounded, Is.True);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "still crouched");
    }

    [UnityTest]
    public IEnumerator CrouchWalkingOverUnevenGroundStaysOnItsFeet() {
        float startX = Stage.Stride.transform.position.x;
        Stage.AddBumps((startX + 3f, startX + 40f), Bumps);
        Stage.Press(Key.LeftCtrl, Key.D);
        yield return Hold(8f);
        GaitMetrics bumpy = Measure("crouch walk over bumps", 3f, Recorder.Now);

        Assert.That(bumpy.EndsGrounded, Is.True);
        Assert.That(bumpy.RepeatedLegLifts, Is.Zero);
        Assert.That(bumpy.PhaseResyncs, Is.Zero);
        Assert.That(bumpy.WorstPlantedFootGap, Is.LessThan(UnevenGroundFootTolerance));
    }

    [UnityTest]
    public IEnumerator ALowCeilingHoldsTheCrouchUntilThereIsRoomToStand() {
        float startX = Stage.Stride.transform.position.x;
        // Clear enough to crouch under, too low to stand under.
        Stage.AddCeiling((startX + 4f, 4.1f, 14f));
        Stage.Press(Key.LeftCtrl, Key.D);
        yield return Hold(5f);
        Assert.That(Stage.Stride.transform.position.x, Is.GreaterThan(startX + 5f), "under the ceiling");

        Stage.Release(Key.LeftCtrl);
        yield return Hold(1f);
        Assert.That(Telemetry.IsHeadroomBlocked, Is.True, "no room to stand");
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.9f), "still crouched");

        yield return Hold(8f);
        Assert.That(Stage.Stride.transform.position.x, Is.GreaterThan(startX + 19f), "walked out the far side");
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "stands up in the open");
    }

    [UnityTest]
    public IEnumerator AimingAndShootingWhileCrouchedWorksAtTheCrouchPose() {
        int shots = 0;
        Stage.Character.Events.Fired += _ => shots++;
        Stage.Press(Key.LeftCtrl);
        yield return Hold(1.5f);
        GaitMetrics before = Measure("crouched, before aiming", 0.5f, Recorder.Now);
        float aimStart = Recorder.Now;
        for (int frame = 0; frame < 150; frame++) {
            Vector2 body = Stage.Stride.transform.position;
            Stage.AimAndFire(body + new Vector2(6f, 3f), frame % 10 < 5 && frame > 20);
            yield return null;
        }
        Stage.StopAiming();
        GaitMetrics aiming = Measure("crouched, aiming and shooting", aimStart + 0.5f, Recorder.Now);

        Assert.That(shots, Is.GreaterThan(3), "the pistol fires while crouched");
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "aiming does not stand the character up");
        Assert.That(aiming.MeanHeadAngle, Is.GreaterThan(before.MeanHeadAngle + 5f), "the head looks along the aim");
        Assert.That(aiming.MeanBodyAboveStance, Is.EqualTo(before.MeanBodyAboveStance).Within(0.1f), "the low pose holds");
    }

    [UnityTest]
    public IEnumerator JumpingFromACrouchLeavesTheGroundAndLandsStillCrouched() {
        Stage.Press(Key.LeftCtrl);
        yield return Hold(1.5f);
        float jumpAt = Recorder.Now;
        yield return Tap(Key.Space);
        yield return Hold(2.5f);
        GaitMetrics jump = Measure("crouch jump", jumpAt, Recorder.Now);

        Assert.That(jump.UngroundedFrames, Is.GreaterThan(10), "left the ground");
        Assert.That(jump.EndsGrounded, Is.True, "landed");
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "crouched on landing, as the key is still held");
    }

    private IEnumerator Tap(Key key) {
        Stage.Press(key);
        for (int frame = 0; frame < 3; frame++) {
            yield return null;
        }
        Stage.Release(key);
    }
}
