using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// A crate pushed along flat ground into the foot of a 45 degree ramp (a Hill built for each test). The push is slope
// aware: at the ramp it pushes along the ramp with the crate's weight along it carried, so light crates ride up the ramp
// and over its top with the body climbing behind them, while a crate too heavy for the push's strength stalls at the ramp's
// foot with the body standing still at it (no jitter), as a very heavy crate does on flat ground.
public class PushRampScenarioTests : ScenarioFixture {
    private static readonly Vector2 CrateSize = new Vector2(2.4f, 2.4f);
    private const float HillHeight = 4.5f;
    private const float RampAngle = 45f;
    private const float HillTop = 8f;
    // Too heavy for the push on a ramp (its weight along 45 degrees alone passes the push strength), light enough on the flat.
    private const float HeavyMass = 12f;

    private Vector2 Body => Stage.Stride.transform.position;
    private CratePusher Pusher => Stage.Character.Parts.Pusher;

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator ALightCrateIsPushedUpA45DegreeRampAndOntoItsTopWithTheBodyBehindIt([Values(0.6f, 3f)] float mass) {
        yield return Hold(0.5f);
        Hill hill = Stage.AddHill((Body.x + 9f, HillHeight, RampAngle, HillTop));
        Rigidbody2D crate = Stage.AddCrate((Body.x + 2.5f, 1), (CrateSize, mass));
        float restingY = crate.position.y;
        yield return Hold(0.3f);

        Stage.Press(Key.D);
        float highest = restingY;
        int stops = 0;
        Stage.Character.Events.PushStopped += () => stops++;
        yield return WaitUntil(() => {
            highest = Mathf.Max(highest, crate.position.y);
            return crate.position.x > hill.TopStartX + CrateSize.x;
        }, 12f);
        yield return Hold(0.5f);

        float topCentreY = hill.TopY + CrateSize.y / 2f;
        Report($"mass {mass}: crate ({crate.position.x:F2},{crate.position.y:F2}) top centre y {topCentreY:F2}, highest {highest:F2}, body ({Body.x:F2},{Body.y:F2}), pushing {Pusher.IsPushing}, stops {stops}");
        Assert.That(highest, Is.GreaterThan(topCentreY - 0.15f), "the crate was pushed up the ramp to its top");
        Assert.That(crate.position.x, Is.GreaterThan(hill.TopStartX + CrateSize.x), "and along the top");
        Assert.That(Body.y, Is.GreaterThan(hill.TopY - 0.5f + 2.4f), "the body climbed the ramp behind it");
        Assert.That(Body.x, Is.GreaterThan(hill.TopStartX - 0.5f), "to the top");
    }

    [UnityTest]
    public IEnumerator ACrateTooHeavyForThePushStallsAtTheRampsFootWithTheBodyStandingStill() {
        yield return Hold(0.5f);
        Hill hill = Stage.AddHill((Body.x + 9f, HillHeight, RampAngle, HillTop));
        Rigidbody2D crate = Stage.AddCrate((Body.x + 2.5f, 1), (CrateSize, HeavyMass));
        yield return Hold(0.3f);

        Stage.Press(Key.D);
        yield return WaitUntil(() => Pusher.IsPushing && crate.position.x + CrateSize.x / 2f > hill.FootX - 0.1f, 12f);
        yield return Hold(1.5f);

        float crateStart = crate.position.x;
        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        bool heldOn = true;
        for (float elapsed = 0f; elapsed < 1.5f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            lowest = Mathf.Min(lowest, Body.x);
            highest = Mathf.Max(highest, Body.x);
            heldOn &= Pusher.IsPushing;
        }
        Report($"crate x {crateStart:F2} to {crate.position.x:F2}, body x range {highest - lowest:F4}, pushing the whole time {heldOn}, foot {hill.FootX:F2}");
        Assert.That(crate.position.x + CrateSize.x / 2f, Is.LessThan(hill.FootX + 0.6f), "the crate stays at the ramp's foot");
        Assert.That(Mathf.Abs(crate.position.x - crateStart), Is.LessThan(0.05f), "and does not creep");
        Assert.That(highest - lowest, Is.LessThan(0.02f), "the body stands still at it");
        Assert.That(Stage.Character.CurrentState, Is.InstanceOf<WalkState>(), "still on its feet");
    }
}
