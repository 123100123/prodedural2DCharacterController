using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The playground the Build Playground menu authored into the saved scene (the stage normally swaps the level for a flat
// track, so these keep the scene's own level): it is there with its crates, targets and one-way platforms, and the
// runway of it can be walked.
public class PlaygroundScenarioTests : ScenarioFixture {
    private const string RootName = "Playground";

    private Vector2 Body => Stage.Stride.transform.position;

    protected override bool KeepsTheLevel => true;

    private static Transform Playground() {
        GameObject root = GameObject.Find(RootName);
        Assert.That(root, Is.Not.Null, "the saved scene has a Playground root (run Tools > Procedural Character > Build Playground)");
        return root.transform;
    }

    [Test]
    public void TheSceneHasThePlaygroundWithItsCratesTargetsAndPlatforms() {
        Transform root = Playground();
        Assert.That(root.GetComponentsInChildren<PushableCrate>(true).Length, Is.GreaterThanOrEqualTo(2), "crates");
        Assert.That(root.GetComponentsInChildren<ShootingTarget>(true).Length, Is.GreaterThanOrEqualTo(3), "targets");
        Assert.That(root.GetComponentsInChildren<OneWayPlatform>(true).Length, Is.GreaterThanOrEqualTo(2), "one-way platforms");
        Assert.That(root.Find("Playground Lamp"), Is.Not.Null, "a lamp");
    }

    [Test]
    public void ThereIsOnePlaygroundRoot() {
        int roots = 0;
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (candidate.name == RootName && candidate.transform.parent == null) {
                roots++;
            }
        }
        Assert.That(roots, Is.EqualTo(1));
    }

    [Test]
    public void TheCratesDifferInMassAndNoTargetBlocksTheWalkingHeight() {
        Transform root = Playground();
        PushableCrate[] crates = root.GetComponentsInChildren<PushableCrate>(true);
        Assert.That(crates[0].GetComponent<Rigidbody2D>().mass, Is.Not.EqualTo(crates[1].GetComponent<Rigidbody2D>().mass), "a light and a heavy crate");

        float floorTop = root.Find("Floor").GetComponent<BoxCollider2D>().bounds.max.y;
        const float StandingHeight = 4.4f;
        foreach (ShootingTarget target in root.GetComponentsInChildren<ShootingTarget>(true)) {
            Assert.That(target.GetComponent<BoxCollider2D>().bounds.min.y, Is.GreaterThan(floorTop + StandingHeight), target.name + " hangs above head height");
        }
    }

    [UnityTest]
    public IEnumerator WalkingAlongTheRunwayPastTheCratesAndUnderTheTargetsGetsNowhereStuck() {
        Transform floor = Playground().Find("Floor");
        Bounds bounds = floor.GetComponent<BoxCollider2D>().bounds;
        float startX = bounds.min.x + 30f;
        Stage.Character.transform.position += new Vector3(startX - Body.x, bounds.max.y - ScenarioStage.GroundTop + 0.1f, 0f);
        Stage.Stride.TrySettleStance((startX, 1, bounds.max.y));
        Stage.Stride.BeginGroundedMotion(Vector2.zero);
        yield return Hold(0.6f);

        Stage.Press(Key.D);
        yield return Hold(4f);
        Report($"walked from {startX:F1} to {Body.x:F1}");
        Assert.That(Body.x, Is.GreaterThan(startX + 8f), "walked on through the targets' stretch");
        Assert.That(Body.y, Is.EqualTo(bounds.max.y).Within(3f), "on the floor");
    }

    [Test]
    public void TheOneWayPlatformsAreAStaircaseOfSixWideSlabsWithALighterEdgeAndNoColliderOnIt() {
        Transform root = Playground();
        float floorTop = root.Find("Floor").GetComponent<BoxCollider2D>().bounds.max.y;
        float previousTop = floorTop;
        foreach (string name in new[] { "One Way Low", "One Way Mid", "One Way High" }) {
            Transform platform = root.Find(name);
            Assert.That(platform, Is.Not.Null, name);
            Bounds bounds = platform.GetComponent<BoxCollider2D>().bounds;
            Assert.That(bounds.size.x, Is.GreaterThanOrEqualTo(5.9f), name + " is wide");
            Assert.That(bounds.max.y - previousTop, Is.EqualTo(1.6f).Within(0.05f), name + " is one rise above the one before");
            previousTop = bounds.max.y;

            Transform edge = platform.Find(name + " Edge");
            Assert.That(edge, Is.Not.Null, name + " has an edge strip");
            Assert.That(edge.GetComponent<Collider2D>(), Is.Null, "the strip is only a picture");
            SpriteRenderer strip = edge.GetComponent<SpriteRenderer>();
            Assert.That(strip.bounds.max.y, Is.EqualTo(bounds.max.y).Within(0.01f), "along the top");
            Assert.That(strip.bounds.size.y, Is.LessThan(0.25f), "thin");
            Assert.That(strip.color.grayscale, Is.GreaterThan(platform.GetComponent<SpriteRenderer>().color.grayscale), "lighter than the slab");
        }
    }

    // Stands the character on the floor at x, as the walking test does.
    private void StandOnTheFloorAt(float x, float floorTop) {
        Stage.Character.transform.position += new Vector3(x - Body.x, floorTop - ScenarioStage.GroundTop + 0.1f, 0f);
        Stage.Stride.TrySettleStance((x, 1, floorTop));
        Stage.Stride.BeginGroundedMotion(Vector2.zero);
    }

    private bool StandsOn(float top) {
        return Stage.Character.CurrentState is WalkState && Stage.Stride.IsDrivingGait && Mathf.Abs(Stage.Stride.StanceHeight - top) <= 0.05f;
    }

    // One hop of a player: walk up to the launch point, jump holding the button and the direction, let go of the
    // direction once over the platform, and wait for it to stand on the top.
    private IEnumerator JumpOnto(Collider2D platform, float launchX) {
        Bounds bounds = platform.bounds;
        Stage.Press(Key.D);
        for (float elapsed = 0f; elapsed < 6f && Body.x < launchX; elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
        Stage.Press(Key.Space);
        for (float elapsed = 0f; elapsed < 4f && !StandsOn(bounds.max.y); elapsed += ScenarioStage.FrameTime) {
            if (elapsed > 0.5f) {
                Stage.Release(Key.Space);
            }
            if (Body.x > bounds.min.x + 1.5f) {
                Stage.Release(Key.D);
            }
            yield return null;
        }
        Stage.ReleaseAll();
        yield return Hold(0.5f);
        Report($"{platform.name}: body {Body}, stance {Stage.Stride.StanceHeight:F2}, top {bounds.max.y:F2}");
        Assert.That(StandsOn(bounds.max.y), Is.True, $"stands on {platform.name} after jumping onto it");
        Assert.That(Body.x, Is.InRange(bounds.min.x, bounds.max.x), $"over {platform.name}");
    }

    [UnityTest]
    public IEnumerator FromTheFloorJumpsAloneReachAndStandOnTheLowThenTheMidThenTheHighPlatform() {
        Transform root = Playground();
        float floorTop = root.Find("Floor").GetComponent<BoxCollider2D>().bounds.max.y;
        Collider2D low = root.Find("One Way Low").GetComponent<Collider2D>();
        Collider2D mid = root.Find("One Way Mid").GetComponent<Collider2D>();
        Collider2D high = root.Find("One Way High").GetComponent<Collider2D>();
        StandOnTheFloorAt(low.bounds.min.x - 3f, floorTop);
        yield return Hold(0.6f);

        yield return JumpOnto(low, low.bounds.min.x - 1.5f);
        yield return JumpOnto(mid, low.bounds.max.x - 0.6f);
        yield return JumpOnto(high, mid.bounds.max.x - 0.6f);
    }
}
