using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Jumps on the real character: they keep the speed the character had, a run jumps off its stride, a tap is a hop and a
// hold is the whole jump, and the air steering keeps the momentum.
public class JumpScenarioTests : ScenarioFixture {
    private readonly struct Flight {
        public Flight(float distance, float apexHeight, float launchSpeed, float speedAtApex, float airTime, bool landed) {
            Distance = distance;
            ApexHeight = apexHeight;
            LaunchSpeed = launchSpeed;
            SpeedAtApex = speedAtApex;
            AirTime = airTime;
            Landed = landed;
        }

        public float Distance { get; }
        public float ApexHeight { get; }
        public float LaunchSpeed { get; }
        public float SpeedAtApex { get; }
        public float AirTime { get; }
        public bool Landed { get; }
    }

    private Flight lastFlight;

    // Presses Space for the given number of frames (a tap is 1) and follows the flight until it lands: the horizontal
    // distance, apex height above the take-off point, launch speed and the speed at the top.
    private IEnumerator Jump(int spaceFrames, System.Action<int> everyFrameInAir = null) {
        Stage.Press(Key.Space);
        Vector2 takeoff = default;
        float apex = 0f;
        float launchSpeed = 0f;
        float speedAtApex = 0f;
        float airStart = 0f;
        bool left = false;
        for (int frame = 0; frame < 400; frame++) {
            if (frame == spaceFrames) {
                Stage.Release(Key.Space);
            }
            yield return null;
            Vector2 body = Stage.Stride.transform.position;
            LocomotionTelemetry t = Stage.Inspector.ReadTelemetry();
            if (!left && t.Air.IsAirborne) {
                left = true;
                takeoff = body;
                launchSpeed = t.Air.LaunchVelocity.x;
                airStart = Recorder.Now;
            }
            if (left && t.Air.IsAirborne) {
                everyFrameInAir?.Invoke(frame);
                if (body.y - takeoff.y > apex) {
                    apex = body.y - takeoff.y;
                    speedAtApex = t.Air.Velocity.x;
                }
            }
            if (left && !t.Air.IsAirborne && Stage.Stride.IsDrivingGait) {
                lastFlight = new Flight(body.x - takeoff.x, apex, launchSpeed, speedAtApex, Recorder.Now - airStart, true);
                yield break;
            }
        }
        lastFlight = new Flight(0f, apex, launchSpeed, speedAtApex, 0f, false);
    }

    private IEnumerator Prepare(params Key[] keys) {
        Stage.Press(keys);
        yield return Hold(keys.Length == 0 ? 1.5f : 2.5f);
    }

    [UnityTest]
    public IEnumerator AStandingJumpGoesStraightUp() {
        yield return Prepare();
        yield return Jump(8);
        Assert.That(lastFlight.Landed, Is.True);
        Assert.That(Mathf.Abs(lastFlight.Distance), Is.LessThan(0.5f));
        Assert.That(lastFlight.ApexHeight, Is.GreaterThan(1.8f), "a held jump rises high");
    }

    [UnityTest]
    public IEnumerator ARunningJumpGoesFurtherThanAWalkingJumpWhichGoesFurtherThanAStandingOne() {
        yield return Prepare();
        yield return Jump(8);
        float standing = lastFlight.Distance;
        yield return Hold(0.5f);

        yield return Prepare(Key.D);
        yield return Jump(8);
        float walking = lastFlight.Distance;
        Stage.ReleaseAll();
        yield return Hold(2f);

        yield return Prepare(Key.D, Key.LeftShift);
        yield return Jump(8);
        float running = lastFlight.Distance;

        Report($"standing {standing:F2}, walking {walking:F2}, running {running:F2}");
        Assert.That(walking, Is.GreaterThan(standing + 1.5f));
        Assert.That(running, Is.GreaterThan(walking * 1.8f));
    }

    [UnityTest]
    public IEnumerator ARunningJumpKeepsItsSpeedThroughTheFlightAndAfterLanding() {
        yield return Prepare(Key.D, Key.LeftShift);
        yield return Jump(8);
        Assert.That(lastFlight.LaunchSpeed, Is.GreaterThan(Stage.Stride.Run.speed), "the launch carries the run speed and a bit more");
        Assert.That(lastFlight.SpeedAtApex, Is.GreaterThan(lastFlight.LaunchSpeed * 0.9f), "holding forward keeps the momentum");
        yield return Hold(0.5f);
        Assert.That(Stage.Stride.GroundSpeed, Is.GreaterThan(Stage.Stride.Walk.speed), "carries on after the landing");
    }

    [UnityTest]
    public IEnumerator ARunningJumpHasNoStopForAWindup() {
        yield return Prepare(Key.D, Key.LeftShift);
        float before = Stage.Stride.GroundSpeed;
        float slowest = before;
        yield return Jump(8, _ => slowest = Mathf.Min(slowest, Stage.Inspector.ReadTelemetry().Air.Velocity.x));
        Assert.That(slowest, Is.GreaterThan(before * 0.85f), "the speed was never lost");
    }

    [UnityTest]
    public IEnumerator ATapIsAHopAndAHoldIsTheWholeJump() {
        yield return Prepare();
        yield return Jump(1);
        float hop = lastFlight.ApexHeight;
        yield return Hold(1f);
        yield return Jump(30);
        float full = lastFlight.ApexHeight;

        Report($"hop {hop:F2}, full {full:F2}");
        Assert.That(hop, Is.LessThan(1.3f), "a tap rises only a little");
        Assert.That(full, Is.GreaterThan(2.0f), "a hold rises the whole way");
    }

    [UnityTest]
    public IEnumerator ReleasingTheKeysInTheAirKeepsMostOfTheMomentum() {
        yield return Prepare(Key.D, Key.LeftShift);
        yield return Jump(8, frame => {
            if (frame == 9) {
                Stage.ReleaseAll();
            }
        });
        Assert.That(lastFlight.Landed, Is.True);
        Assert.That(lastFlight.Distance, Is.GreaterThan(5f), "still travels a long way");
    }

    [UnityTest]
    public IEnumerator SteeringBackwardsInTheAirBrakesAndReverses() {
        yield return Prepare(Key.D, Key.LeftShift);
        float apexSpeed = 0f;
        yield return Jump(8, frame => {
            if (frame == 9) {
                Stage.Release(Key.D, Key.LeftShift);
                Stage.Press(Key.A);
            }
            apexSpeed = Stage.Inspector.ReadTelemetry().Air.Velocity.x;
        });
        Assert.That(apexSpeed, Is.LessThan(0f), "ends heading back");
    }

    [UnityTest]
    public IEnumerator AJumpFromACrouchRisesALittleLessThanAStandingJump() {
        yield return Prepare();
        yield return Jump(30);
        float standing = lastFlight.ApexHeight;
        yield return Hold(1f);

        Stage.Press(Key.LeftCtrl);
        yield return Hold(1.5f);
        yield return Jump(30);
        float crouched = lastFlight.ApexHeight;

        Report($"standing {standing:F2}, crouched {crouched:F2}");
        Assert.That(crouched, Is.LessThan(standing));
        Assert.That(crouched, Is.GreaterThan(standing * 0.7f));
    }
}
