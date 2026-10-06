using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Landing on one-way platforms on the real character: a held jump from the floor reaches a platform up to the jump's own
// height, a hop reaches the next platform of a staircase, and a fall onto a platform from well above always lands on it,
// straight down or at run speed, however fast it falls (no tunneling through the thin top).
public class OneWayLandingScenarioTests : ScenarioFixture {
    private const float StanceTolerance = 0.05f;
    private const float PlatformWidth = 6f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;

    private static IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    private bool StandsOn(float top) => State is WalkState && Stage.Stride.IsDrivingGait && Mathf.Abs(Stage.Stride.StanceHeight - top) <= StanceTolerance;

    private void AssertStandsOn(float top, string what) {
        Report($"{what}: body {Body}, stance {Stage.Stride.StanceHeight:F3}, top {top:F3}, state {State.GetType().Name}");
        Assert.That(State, Is.InstanceOf<WalkState>(), what + ": landed and stands");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(StanceTolerance), what + ": on the platform's top");
        Assert.That(Body.y, Is.GreaterThan(top + 0.5f * Stage.Stride.BodyHeight), what + ": the body stands above it");
    }

    // A standing jump loads by sinking the pelvis, so its feet clear about 1.7; a jump off a run keeps the full 2.4.
    [UnityTest]
    public IEnumerator AHeldJumpFromTheFloorLandsOnAPlatformUpToTheJumpsHeight([Values(1.6f, 1.8f, 2f)] float height, [Values(true, false)] bool runningStart) {
        Assume.That(runningStart || height < 1.9f, "a standing jump does not clear a platform that high");
        yield return Hold(0.5f);
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x + (runningStart ? 4.5f : 1.5f), height, PlatformWidth));

        if (runningStart) {
            Stage.Press(Key.D, Key.LeftShift);
            yield return WaitUntil(() => Body.x > x + 2.5f, 3f);
        }
        Stage.Press(Key.D, Key.Space);
        yield return Hold(0.5f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => StandsOn(top), 3f);
        Stage.Release(Key.D);
        yield return Hold(0.4f);

        AssertStandsOn(top, $"jump onto {height}");
    }

    [UnityTest]
    public IEnumerator AHopFromOnePlatformToTheNextOneThatIsHigherLandsOnIt([Values(1.6f, 1.8f)] float rise) {
        const float LowHeight = 1.6f;
        const float Gap = 1.5f;
        float x = Body.x;
        const float WidthAhead = 4f;
        Stage.PlaceOnPlatform((LowHeight, WidthAhead), true);
        float top = Stage.AddOneWayPlatform((x + WidthAhead + Gap, LowHeight + rise, PlatformWidth));
        yield return Hold(0.6f);
        Assert.That(StandsOn(ScenarioStage.GroundTop + LowHeight), Is.True, "stands on the first platform");

        Stage.Press(Key.D);
        yield return WaitUntil(() => Body.x > x + WidthAhead - 1.6f, 3f);
        Stage.Press(Key.Space);
        yield return Hold(0.5f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => StandsOn(top), 3f);
        Stage.Release(Key.D);
        yield return Hold(0.4f);

        AssertStandsOn(top, $"hop up {rise}");
    }

    // Falls straight down from the given height above the platform's top, with the platform wide under the body.
    [UnityTest]
    public IEnumerator AFallStraightOntoAPlatformLands([Values(5f, 40f)] float fallHeight) {
        yield return Hold(0.5f);
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x - PlatformWidth / 2f, 1.6f, PlatformWidth));
        Stage.Lift(1.6f + fallHeight);

        yield return WaitUntil(() => !(State is WalkState), 0.5f);
        yield return WaitUntil(() => StandsOn(top) || Body.y < top - 1f, 6f);
        yield return Hold(0.4f);

        AssertStandsOn(top, $"fall of {fallHeight}");
    }

    // Falls from a height with D held: it drifts sideways at the air's top speed across a platform that spans the drop.
    [UnityTest]
    public IEnumerator AFallDriftingSidewaysOntoAPlatformLands([Values(5f, 40f)] float fallHeight, [Values(1, -1)] int side) {
        yield return Hold(0.5f);
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x - PlatformWidth * 2f, 1.6f, PlatformWidth * 4f));
        Stage.Lift(1.6f + fallHeight);

        Stage.Press(side > 0 ? Key.D : Key.A);
        yield return WaitUntil(() => !(State is WalkState), 0.5f);
        yield return WaitUntil(() => StandsOn(top) || Body.y < top - 1f, 6f);
        Stage.ReleaseAll();
        yield return Hold(0.4f);

        AssertStandsOn(top, $"drifting fall of {fallHeight}");
    }

    // Runs off the end of a solid ledge at run speed onto a one-way platform just below and ahead of it.
    [UnityTest]
    public IEnumerator RunningOffALedgeOntoAPlatformBelowLands([Values(3f, 8f, 20f)] float drop) {
        const float LedgeHeight = 24f;
        const float WidthAhead = 6f;
        yield return Hold(0.5f);
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x + WidthAhead - 0.5f, LedgeHeight - drop, PlatformWidth * 2f));
        Stage.PlaceOnPlatform((LedgeHeight, WidthAhead));
        yield return Hold(0.6f);

        Stage.Press(Key.D, Key.LeftShift);
        yield return WaitUntil(() => !(State is WalkState), 4f);
        yield return WaitUntil(() => StandsOn(top) || Body.y < top - 1f, 6f);
        Stage.ReleaseAll();
        yield return Hold(0.4f);

        AssertStandsOn(top, $"run off a ledge, dropping {drop}");
    }
}
