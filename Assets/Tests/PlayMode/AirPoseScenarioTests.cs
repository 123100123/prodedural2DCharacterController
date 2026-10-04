using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The flight pose on the real character: it moves from the push-off through the hang to the reach for the ground by
// the vertical speed, changes smoothly, and carries the speed the jump started with.
public class AirPoseScenarioTests : ScenarioFixture {
    private struct Frame {
        public AirSnapshot Air;
        public float LowestFootBelowBody;
        public Vector2 FrontFoot;
        public Vector2 BackFoot;
        public float PostureTargetLean;
    }

    private readonly List<Frame> flight = new List<Frame>();

    private IEnumerator Fly(int spaceFrames) {
        flight.Clear();
        Stage.Press(Key.Space);
        for (int frame = 0; frame < 300; frame++) {
            if (frame == spaceFrames) {
                Stage.Release(Key.Space);
            }
            yield return null;
            LocomotionTelemetry t = Stage.Inspector.ReadTelemetry();
            if (!t.Air.IsAirborne) {
                if (flight.Count > 0 && Stage.Stride.IsDrivingGait) {
                    yield break;
                }
                continue;
            }
            float bodyY = Stage.Stride.transform.position.y;
            flight.Add(new Frame {
                Air = t.Air,
                LowestFootBelowBody = Mathf.Min(t.Front.FootTarget.y, t.Back.FootTarget.y) - bodyY,
                FrontFoot = t.Front.FootTarget,
                BackFoot = t.Back.FootTarget,
                PostureTargetLean = t.PostureTargetLean,
            });
        }
    }

    [UnityTest]
    public IEnumerator AJumpMovesFromRisingThroughTheHangToFalling() {
        yield return Hold(1.5f);
        yield return Fly(30);
        Assert.That(flight.Count, Is.GreaterThan(30));

        Assert.That(flight[2].Air.Ascent, Is.GreaterThan(0.7f), "rising at first");
        int apexFrame = flight.FindIndex(frame => frame.Air.Velocity.y <= 0f);
        Assert.That(flight[apexFrame].Air.Apex, Is.GreaterThan(0.5f), "hanging at the top");
        Assert.That(flight[flight.Count - 4].Air.Descent, Is.GreaterThan(0.7f), "falling at the end");
    }

    [UnityTest]
    public IEnumerator TheLegsPushOffThenTuckAtTheTopAndReachDownAgain() {
        yield return Hold(1.5f);
        yield return Fly(30);
        float earlyReach = Mathf.Min(flight[1].LowestFootBelowBody, flight[3].LowestFootBelowBody);
        int apexFrame = flight.FindIndex(frame => frame.Air.Velocity.y <= 0f);
        float apexReach = flight[apexFrame].LowestFootBelowBody;
        float lateReach = flight[flight.Count - 5].LowestFootBelowBody;

        Report($"reach below the body: take-off {earlyReach:F2}, apex {apexReach:F2}, landing {lateReach:F2}");
        Assert.That(earlyReach, Is.LessThan(apexReach - 0.3f), "the back leg is extended while pushing off, drawn up at the top");
        Assert.That(lateReach, Is.LessThan(apexReach - 0.3f), "and reaching down for the ground again");
    }

    [UnityTest]
    public IEnumerator TheFeetNeverSnapBetweenFrames() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2.5f);
        yield return Fly(30);
        float worst = 0f;
        for (int index = 4; index < flight.Count - 6; index++) {
            worst = Mathf.Max(worst, Vector2.Distance(flight[index].FrontFoot, flight[index - 1].FrontFoot));
            worst = Mathf.Max(worst, Vector2.Distance(flight[index].BackFoot, flight[index - 1].BackFoot));
        }
        Report($"largest foot target step in flight: {worst:F3}");
        Assert.That(worst, Is.LessThan(0.5f), "the pose changes smoothly");
    }

    [UnityTest]
    public IEnumerator ARunningJumpLeansForwardMoreThanAStandingJump() {
        yield return Hold(1.5f);
        yield return Fly(30);
        float standing = flight[flight.Count / 2].PostureTargetLean;
        Stage.ReleaseAll();
        yield return Hold(1f);

        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2.5f);
        yield return Fly(30);
        float running = flight[flight.Count / 2].PostureTargetLean;

        Report($"lean at mid-flight: standing {standing:F1}, running {running:F1}");
        Assert.That(running, Is.GreaterThan(standing + 4f));
    }

    [UnityTest]
    public IEnumerator AShortHopPassesQuicklyThroughTheHang() {
        yield return Hold(1.5f);
        yield return Fly(1);
        Assert.That(flight.Count, Is.LessThan(60), "a hop is brief");
        Assert.That(flight[flight.Count / 2].Air.Ascent + flight[flight.Count / 2].Air.Apex + flight[flight.Count / 2].Air.Descent, Is.EqualTo(1f).Within(0.001f));
    }
}
