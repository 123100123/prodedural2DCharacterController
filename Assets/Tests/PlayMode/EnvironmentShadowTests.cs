using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// The sun's shadows on the real scene, measured on screen: a camera renders the view and the brightness of the background
// inside the shadow of the raised block is compared with the background beside it. They need the Universal Render
// Pipeline and are skipped without it.
public class EnvironmentShadowTests {
    private const string BlockName = "ground (8)";
    private const int PatchHalf = 4;

    private ScenarioStage stage;
    private ILightingTarget lighting;
    private Rect block;

    [UnitySetUp]
    public IEnumerator LoadTheRealLevel() {
        if (GraphicsSettings.currentRenderPipeline == null) {
            Assert.Ignore("The Universal Render Pipeline is not set up.");
        }
        stage = new ScenarioStage();
        yield return stage.Load(true);
        yield return null;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) {
            if (behaviour is ILightingTarget found) {
                lighting = found;
            }
        }
        Assert.That(lighting, Is.Not.Null, "the scene has a lighting rig");
        Bounds bounds = GameObject.Find(BlockName).GetComponent<SpriteRenderer>().bounds;
        block = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        // The probes compare the raised block's shadow with the lit background beside it; the playground built against the
        // block's right edge casts shadows of its own there, so it is switched off for those (only its pictures keep it).
        GameObject playground = GameObject.Find("Playground");
        if (playground != null && !TestContext.CurrentContext.Test.Name.Contains("Playground")) {
            playground.SetActive(false);
        }
        yield return Frames(5);
    }

    [TearDown]
    public void Unload() => stage?.Unload();

    [UnityTest]
    public IEnumerator TheSunShadowsTheBackgroundBehindTheRaisedBlock() {
        (Vector2 shadowed, Vector2 lit) points = PointsAroundTheBlockCorner();
        Assert.That(IsInShadow(points.shadowed), Is.True, "the probe point is in the block's shadow, by geometry");
        Assert.That(IsInShadow(points.lit), Is.False, "and the comparison point is not");

        float inShadow = BackgroundBrightnessAt(points.shadowed);
        float beside = BackgroundBrightnessAt(points.lit);
        Debug.Log($"background brightness: in the shadow {inShadow:F3}, beside it {beside:F3}");
        Assert.That(inShadow, Is.LessThan(beside * 0.85f), "at least 15 % darker inside the shadow");
        yield return null;
    }

    [UnityTest]
    public IEnumerator WithTheSunOffTheSamePlacesAreEquallyBright() {
        lighting.Settings.sunEnabled = false;
        lighting.Apply();
        yield return Frames(3);
        (Vector2 shadowed, Vector2 lit) points = PointsAroundTheBlockCorner();
        float first = BackgroundBrightnessAt(points.shadowed);
        float second = BackgroundBrightnessAt(points.lit);
        Assert.That(first, Is.EqualTo(second).Within(second * 0.05f), "the darkening was the sun's shadow");
    }

    [UnityTest]
    public IEnumerator TheShadowTurnsWithTheSunAngle() {
        (Vector2 shadowed, Vector2 lit) points = PointsAroundTheBlockCorner();
        float before = BackgroundBrightnessAt(points.shadowed);

        lighting.Settings.sunAngle = 60f;
        lighting.Apply();
        yield return Frames(3);
        Assert.That(IsInShadow(points.shadowed), Is.False, "from the other side that spot is no longer in shadow, by geometry");
        float after = BackgroundBrightnessAt(points.shadowed);
        Assert.That(after, Is.GreaterThan(before * 1.1f), "and it is lit again");
    }

    [UnityTest]
    public IEnumerator TheSunLightsTheBackgroundAndALampLightsItToo() {
        yield return Frames(2);
        Assert.That(lighting.SunIntensity, Is.GreaterThan(0f));
        Assert.That(lighting.SunCastsShadows, Is.True);
        Assert.That(lighting.LampCount, Is.GreaterThanOrEqualTo(2), "the placed lamps are part of the rig");
        Assert.That(lighting.ShadowCasterCount, Is.GreaterThanOrEqualTo(9), "the level's blocks cast");
    }

    // Pictures of the lit scene for a person to look at (Temp/LocomotionScenarios/*.png): the start, the raised block with
    // the shadow it throws, and a lamp.
    [UnityTest]
    public IEnumerator SavesPicturesOfTheLitScene() {
        string folder = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        System.IO.Directory.CreateDirectory(folder);
        (string name, Vector2 center, float size)[] views = {
            ("lit-start", new Vector2(-8.5f, -3.5f), 13.1f),
            ("lit-raised-block", new Vector2(14f, -3f), 11f),
            ("lit-lamp", new Vector2(-9f, -2f), 7f),
        };
        foreach ((string name, Vector2 center, float size) view in views) {
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, view.name + ".png"), Capture(view.center, view.size).EncodeToPNG());
        }
        yield return null;
    }

    // The playground area (crates and targets, then the slide gap, ledge and platforms, then the wall-jump pit), when the
    // scene has one: Temp/LocomotionScenarios/playground*.png.
    [UnityTest]
    public IEnumerator SavesPicturesOfThePlayground() {
        GameObject floor = GameObject.Find("Playground")?.transform.Find("Floor")?.gameObject;
        if (floor == null) {
            Assert.Ignore("The scene has no playground.");
        }
        string folder = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        System.IO.Directory.CreateDirectory(folder);
        Bounds bounds = floor.GetComponent<BoxCollider2D>().bounds;
        (string name, float along, float size)[] views = { ("playground", 24f, 13.1f), ("playground-platforms", 66f, 13.1f), ("playground-pit", 104f, 13.1f), ("playground-hill", 107.5f, 6f) };
        foreach ((string name, float along, float size) view in views) {
            Vector2 center = new Vector2(bounds.min.x + view.along, bounds.max.y + 3f);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, view.name + ".png"), Capture(center, view.size).EncodeToPNG());
        }
        yield return null;
    }

    private static Texture2D Capture(Vector2 center, float orthographicSize) {
        GameObject host = new GameObject("pictureCamera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = new Vector3(center.x, center.y, -10f);
        camera.orthographicSize = orthographicSize;
        camera.aspect = 16f / 9f;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(host);
        return picture;
    }

    // A spot in the raised block's shadow, below and beyond its bottom right corner as seen from the sun, and a spot
    // the same distance along the shadow but to the side of it, in the light.
    private (Vector2 shadowed, Vector2 lit) PointsAroundTheBlockCorner() {
        Vector2 corner = new Vector2(block.xMax, block.yMin);
        Vector2 away = (corner - lighting.SunPivot).normalized;
        Vector2 sideways = new Vector2(-away.y, away.x);
        if (sideways.y < 0f) {
            sideways = -sideways;
        }
        Vector2 along = corner + away * 3f;
        return (Snap(along - sideways * 1.2f), Snap(along + sideways * 3f));
    }

    // Away from the grid's dots: the middle of a cell.
    private static Vector2 Snap(Vector2 point) => new Vector2(Mathf.Floor(point.x) + 0.5f, Mathf.Floor(point.y) + 0.5f);

    // Whether the line from the point to the sun passes through the block.
    private bool IsInShadow(Vector2 point) {
        Vector2 to = lighting.SunPivot;
        for (int step = 0; step <= 400; step++) {
            Vector2 along = Vector2.Lerp(point, to, step / 400f * 0.2f);
            if (block.Contains(along)) {
                return true;
            }
        }
        return false;
    }

    private float BackgroundBrightnessAt(Vector2 world) {
        GameObject host = new GameObject("shadowProbeCamera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = new Vector3(world.x, world.y, -10f);
        camera.orthographicSize = 4f;
        camera.aspect = 16f / 9f;
        RenderTexture target = new RenderTexture(640, 360, 24);
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D readback = new Texture2D(640, 360, TextureFormat.RGB24, false);
        readback.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
        RenderTexture.active = previous;

        Color[] patch = readback.GetPixels(320 - PatchHalf, 180 - PatchHalf, PatchHalf * 2 + 1, PatchHalf * 2 + 1);
        float sum = 0f;
        foreach (Color pixel in patch) {
            sum += 0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b;
        }

        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(readback);
        Object.Destroy(host);
        return sum / patch.Length;
    }

    private static IEnumerator Frames(int count) {
        for (int frame = 0; frame < count; frame++) {
            yield return null;
        }
    }
}
