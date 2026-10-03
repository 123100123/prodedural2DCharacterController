using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RigValidationTests {
    private readonly List<GameObject> created = new List<GameObject>();

    private Transform Create(string name, Transform parent) {
        GameObject gameObject = new GameObject(name);
        created.Add(gameObject);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    [TearDown]
    public void DestroyCreated() {
        foreach (GameObject gameObject in created) {
            Object.DestroyImmediate(gameObject);
        }
        created.Clear();
    }

    // character / body (kinematic) / pelvis / chest, with the IK targets directly under the character.
    private (Transform root, Transform body, Transform pelvis, Transform chest, Transform target) Rig() {
        Transform root = Create("character", null);
        Transform body = Create("body", root);
        Transform pelvis = Create("pelvis", body);
        Transform chest = Create("chest", pelvis);
        Transform target = Create("footTarget", root);
        return (root, body, pelvis, chest, target);
    }

    [Test]
    public void APelvisPostureRootUnderTheBodyWithWorldSpaceTargetsIsValid() {
        var rig = Rig();
        Assert.That(RigValidation.Problems((rig.body, rig.pelvis, rig.chest, new[] { rig.target })), Is.Empty);
    }

    [Test]
    public void ReportsAMissingPostureRoot() {
        var rig = Rig();
        Assert.That(RigValidation.Problems((rig.body, null, rig.chest, new[] { rig.target })), Has.Count.EqualTo(1));
    }

    [Test]
    public void ReportsLeaningTheChestOrTheKinematicBody() {
        var rig = Rig();
        Assert.That(RigValidation.Problems((rig.body, rig.chest, rig.chest, new[] { rig.target })), Is.Not.Empty, "the chest itself");
        Assert.That(RigValidation.Problems((rig.body, rig.body, rig.chest, new[] { rig.target })), Is.Not.Empty, "the kinematic body");
    }

    [Test]
    public void ReportsAnIkTargetThatWouldLeanWithThePosture() {
        var rig = Rig();
        rig.target.SetParent(rig.pelvis, false);
        Assert.That(RigValidation.Problems((rig.body, rig.pelvis, rig.chest, new[] { rig.target })), Has.Count.EqualTo(1));
    }

    [Test]
    public void ReportsAChestOutsideThePosture() {
        var rig = Rig();
        rig.chest.SetParent(rig.body, false);
        Assert.That(RigValidation.Problems((rig.body, rig.pelvis, rig.chest, new[] { rig.target })), Has.Count.EqualTo(1));
    }
}
