using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// The level's shadow casters as authored in the scene (the test stage switches the blocks off, so inactive ones are
// included): every solid block has one, and it shadows the wall and the ruins
// behind it and the level in front, but not the distant scenery. They are read by name so this needs no URP reference;
// without the Universal Render Pipeline the scenarios are skipped.
public class ShadowCasterScenarioTests : ScenarioFixture {
    private const string CasterTypeName = "ShadowCaster2D";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static List<Component> Casters() {
        List<Component> casters = new List<Component>();
        foreach (Component component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (component.GetType().Name == CasterTypeName) {
                casters.Add(component);
            }
        }
        return casters;
    }

    private static int[] TargetLayers(Component caster) {
        FieldInfo field = caster.GetType().GetField("m_ApplyToSortingLayers", Private);
        return (int[])field.GetValue(caster);
    }

    private static void RequireUrp() {
        if (GraphicsSettings.currentRenderPipeline == null) {
            Assert.Ignore("The Universal Render Pipeline is not set up.");
        }
    }

    [UnityTest]
    public IEnumerator EveryLevelBlockHasACasterThatShadowsTheWallTheRuinsAndTheLevel() {
        RequireUrp();
        yield return null;
        List<Component> casters = Casters();
        int blocks = 0;
        foreach (BoxCollider2D box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            bool isLevelBlock = !box.isTrigger && box.GetComponent<SpriteRenderer>() != null;
            if (!isLevelBlock) {
                continue;
            }
            blocks++;
            Component caster = casters.Find(c => c.gameObject == box.gameObject);
            Assert.That(caster, Is.Not.Null, box.name + " has a shadow caster");

            int[] targets = TargetLayers(caster);
            foreach (string name in EnvironmentLayers.ShadowedByBlocks) {
                Assert.That(targets, Has.Member(SortingLayer.NameToID(name)), $"{box.name} shadows {name}");
            }
            Assert.That(targets, Has.No.Member(SortingLayer.NameToID(EnvironmentLayers.Far)), box.name + " leaves the far scenery lit");
        }
        Assert.That(blocks, Is.GreaterThan(5), "the level has its blocks");
    }

    [UnityTest]
    public IEnumerator TheCharacterCastsNoShadow() {
        RequireUrp();
        yield return null;
        foreach (Component caster in Casters()) {
            Assert.That(caster.GetComponentInParent<CharacterDriver>(), Is.Null, caster.name + " is not part of the character");
        }
    }

    [UnityTest]
    public IEnumerator TheLevelSpritesAreLit() {
        RequireUrp();
        yield return null;
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            bool isMarker = renderer.sprite != null && renderer.sprite.name.StartsWith("icon");
            if (!isMarker && renderer.sprite != null) {
                Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Sprite-Lit"), renderer.name + " takes the lights");
            }
        }
    }
}
