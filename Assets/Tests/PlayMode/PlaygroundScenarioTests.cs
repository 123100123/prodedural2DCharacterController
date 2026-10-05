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
}
