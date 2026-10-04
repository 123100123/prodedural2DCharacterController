using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The procedural effects on the real character: stepping and landing raise dust, shots raise smoke, sparks and shell
// casings, bullets striking something raise sparks, casings settle on the ground and return to the pool, and a roll
// kicks dust where the hands touch down. Also writes a picture (Temp/LocomotionScenarios/vfx-*.png) to look at.
public class EffectsScenarioTests : ScenarioFixture {
    private const int FramesBetweenShots = 14;
    private const int FramesBeforeFirstClick = 30;
    private const float GroundTop = -5.21f;

    private Vector2 aimRequest;
    private Vector2 aimedPoint;

    private CharacterEffects Effects => Stage.Character.Effects;
    private EffectsSettings Settings => Stage.Character.Parts.Effects;
    private Vector2 Feet => new Vector2(Stage.Stride.transform.position.x, Stage.Stride.StanceHeight);

    private IEnumerator FindWhereTheMouseAims() {
        aimRequest = (Vector2)Stage.Stride.transform.position + new Vector2(3f, 1.5f);
        for (int frame = 0; frame < 8; frame++) {
            Stage.AimAndFire(aimRequest, false);
            yield return null;
        }
        Camera camera = Camera.main;
        Vector2 pointer = Stage.Character.LatestCommands.PointerScreenPosition;
        aimedPoint = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
    }

    // Tall walls on both sides of the character, so a shot meets one wherever the editor's remapped pointer ends up aiming.
    private void SpawnWallsAround() {
        foreach (float side in new[] { -1f, 1f }) {
            GameObject box = new GameObject("scenarioWall");
            box.transform.position = new Vector3(Stage.Stride.transform.position.x + side * 6f, 0f, 0f);
            box.AddComponent<BoxCollider2D>().size = new Vector2(1f, 100f);
        }
    }

