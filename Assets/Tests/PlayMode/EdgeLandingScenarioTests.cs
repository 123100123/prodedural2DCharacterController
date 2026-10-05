using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Falls onto ground narrower than the stance or ending just past the body: the feet whose stance spots are past the edge
// take the last ground toward the body, so the character stands on the top instead of falling on through a one-way
// platform (its hull never touches one) or hanging on a solid block's corner. The character faces right.
public class EdgeLandingScenarioTests : ScenarioFixture {
    private const float StanceTolerance = 0.05f;
    private const float PlatformHeight = 2f;
    // Lifts the standing character this far: its feet start a unit above the platform's top.
    private const float LiftHeight = PlatformHeight + 1f;
    private const float WidePlatform = 6f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // Falls straight down from the lift onto whatever was built under the body, then stands a while.
    private IEnumerator FallAndSettle() {
        yield return WaitUntil(() => !(State is WalkState), 0.5f);
        yield return WaitUntil(() => State is WalkState && Stage.Stride.IsDrivingGait, 3f);
        yield return Hold(1f);
    }

    private void AssertStandsOn(float top) {
        Report($"body {Body}, stance {Stage.Stride.StanceHeight:F3}, top {top:F3}, state {State.GetType().Name}");
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed and stands");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(StanceTolerance), "on the platform's top, not the ground below");
        Assert.That(Body.y, Is.GreaterThan(top + 0.5f * Stage.Stride.BodyHeight), "the body stands above it");
    }

    [UnityTest]
    public IEnumerator ALandingOnAOneWayPlatformNarrowerThanTheStanceHolds() {
        yield return Hold(0.5f);
        float x = Body.x;
        Stage.Lift(LiftHeight);
        const float Width = 0.3f;
        float top = Stage.AddOneWayPlatform((x - Width / 2f, PlatformHeight, Width));

        yield return FallAndSettle();
        AssertStandsOn(top);
    }

    // inset: how far inside the platform's end the body comes down; side: +1 the end is ahead of the character (its lead
    // foot's spot is past it), -1 behind it.
    [UnityTest]
    public IEnumerator ALandingJustInsideAOneWayPlatformsEndHolds([Values(0.05f, 0.2f)] float inset, [Values(1, -1)] int side) {
        yield return Hold(0.5f);
        float x = Body.x;
        Stage.Lift(LiftHeight);
        float endX = x + side * inset;
        float top = Stage.AddOneWayPlatform((side > 0 ? endX - WidePlatform : endX, PlatformHeight, WidePlatform));

        yield return FallAndSettle();
        AssertStandsOn(top);
    }

    [UnityTest]
    public IEnumerator ALandingJustInsideASolidBlocksEndStandsOnItsTop() {
        yield return Hold(0.5f);
        float x = Body.x;
        Stage.Lift(LiftHeight);
        const float Inset = 0.05f;
        float top = Stage.AddLedge((x + Inset - WidePlatform, PlatformHeight, WidePlatform));

        yield return FallAndSettle();
        AssertStandsOn(top);
    }
}
