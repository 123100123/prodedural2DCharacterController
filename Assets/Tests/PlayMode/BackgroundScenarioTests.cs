using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The plain dot-grid background in the real scene: one sprite behind the level that always covers the view and moves
// only in whole pattern periods, so the dots look fixed in the world.
public class BackgroundScenarioTests : ScenarioFixture {
    private const float Period = 5f;

    private static SpriteRenderer Grid() {
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) {
            if (renderer.name == "BackgroundGrid") {
                return renderer;
            }
        }
        return null;
    }

    [UnityTest]
    public IEnumerator TheBackgroundIsOneLitSpriteBehindTheLevel() {
        yield return null;
        SpriteRenderer grid = Grid();
        Assert.That(grid, Is.Not.Null);
        Assert.That(grid.sprite, Is.Not.Null, "it has its art");
        Assert.That(SortingLayer.GetLayerValueFromName(grid.sortingLayerName), Is.LessThan(SortingLayer.GetLayerValueFromName("Default")));
        Assert.That(grid.sharedMaterial.shader.name, Does.Contain("Sprite-Lit"), "it takes the lights and the shadows");
    }

    [UnityTest]
    public IEnumerator ItCoversTheViewWhereverTheCameraGoes() {
        Camera camera = Camera.main;
        Stage.Press(Key.D, Key.LeftShift);
        for (int second = 0; second < 5; second++) {
            yield return Hold(1f);
            Bounds bounds = Grid().bounds;
            float halfWidth = camera.orthographicSize * camera.aspect;
            Vector2 center = camera.transform.position;
            Assert.That(bounds.min.x, Is.LessThan(center.x - halfWidth), $"left edge covered after {second + 1} s");
            Assert.That(bounds.max.x, Is.GreaterThan(center.x + halfWidth), $"right edge covered after {second + 1} s");
            Assert.That(bounds.min.y, Is.LessThan(center.y - camera.orthographicSize));
            Assert.That(bounds.max.y, Is.GreaterThan(center.y + camera.orthographicSize));
        }
    }

    [UnityTest]
    public IEnumerator ItMovesOnlyInWholePeriods() {
        Stage.Press(Key.D, Key.LeftShift);
        for (int frame = 0; frame < 240; frame++) {
            yield return null;
            Vector3 position = Grid().transform.position;
            Assert.That(Mathf.Abs(position.x / Period - Mathf.Round(position.x / Period)), Is.LessThan(0.001f), $"x {position.x} at frame {frame}");
            Assert.That(Mathf.Abs(position.y / Period - Mathf.Round(position.y / Period)), Is.LessThan(0.001f), $"y {position.y} at frame {frame}");
        }
    }
}
