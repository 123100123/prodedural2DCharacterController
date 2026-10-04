using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The parallax background in the real scene: three layers behind the level, each on its sorting layer, moving by its
// own factor as the camera follows the character.
public class BackgroundScenarioTests : ScenarioFixture {
    private static readonly string[] LayerNames = { "BackgroundFar", "BackgroundMid", "BackgroundWall" };

    private static SpriteRenderer Layer(string name) {
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) {
            if (renderer.name == name) {
                return renderer;
            }
        }
        return null;
    }

    [UnityTest]
    public IEnumerator TheThreeLayersSitBehindTheLevelInOrder() {
        yield return null;
        int previous = int.MinValue;
        foreach (string name in LayerNames) {
            SpriteRenderer layer = Layer(name);
            Assert.That(layer, Is.Not.Null, name);
            Assert.That(layer.sprite, Is.Not.Null, name + " has its art");
            int value = SortingLayer.GetLayerValueFromName(layer.sortingLayerName);
            Assert.That(value, Is.GreaterThan(previous), $"{name} is in front of the layer before it");
            previous = value;
        }
        Assert.That(previous, Is.LessThan(SortingLayer.GetLayerValueFromName("Default")), "all behind the level");
    }

    [UnityTest]
    public IEnumerator EachLayerFollowsTheCameraByItsFactor() {
        Camera camera = Camera.main;
        yield return Hold(1f);
        (string name, Vector2 position)[] before = new (string, Vector2)[LayerNames.Length];
        for (int index = 0; index < LayerNames.Length; index++) {
            before[index] = (LayerNames[index], Layer(LayerNames[index]).transform.position);
        }
        Vector2 cameraBefore = camera.transform.position;

        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(3f);
        Vector2 cameraDelta = (Vector2)camera.transform.position - cameraBefore;
        Assert.That(cameraDelta.x, Is.GreaterThan(10f), "the camera followed the run");

        foreach ((string name, Vector2 position) start in before) {
            Vector2 factor = Layer(start.name).GetComponent<ParallaxLayer>().Factor;
            Vector2 expected = ParallaxMath.Offset(cameraDelta, factor);
            Vector2 moved = (Vector2)Layer(start.name).transform.position - start.position;
            Assert.That(moved.x, Is.EqualTo(expected.x).Within(0.05f), start.name + " x");
            Assert.That(moved.y, Is.EqualTo(expected.y).Within(0.05f), start.name + " y");
        }
    }

    [UnityTest]
    public IEnumerator TheLayersCoverTheWholeViewAtTheStart() {
        yield return Hold(0.5f);
        Camera camera = Camera.main;
        float halfWidth = camera.orthographicSize * camera.aspect;
        foreach (string name in LayerNames) {
            Bounds bounds = Layer(name).bounds;
            Assert.That(bounds.min.x, Is.LessThan(camera.transform.position.x - halfWidth), name + " reaches past the left edge");
            Assert.That(bounds.max.x, Is.GreaterThan(camera.transform.position.x + halfWidth), name + " reaches past the right edge");
        }
    }
}
