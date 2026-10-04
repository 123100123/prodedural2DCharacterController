using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Scenarios around the grounded gait: standing still, uneven ground, jumps and landings, and aiming while walking.
// They check the gait stays even and that the other moves hand back to it cleanly.
public class MoveScenarioTests : ScenarioFixture {
    // Small steps up and down along the track, low enough to walk over without climbing.
    private static readonly (float height, float width, float spacing) Bumps = (0.15f, 1f, 2.2f);
    // Stepping between bump heights, the knees keep their reserve and the IK may trail a foot by a few hundredths for a
    // frame on top of the rig's static 0.06 effector offset.
    private const float UnevenGroundFootTolerance = 0.15f;

    [UnityTest]
    public IEnumerator StandingStillHoldsAQuietStance() {
        yield return Hold(6f);
        GaitMetrics standing = Measure("standing 5.5 s", 0.5f, Recorder.Now);

        Assert.That(standing.LiftCount, Is.Zero, "no steps while standing");
        Assert.That(standing.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance));
        Assert.That(standing.HeadAngleDeviation, Is.LessThan(1f), "breathing only moves the head a little");
    }

    [UnityTest]
    public IEnumerator UnevenGroundKeepsTheGaitEven([Values(false, true)] bool running) {
        float startX = Stage.Stride.transform.position.x;
        Stage.AddBumps((startX + 3f, startX + 60f), Bumps);
        if (running) {
            Stage.Press(Key.D, Key.LeftShift);
        }
        else {
            Stage.Press(Key.D);
        }
        yield return Hold(running ? 5f : 8f);
        GaitMetrics bumpy = Measure(running ? "run over bumps" : "walk over bumps", 2f, Recorder.Now);

        Assert.That(bumpy.EndsGrounded, Is.True, "still on its feet");
        Assert.That(bumpy.RepeatedLegLifts, Is.Zero, "legs alternate");
        Assert.That(bumpy.PhaseResyncs, Is.Zero, "no emergency re-phasing");
        Assert.That(bumpy.WorstPlantedFootGap, Is.LessThan(UnevenGroundFootTolerance), "feet reach the bumps they stand on");
    }

    [UnityTest]
    public IEnumerator JumpsFromAWalkAndARunLandBackIntoTheGait([Values(false, true)] bool running) {
        Stage.Press(running ? new[] { Key.D, Key.LeftShift } : new[] { Key.D });
        yield return Hold(2f);
        float jumpAt = Recorder.Now;
        yield return Tap(Key.Space);
        yield return Hold(2.5f);
        GaitMetrics jump = Measure(running ? "jump from run" : "jump from walk", jumpAt, Recorder.Now);
        GaitMetrics after = Measure("gait after the landing", Recorder.Now - 1.2f, Recorder.Now);

        Assert.That(jump.UngroundedFrames, Is.GreaterThan(10), "the character left the ground");
        Assert.That(after.EndsGrounded, Is.True, "and walks on after landing");
        AssertEvenGait((after, TransitionAsymmetry));
    }

    [UnityTest]
    public IEnumerator MovingStraightAfterALandingStepsEvenly() {
        yield return Hold(1f);
        yield return Tap(Key.Space);
        yield return WaitUntilGrounded(false);
        yield return WaitUntilGrounded(true);
        float landedAt = Recorder.Now;
        Stage.Press(Key.D);
        yield return Hold(2.5f);
        GaitMetrics afterLanding = Measure("move after landing", landedAt, Recorder.Now);

        Assert.That(afterLanding.EndsGrounded, Is.True);
        Assert.That(afterLanding.RepeatedLegLifts, Is.Zero, "legs alternate setting off from the landing");
        Assert.That(afterLanding.PhaseResyncs, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ClimbingALedgeHandsBackToTheGaitWithAnUprightPosture() {
        float bodyX = Stage.Stride.transform.position.x;
        float ledgeTop = Stage.AddLedge((bodyX + 1.6f, 2.5f, 30f));
        Stage.Press(Key.D);
        yield return Hold(1f);
        yield return Tap(Key.Space);
        yield return WaitUntilGrounded(false);
        float leanWhileClimbing = 0f;
        for (float elapsed = 0f; elapsed < 6f && !Stage.Stride.IsDrivingGait; elapsed += ScenarioStage.FrameTime) {
            leanWhileClimbing = Mathf.Max(leanWhileClimbing, Mathf.Abs(Stage.Inspector.ReadTelemetry().PostureLean));
            yield return null;
        }
        yield return Hold(1.5f);

        Assert.That(Stage.Stride.IsDrivingGait, Is.True, "walks again after the climb");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ledgeTop).Within(0.05f), "standing on top of the ledge");
        Assert.That(Stage.Inspector.ReadTelemetry().PostureLean, Is.GreaterThan(1f), "walking on, the posture leans again");
        Assert.That(leanWhileClimbing, Is.LessThan(6f), "the walking lean relaxes during the climb");
    }

    [UnityTest]
    public IEnumerator AimingWhileWalkingLooksAlongTheAimAndKeepsTheGait() {
        Stage.Press(Key.D);
        yield return Hold(2f);
        GaitMetrics walking = Measure("walking", 1f, Recorder.Now);

        float aimStart = Recorder.Now;
        for (float elapsed = 0f; elapsed < 2.5f; elapsed += ScenarioStage.FrameTime) {
            Vector2 body = Stage.Stride.transform.position;
            Stage.AimAt(body + new Vector2(6f, 4f));
            yield return null;
        }
        Stage.StopAiming();
        GaitMetrics aiming = Measure("walking while aiming up", aimStart + 0.5f, Recorder.Now);

        Assert.That(aiming.MeanHeadAngle, Is.GreaterThan(walking.MeanHeadAngle + 8f), "the head looks up along the aim");
        Assert.That(aiming.MeanSpeed, Is.LessThanOrEqualTo(Stage.Stride.Walk.speed + 0.01f), "aiming walks");
        AssertEvenGait((aiming, SteadyAsymmetry));
    }

    private IEnumerator WaitUntilGrounded(bool grounded) {
        for (float waited = 0f; waited < 3f && Stage.Stride.IsDrivingGait != grounded; waited += ScenarioStage.FrameTime) {
            yield return null;
        }
        Assert.That(Stage.Stride.IsDrivingGait, Is.EqualTo(grounded), grounded ? "landed" : "took off");
    }

    private IEnumerator Tap(Key key) {
        Stage.Press(key);
        yield return null;
        Stage.Release(key);
    }
}
