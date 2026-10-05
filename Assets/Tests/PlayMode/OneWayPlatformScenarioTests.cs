using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// One-way platforms on the real character, built for each test: it jumps up through one from below and lands on top,
// walks along one and off its end, walks through a low one from the side without stepping onto it, and drops through
// one with down + jump. Its feet only ever plant on the ground or on a top it stands on.
public class OneWayPlatformScenarioTests : ScenarioFixture {
    private const float StanceTolerance = 0.05f;

    private readonly List<Vector2> plants = new List<Vector2>();

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    private void RecordFootPlants() {
        plants.Clear();
        Stage.Character.Events.FootPlanted += plant => plants.Add(plant.position);
    }

    // Every recorded plant on the ground or on one of the given tops: never under a platform or inside one.
    private void AssertPlantsOnlyOn(params float[] tops) {
        foreach (Vector2 plant in plants) {
            bool onATop = Mathf.Abs(plant.y - ScenarioStage.GroundTop) <= StanceTolerance;
            foreach (float top in tops) {
                onATop |= Mathf.Abs(plant.y - top) <= StanceTolerance;
            }
            Assert.That(onATop, Is.True, $"a foot planted at {plant} (ground {ScenarioStage.GroundTop:F2}, tops {string.Join(", ", tops)})");
        }
    }

    private IEnumerator FaceRight() {
        Stage.Press(Key.D);
        yield return Hold(0.1f);
        Stage.Release(Key.D);
        yield return Hold(0.6f);
    }

    [UnityTest]
    public IEnumerator JumpingUpThroughAPlatformFromBelowLandsOnItsTop() {
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x - 3f, 1.5f, 6f));
        yield return Hold(0.5f);
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(StanceTolerance), "stands on the ground under it");
        RecordFootPlants();

        Stage.Press(Key.Space);
        yield return Hold(0.35f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => Stage.Stride.IsDrivingGait, 3f);
        yield return Hold(0.5f);

        Assert.That(State, Is.InstanceOf<WalkState>(), "landed");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(StanceTolerance), "on the platform");
        AssertPlantsOnlyOn(top);
    }

    [UnityTest]
    public IEnumerator WalkingAlongAPlatformHoldsAndWalkingOffItsEndFalls() {
        yield return FaceRight();
        float startX = Body.x;
        const float WidthAhead = 4f;
        Stage.PlaceOnPlatform((3f, WidthAhead), true);
        float top = ScenarioStage.GroundTop + 3f;
        yield return Hold(0.5f);
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(StanceTolerance), "stands on the platform");
        RecordFootPlants();

        Stage.Press(Key.D);
        bool heldAlongIt = true;
        float walkedOnTop = 0f;
        for (float elapsed = 0f; elapsed < 6f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            if (Stage.Stride.IsDrivingGait && Body.x < startX + WidthAhead - 1f) {
                heldAlongIt &= Mathf.Abs(Stage.Stride.StanceHeight - top) <= StanceTolerance;
                walkedOnTop = Mathf.Max(walkedOnTop, Body.x - startX);
            }
            if (Body.x > startX + WidthAhead + 1f && Stage.Stride.IsDrivingGait && Mathf.Abs(Stage.Stride.StanceHeight - ScenarioStage.GroundTop) <= StanceTolerance) {
                break;
            }
        }
        Stage.Release(Key.D);
        yield return Hold(0.3f);

        Report($"walked {walkedOnTop:F2} along the platform");
        Assert.That(heldAlongIt, Is.True, "the platform holds the feet all along");
        Assert.That(walkedOnTop, Is.GreaterThan(WidthAhead - 1.5f), "walked across it");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(StanceTolerance), "walked off its end and landed below");
        AssertPlantsOnlyOn(top);
    }

    [UnityTest]
    public IEnumerator WalkingIntoALowPlatformFromTheSidePassesThroughIt() {
        yield return FaceRight();
        float x = Body.x;
        float top = Stage.AddOneWayPlatform((x + 2f, 0.6f, 3f));
        RecordFootPlants();

        Stage.Press(Key.D);
        bool stayedOnTheGround = true;
        for (float elapsed = 0f; elapsed < 3f && Body.x < x + 6.5f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            stayedOnTheGround &= !Stage.Stride.IsDrivingGait || Mathf.Abs(Stage.Stride.StanceHeight - ScenarioStage.GroundTop) <= StanceTolerance;
        }
        Stage.Release(Key.D);

        Assert.That(Body.x, Is.GreaterThan(x + 6f), "walked through to the far side");
        Assert.That(stayedOnTheGround, Is.True, "never stepped up onto it from the side");
        Assert.That(State, Is.InstanceOf<WalkState>());
        AssertPlantsOnlyOn();
        Assert.That(top, Is.GreaterThan(ScenarioStage.GroundTop));
    }

    [UnityTest]
    public IEnumerator DownAndJumpDropsThroughThePlatformToTheGroundBelow() {
        yield return FaceRight();
        Stage.PlaceOnPlatform((3f, 4f), true);
        yield return Hold(0.5f);
        int drops = 0;
        int jumps = 0;
        Stage.Character.Events.DroppedThroughPlatform += _ => drops++;
        Stage.Character.Events.Jumped += _ => jumps++;

        Stage.Press(Key.S, Key.Space);
        yield return null;
        Stage.Release(Key.Space);
        yield return WaitUntil(() => !Stage.Stride.IsDrivingGait, 0.5f);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "fell through");
        yield return WaitUntil(() => Stage.Stride.IsDrivingGait, 3f);
        Stage.Release(Key.S);
        yield return Hold(0.3f);

        Assert.That(drops, Is.EqualTo(1), "one drop-through");
        Assert.That(jumps, Is.Zero, "no jump");
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(StanceTolerance), "on the ground below");
    }

    [UnityTest]
    public IEnumerator DownAndJumpOnSolidGroundStillJumps() {
        yield return Hold(0.5f);
        int jumps = 0;
        int drops = 0;
        Stage.Character.Events.Jumped += _ => jumps++;
        Stage.Character.Events.DroppedThroughPlatform += _ => drops++;

        Stage.Press(Key.S, Key.Space);
        yield return null;
        Stage.Release(Key.Space);
        yield return Hold(0.4f);
        Stage.Release(Key.S);

        Assert.That(jumps, Is.EqualTo(1));
        Assert.That(drops, Is.Zero);
    }
}