    private IEnumerator AimAndClick(int frames) {
        for (int frame = 0; frame < frames; frame++) {
            bool isClicking = frame >= FramesBeforeFirstClick && (frame - FramesBeforeFirstClick) % FramesBetweenShots < 3;
            Stage.AimAndFire(aimRequest, isClicking);
            yield return null;
        }
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator RunningRaisesFootDust() {
        float strongestPlant = 0f;
        int plants = 0;
        Stage.Character.Events.FootPlanted += plant => {
            plants++;
            strongestPlant = Mathf.Max(strongestPlant, plant.impactSpeed);
        };
        yield return Hold(1f);
        Stage.Press(Key.D, Key.LeftShift);

        int mostDust = 0;
        for (int frame = 0; frame < 180; frame++) {
            yield return null;
            mostDust = Mathf.Max(mostDust, Effects.DustCount);
        }
        Report($"{plants} plants, strongest {strongestPlant:F2} u/s (dust from {Settings.footDustMinImpact}), most dust particles alive {mostDust}");
        Assert.That(plants, Is.GreaterThan(3), "the feet planted while running");
        Assert.That(mostDust, Is.GreaterThan(0), "and raised dust");
    }

    [UnityTest]
    public IEnumerator StandingStillRaisesNoDust() {
        yield return Hold(2f);
        Assert.That(Effects.DustCount, Is.Zero);
        Assert.That(Effects.SparkCount, Is.Zero);
        Assert.That(Effects.SmokeCount, Is.Zero);
        Assert.That(Effects.Casings.ActiveCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator AHardLandingSpraysMoreDustThanASoftOneAndAGentleOneNone() {
        yield return Hold(1f);
        Stage.Character.Events.RaiseLanded((Feet, Settings.landingDustMinImpact - 1f));
        Assert.That(Effects.DustCount, Is.Zero, "a gentle landing raises nothing");

        Stage.Character.Events.RaiseLanded((Feet, Settings.landingDustMinImpact + 2f));
        int soft = Effects.DustCount;
        Stage.Character.Events.RaiseLanded((Feet, Settings.landingDustFullImpact));
        int hard = Effects.DustCount - soft;
        Report($"soft landing {soft} particles, hard landing {hard}");
        Assert.That(soft, Is.GreaterThan(0));
        Assert.That(hard, Is.GreaterThan(soft));

        yield return Hold(Settings.dustLifetime * 1.3f + 0.2f);
        Assert.That(Effects.DustCount, Is.Zero, "the dust dies away");
    }

    [UnityTest]
    public IEnumerator JumpingKicksUpDustAtTheFeet() {
        yield return Hold(1f);
        Assert.That(Effects.DustCount, Is.Zero);
        Stage.Press(Key.Space);
        int mostDust = 0;
        for (int frame = 0; frame < 40; frame++) {
            yield return null;
            mostDust = Mathf.Max(mostDust, Effects.DustCount);
            if (frame == 2) {
                Stage.Release(Key.Space);
            }
        }
        Assert.That(mostDust, Is.GreaterThanOrEqualTo(EffectMath.ScaledCount(Settings.jumpDustCount, Settings.dustScale)), "the take-off puffs");
    }

    [UnityTest]
    public IEnumerator ARollRaisesDustWhereTheHandsTouchDown() {
        FieldInfo rollField = typeof(CharacterDriver).GetField("roll", BindingFlags.NonPublic | BindingFlags.Instance);
        ((RollSettings)rollField.GetValue(Stage.Character)).enabled = true;
        int rolls = 0;
        Stage.Character.Events.Rolled += _ => rolls++;
        yield return Hold(1f);
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(0.5f);
        Stage.Press(Key.D, Key.LeftShift, Key.C);
        yield return null;
        Stage.Press(Key.D, Key.LeftShift);
        int mostDust = 0;
        for (int frame = 0; frame < 120; frame++) {
            yield return null;
            mostDust = Mathf.Max(mostDust, Effects.DustCount);
        }
        Report($"{rolls} Rolled events, most dust alive {mostDust}");
        Assert.That(rolls, Is.EqualTo(1), "the roll's hands met the ground once");
        Assert.That(mostDust, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator AShotRaisesSmokeSparksAndACasingAndABulletStrikeRaisesSparksAndDust() {
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnWallsAround();
        Report($"aimed at {aimedPoint}, character at {Stage.Stride.transform.position}");
        int hits = 0;
        Stage.Character.Events.ShotHit += _ => hits++;
        Stage.Character.Events.Fired += shot => Report($"fired from {shot.muzzle} along {shot.direction}");

        int mostSmoke = 0;
        int mostSparks = 0;
        int mostCasings = 0;
        for (int frame = 0; frame < FramesBeforeFirstClick + 2 * FramesBetweenShots; frame++) {
            bool isClicking = frame >= FramesBeforeFirstClick && (frame - FramesBeforeFirstClick) % FramesBetweenShots < 3;
            Stage.AimAndFire(aimRequest, isClicking);
            yield return null;
            mostSmoke = Mathf.Max(mostSmoke, Effects.SmokeCount);
            mostSparks = Mathf.Max(mostSparks, Effects.SparkCount);
            mostCasings = Mathf.Max(mostCasings, Effects.Casings.ActiveCount);
        }
        Stage.StopAiming();
        Report($"hits {hits}, most smoke {mostSmoke}, sparks {mostSparks}, casings {mostCasings}");
        Assert.That(hits, Is.GreaterThan(0), "the bullets struck the wall");
        Assert.That(mostSmoke, Is.GreaterThan(0), "smoke curls from the muzzle");
        Assert.That(mostSparks, Is.GreaterThan(EffectMath.ScaledCount(3, Settings.sparkScale)), "the muzzle and the strike both throw sparks");
        Assert.That(mostCasings, Is.EqualTo(2), "one casing per shot");
    }

    [UnityTest]
    public IEnumerator CasingsLandOnTheGroundLieThereFadeAndReturnToThePool() {
        Settings.casingLifetime = 4f;
        Settings.casingFadeTime = 0.5f;
        yield return Hold(1f);
        Vector2 muzzle = (Vector2)Stage.Stride.transform.position + new Vector2(0.8f, 0.6f);
        for (int shot = 0; shot < 3; shot++) {
            Stage.Character.Events.RaiseFired((muzzle, Vector2.right));
        }
        CasingPool pool = Effects.Casings;
        Assert.That(pool.ActiveCount, Is.EqualTo(3));

        yield return Hold(2.2f);
        int resting = 0;
        for (int index = 0; index < CasingPool.Size; index++) {
            if (!pool.IsActive(index)) {
                continue;
            }
            Rigidbody2D body = pool.BodyOf(index);
            RaycastHit2D below = Physics2D.Raycast(body.position + Vector2.down * 0.05f, Vector2.down, 20f);
            Report($"casing {index} at {body.position}, velocity {body.linearVelocity}, simulated {body.simulated}, ground below: {(below.collider != null ? below.collider.name + " at " + below.point : "none")}, muzzle {muzzle}, feet {Feet}");
            Assert.That(body.gameObject.layer, Is.EqualTo(2), "casings are on the Ignore Raycast layer");
            Assert.That(body.position.y, Is.GreaterThan(GroundTop - 0.05f).And.LessThan(GroundTop + 0.4f), "it landed on the ground and did not fall through");
            resting += body.linearVelocity.magnitude < 1f ? 1 : 0;
        }
        Assert.That(resting, Is.EqualTo(3), "they settled");

        yield return Hold(2.2f);
        Assert.That(pool.ActiveCount, Is.Zero, "after their lifetime they are back in the pool");
    }

    [UnityTest]
    public IEnumerator ACasingNeitherStopsABulletNorTheCharacterNorCountsAsGround() {
        yield return Hold(1f);
        Vector2 feet = Feet;
        Vector2 muzzle = new Vector2(feet.x + 1f, GroundTop + 3f);
        Stage.Character.Events.RaiseFired((muzzle, Vector2.right));
        yield return Hold(0.2f);

        RaycastHit2D[] hits = new RaycastHit2D[8];
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers, useTriggers = false };
        Vector2 casing = default;
        for (int index = 0; index < CasingPool.Size; index++) {
            if (Effects.Casings.IsActive(index)) {
                casing = Effects.Casings.BodyOf(index).position;
            }
        }
        int count = Physics2D.Raycast(casing + Vector2.left * 2f, Vector2.right, filter, hits, 4f);
        for (int hit = 0; hit < count; hit++) {
            Assert.That(hits[hit].collider.gameObject.layer, Is.Not.EqualTo(2), "the default-mask ray passes through the casing");
        }
        Assert.That(Stage.Stride.HasLostGround, Is.False, "the feet still have ground");
    }

    [UnityTest]
    public IEnumerator SwitchingTheEffectsOffEmitsNothing() {
        Settings.enabled = false;
        yield return Hold(1f);
        Stage.Character.Events.RaiseLanded((Feet, 25f));
        Stage.Character.Events.RaiseFired((Feet + Vector2.up, Vector2.right));
        Stage.Character.Events.RaiseRolled((Feet, 1));
        Assert.That(Effects.DustCount, Is.Zero);
        Assert.That(Effects.SmokeCount, Is.Zero);
        Assert.That(Effects.SparkCount, Is.Zero);
        Assert.That(Effects.Casings.ActiveCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ADustScaleOfZeroRaisesNoDustButTheSparksStay() {
        Settings.dustScale = 0f;
        yield return Hold(1f);
        Stage.Character.Events.RaiseLanded((Feet, 25f));
        Stage.Character.Events.RaiseFired((Feet + Vector2.up, Vector2.right));
        Assert.That(Effects.DustCount, Is.Zero);
        Assert.That(Effects.SparkCount, Is.GreaterThan(0));
    }

    // A picture of every effect at once for a person to look at.
    [UnityTest]
    public IEnumerator SavesAPictureOfTheEffects() {
        yield return Hold(1f);
        Vector2 feet = Feet;
        Stage.Character.Events.RaiseLanded((feet + Vector2.left * 3f, 18f));
        Stage.Character.Events.RaiseFootPlanted((feet, 7f));
        Stage.Character.Events.RaiseRolled((feet + Vector2.right * 3f, 1));
        Vector2 muzzle = feet + new Vector2(1.5f, 2f);
        for (int shot = 0; shot < 4; shot++) {
            Stage.Character.Events.RaiseFired((muzzle, Vector2.right));
        }
        Vector2 wall = feet + new Vector2(7f, 2f);
        for (int hit = 0; hit < 3; hit++) {
            Stage.Character.Events.RaiseShotHit(new ShotHit(null, (wall + Vector2.up * hit * 0.4f, Vector2.left, Vector2.right), (1f, 1f)));
        }
        yield return Hold(0.18f);

        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "vfx-burst.png"), Capture(feet + new Vector2(2f, 1.2f), 4.5f).EncodeToPNG());
        Assert.That(Effects.DustCount + Effects.SparkCount + Effects.SmokeCount, Is.GreaterThan(10));
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
}
