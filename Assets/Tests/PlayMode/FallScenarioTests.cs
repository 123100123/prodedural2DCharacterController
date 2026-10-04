using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Falls on the real character: a short fall or a normal jump is left alone, a long fall gradually brings in the
// secondary motion, and aiming gives the arms back.
public class FallScenarioTests : ScenarioFixture {
    private const float PlatformHeight = 60f;

    private readonly List<AirSnapshot> flight = new List<AirSnapshot>();

    // Follows the flight from its first frame until it lands (or the frames run out).
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

    private IEnumerator WalkOffThePlatform() {
        Stage.PlaceOnPlatform((PlatformHeight, 3f));
        yield return Hold(1.5f);
        Assert.That(Stage.Stride.IsDrivingGait, Is.True, "standing on the platform");
        Stage.Press(Key.D);
    }

    [UnityTest]
    public IEnumerator AJumpHasNoFlailAtAll() {
        yield return Hold(1.5f);
        Stage.Press(Key.Space);
        yield return FollowFlight(200, frame => {
            if (frame == 20) {
                Stage.Release(Key.Space);
            }
        });
        Assert.That(flight.Count, Is.GreaterThan(30));
        foreach (AirSnapshot air in flight) {
            Assert.That(air.FlailStrength, Is.EqualTo(0f), "a normal jump never reaches the onset");
        }
    }

    [UnityTest]
    public IEnumerator ALongFallBuildsTheFlailUpGradually() {
        yield return WalkOffThePlatform();
        yield return FollowFlight(400);
        Assert.That(flight.Count, Is.GreaterThan(80), "a long fall");

        float previous = 0f;
        foreach (AirSnapshot air in flight) {
            if (air.TimeFalling < 0.45f) {
                Assert.That(air.FlailStrength, Is.EqualTo(0f), $"nothing before the onset (t={air.TimeFalling:F2})");
            }
            if (air.LandingPrep <= 0f) {
                Assert.That(air.FlailStrength, Is.GreaterThanOrEqualTo(previous - 0.001f), "never weakens until the ground nears");
            }
            Assert.That(air.FlailStrength - previous, Is.LessThan(0.05f), "builds smoothly");
            previous = Mathf.Max(previous, air.FlailStrength);
        }
        Assert.That(previous, Is.GreaterThan(0.6f), "ends up flailing hard");
    }

    [UnityTest]
    public IEnumerator AimingInALongFallGivesTheArmsToTheAim() {
        yield return WalkOffThePlatform();
        bool sawStrongFlail = false;
        float flailWhileAiming = 1f;
        yield return FollowFlight(400, frame => {
            LocomotionTelemetry t = Stage.Inspector.Telemetry;
            if (!sawStrongFlail) {
                sawStrongFlail = t.Air.FlailStrength > 0.5f;
            }
            else {
                Stage.AimAt((Vector2)Stage.Stride.transform.position + new Vector2(6f, 2f));
                flailWhileAiming = Mathf.Min(flailWhileAiming, t.Air.FlailStrength);
            }
        });
        Stage.StopAiming();
        Assert.That(sawStrongFlail, Is.True);
        Assert.That(flailWhileAiming, Is.LessThan(0.05f), "the flail gives way to the aim");
    }
}
