using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Landings on the real character: the legs reach for the ground before touchdown, a hard landing squashes more than
// a light one, and a running jump lands and carries on running without the gait starting over.
public class LandingScenarioTests : ScenarioFixture {
    private readonly List<AirSnapshot> flight = new List<AirSnapshot>();

    private IEnumerator FollowFlight(int maxFrames, System.Action<int> everyFrame = null) {
        flight.Clear();
        for (int frame = 0; frame < maxFrames; frame++) {
            yield return null;
            LocomotionTelemetry t = Stage.Inspector.ReadTelemetry();
            if (t.Air.IsAirborne) {
                flight.Add(t.Air);
                everyFrame?.Invoke(frame);
            }
            else if (flight.Count > 0) {
                yield break;
            }
        }
    }

    // How low the pelvis dips below its settled standing height in the second after the landing.
    private IEnumerator MeasureDipAfterLanding() {
        float lowest = float.MaxValue;
        for (int frame = 0; frame < 60; frame++) {
            lowest = Mathf.Min(lowest, Stage.Stride.transform.position.y - Stage.Stride.StanceHeight);
            yield return null;
        }
        dip = lowest;
    }

    private float dip;

    [UnityTest]
    public IEnumerator TheLegsReachForTheGroundBeforeTouchdown() {
        yield return Hold(1.5f);
        Stage.Press(Key.Space);
        yield return FollowFlight(300, frame => {
            if (frame == 25) {
                Stage.Release(Key.Space);
            }
        });
        float highest = 0f;
        foreach (AirSnapshot air in flight) {
            highest = Mathf.Max(highest, air.LandingPrep);
        }
        Assert.That(highest, Is.GreaterThan(0.9f), "the reach is complete as the feet arrive");
        Assert.That(flight[flight.Count / 3].LandingPrep, Is.EqualTo(0f), "and nothing while the ground is far");
    }

    [UnityTest]
    public IEnumerator AHardLandingSquashesMoreThanALightOne() {
        yield return Hold(1.5f);
        Stage.Press(Key.Space);
        yield return FollowFlight(300, frame => {
            if (frame == 1) {
                Stage.Release(Key.Space);
            }
        });
        yield return MeasureDipAfterLanding();
        float lightDip = dip;
        float lightImpact = Stage.Inspector.ReadTelemetry().Air.LastImpactSpeed;
        Stage.Press(Key.Space);
        yield return null;
        Stage.Release(Key.Space);
        yield return Hold(1f);

        Stage.PlaceOnPlatform((30f, 3f));
        yield return Hold(1.5f);
        Stage.Press(Key.D);
        yield return FollowFlight(600);
        Stage.Release(Key.D);
        yield return MeasureDipAfterLanding();
        float hardDip = dip;
        float hardImpact = Stage.Inspector.ReadTelemetry().Air.LastImpactSpeed;

        Report($"light: impact {lightImpact:F1}, dip {lightDip:F2}; hard: impact {hardImpact:F1}, dip {hardDip:F2}");
        Assert.That(hardImpact, Is.GreaterThan(lightImpact * 1.5f));
        Assert.That(hardDip, Is.LessThan(lightDip - 0.15f), "the pelvis sinks lower after a hard landing");
    }

    [UnityTest]
    public IEnumerator ARunningJumpLandsAndCarriesOnRunningWithoutStartingTheGaitOver() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(3f);
        float before = Mathf.Abs(Stage.Stride.GroundSpeed);

        Stage.Press(Key.Space);
        float landedAt = 0f;
        yield return FollowFlight(400, frame => {
            if (frame == 15) {
                Stage.Release(Key.Space);
            }
        });
        landedAt = Recorder.Now;
        yield return Hold(1.5f);
        GaitMetrics after = Measure("after the running landing", landedAt, Recorder.Now);

        Assert.That(Mathf.Abs(Stage.Stride.GroundSpeed), Is.GreaterThan(before * 0.8f), "still running");
        Assert.That(after.LiftCount, Is.GreaterThan(3), "stepping on at once");
        Assert.That(after.PhaseResyncs, Is.Zero, "no emergency re-phasing");
        Assert.That(after.RepeatedLegLifts, Is.Zero, "legs alternate from the landing foot");
        Assert.That(after.EndsGrounded, Is.True);
    }

    [UnityTest]
    public IEnumerator AStandingLandingStillSettlesIntoAQuietStance() {
        yield return Hold(1.5f);
        Stage.Press(Key.Space);
        float landedAt = 0f;
        yield return FollowFlight(300, frame => {
            if (frame == 25) {
                Stage.Release(Key.Space);
            }
        });
        landedAt = Recorder.Now;
        yield return Hold(2.5f);
        GaitMetrics after = Measure("after the standing landing", landedAt + 1f, Recorder.Now);

        Assert.That(after.LiftCount, Is.LessThanOrEqualTo(2), "at most a settling step");
        Assert.That(after.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance));
    }

    [UnityTest]
    public IEnumerator LandingWithCrouchHeldContinuesCrouched() {
        Stage.Press(Key.LeftCtrl);
        yield return Hold(1.5f);
        Stage.Press(Key.Space);
        yield return FollowFlight(300, frame => {
            if (frame == 20) {
                Stage.Release(Key.Space);
            }
        });
        yield return Hold(0.8f);
        Assert.That(Stage.Inspector.ReadTelemetry().CrouchShare, Is.GreaterThan(0.95f));
        Assert.That(Stage.Stride.IsDrivingGait, Is.True);
    }

    [UnityTest]
    public IEnumerator AHardLandingCostsSomeOfTheSpeed() {
        Stage.PlaceOnPlatform((40f, 30f));
        yield return Hold(1.5f);
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(1.2f);
        float speedAtTheEdge = 0f;
        float speedAtLanding = 0f;
        yield return FollowFlight(600, _ => {
            LocomotionTelemetry t = Stage.Inspector.Telemetry;
            speedAtTheEdge = speedAtTheEdge == 0f ? Mathf.Abs(t.Air.Velocity.x) : speedAtTheEdge;
            speedAtLanding = Mathf.Abs(t.Air.Velocity.x);
        });
        Report($"speed in the air at first {speedAtTheEdge:F2}, at the end {speedAtLanding:F2}");
        Assert.That(Mathf.Abs(Stage.Stride.GroundSpeed), Is.LessThan(speedAtLanding * 0.95f + 0.5f), "the landing cost some speed");
    }
}
