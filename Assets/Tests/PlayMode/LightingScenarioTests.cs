using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// The lighting rig on the real character: the muzzle flash lights on every shot, at the muzzle, and dies away; rapid fire
// stays well behaved; the light works from any pose. They need the Universal Render Pipeline (Tools > Procedural
// Character > Set Up URP 2D Lighting) and are skipped without it.
public class LightingScenarioTests : ScenarioFixture {
    private ILightingTarget lighting;

    private IEnumerator RequireLighting() {
        if (GraphicsSettings.currentRenderPipeline == null) {
            Assert.Ignore("The Universal Render Pipeline is not set up.");
        }
        yield return null;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) {
            if (behaviour is ILightingTarget found) {
                lighting = found;
            }
        }
        Assert.That(lighting, Is.Not.Null, "the scene has a lighting rig (the bootstrap adds one)");
    }

    // Aims up and to the right, firing on alternate frames for the given number of frames.
    private IEnumerator Shoot(int frames) {
        for (int frame = 0; frame < frames; frame++) {
            Vector2 body = Stage.Stride.transform.position;
            Stage.AimAndFire(body + new Vector2(6f, 3f), frame % 2 == 1 && frame > 3);
            yield return null;
        }
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator EveryShotFlashesTheMuzzleAtTheMuzzle() {
        yield return RequireLighting();
        int shots = 0;
        Vector2 firedAt = Vector2.zero;
        Stage.Character.Events.Fired += shot => {
            shots++;
            firedAt = shot.muzzle;
        };
        float brightest = 0f;
        for (int frame = 0; frame < 60; frame++) {
            Stage.AimAndFire((Vector2)Stage.Stride.transform.position + new Vector2(6f, 3f), frame == 20);
            yield return null;
            brightest = Mathf.Max(brightest, lighting.MuzzleIntensity);
        }
        Stage.StopAiming();

        Assert.That(shots, Is.EqualTo(1));
        Assert.That(lighting.MuzzleShots, Is.EqualTo(shots), "one flash per shot");
        Assert.That(brightest, Is.EqualTo(lighting.Settings.muzzlePeakIntensity).Within(0.01f), "full brightness on the shot");
        Assert.That(Vector2.Distance(lighting.MuzzlePosition, firedAt), Is.LessThan(0.01f), "at the muzzle");
        Assert.That(lighting.MuzzleIntensity, Is.EqualTo(0f), "gone again");
    }

    [UnityTest]
    public IEnumerator TheFlashDiesAwaySmoothly() {
        yield return RequireLighting();
        yield return Stage.DrawAt(() => (Vector2)Stage.Stride.transform.position + new Vector2(6f, 3f));
        Stage.AimAndFire((Vector2)Stage.Stride.transform.position + new Vector2(6f, 3f), true);
        yield return null;
        Stage.StopAiming();

        float previous = lighting.MuzzleIntensity;
        Assert.That(previous, Is.GreaterThan(0f));
        for (int frame = 0; frame < 20; frame++) {
            yield return null;
            Assert.That(lighting.MuzzleIntensity, Is.LessThanOrEqualTo(previous + 0.0001f), "never brightens again");
            previous = lighting.MuzzleIntensity;
        }
        Assert.That(previous, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator RapidFireFlashesEveryShotWithoutBuildingUp() {
        yield return RequireLighting();
        int shots = 0;
        Stage.Character.Events.Fired += _ => shots++;
        float brightest = 0f;
        for (int frame = 0; frame < 150; frame++) {
            Stage.AimAndFire((Vector2)Stage.Stride.transform.position + new Vector2(6f, 3f), frame % 2 == 1 && frame > 3);
            yield return null;
            brightest = Mathf.Max(brightest, lighting.MuzzleIntensity);
        }
        Stage.StopAiming();
        yield return Hold(0.3f);

        Assert.That(shots, Is.GreaterThan(5));
        Assert.That(lighting.MuzzleShots, Is.EqualTo(shots));
        Assert.That(brightest, Is.LessThanOrEqualTo(lighting.Settings.muzzlePeakIntensity + 0.01f), "no glare");
        Assert.That(lighting.MuzzleIntensity, Is.EqualTo(0f), "and dark when the shooting stops");
    }

    [UnityTest]
    public IEnumerator TheFlashWorksWhileCrouchedAndInTheAir() {
        yield return RequireLighting();
        Stage.Press(Key.LeftCtrl);
        yield return Hold(1f);
        yield return Shoot(40);
        int crouched = lighting.MuzzleShots;
        Stage.Release(Key.LeftCtrl);
        yield return Hold(1f);

        Stage.Press(Key.Space);
        yield return Hold(0.3f);
        yield return Shoot(30);
        Assert.That(crouched, Is.GreaterThan(0), "while crouched");
        Assert.That(lighting.MuzzleShots, Is.GreaterThan(crouched), "and in the air");
    }

    [UnityTest]
    public IEnumerator ThePlayerLightFollowsAndTheSceneStaysLit() {
        yield return RequireLighting();
        Assert.That(lighting.PlayerIntensity, Is.EqualTo(lighting.Settings.playerIntensity).Within(0.001f));
        Assert.That(lighting.Settings.ambientIntensity, Is.GreaterThanOrEqualTo(0.4f), "the ambient light keeps the scene readable");
    }
}
