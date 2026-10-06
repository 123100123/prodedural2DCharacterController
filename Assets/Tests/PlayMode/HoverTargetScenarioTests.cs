using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The flying wooden targets on the real character: a target flies its route, a shot scores ten and knocks (not the
// generic thud) with a popup that rises and fades back into its pool, the third hit scores fifty and breaks it (the rotor
// runs down, the board drops, splinters fly, a crack sounds) and it rises back after the delay, while the plain
// shooting target is unchanged. Also writes a picture (Temp/LocomotionScenarios/hover-targets.png) to look at.
public class HoverTargetScenarioTests : ScenarioFixture {
    private const int FramesBetweenShots = 14;
    private const int FramesBeforeFirstClick = 30;
    private const float BigBoard = 6f;

    private Vector2 aimRequest;
    private Vector2 aimedPoint;

    private CharacterEffects Effects => Stage.Character.Effects;
    private CharacterAudio Audio => Stage.Character.Audio;

    private IEnumerator AimAheadOfTheCharacter() {
        aimRequest = (Vector2)Stage.Stride.transform.position + new Vector2(3f, 1.5f);
        for (int frame = 0; frame < 8; frame++) {
            Stage.AimAndFire(aimRequest, false);
            yield return null;
        }
        Camera camera = Camera.main;
        Vector2 pointer = Stage.Character.LatestCommands.PointerScreenPosition;
        aimedPoint = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
        Assert.That(Vector2.Distance(aimedPoint, aimRequest), Is.LessThan(0.01f), "the game reads the pointer where the stage put it");
    }

    // A target built with its settings in place before it wakes (the component builds its picture on waking): a big board
    // standing still on the aimed point, so a bullet flying roughly at it hits.
    private HoverTarget SpawnStill(int hitsToBreak = 3, float respawnDelay = 2.5f) {
        return Spawn(aimedPoint, new HoverTargetSettings {
            boardSize = BigBoard, extent = Vector2.zero, bobHeight = 0f, path = HoverPathKind.Loop, hitsToBreak = hitsToBreak, respawnDelay = respawnDelay,
        });
    }

    private static HoverTarget Spawn(Vector2 at, HoverTargetSettings settings) {
        GameObject host = new GameObject("scenarioHover");
        host.SetActive(false);
        host.transform.position = at;
        HoverTarget target = host.AddComponent<HoverTarget>();
        target.Configure(settings);
        host.SetActive(true);
        return target;
    }

    private IEnumerator AimAndClick(int frames) {
        for (int frame = 0; frame < frames; frame++) {
            bool isClicking = frame >= FramesBeforeFirstClick && (frame - FramesBeforeFirstClick) % FramesBetweenShots < 3;
            Stage.AimAndFire(aimRequest, isClicking);
            yield return null;
        }
        Stage.StopAiming();
    }

    private int ActivePopup() {
        for (int index = 0; index < PointsPopups.Size; index++) {
            if (Effects.Popups.IsActive(index)) {
                return index;
            }
        }
        return -1;
    }

    [UnityTest]
    public IEnumerator ATargetFliesItsRouteBobbingAndLeaningIntoItsMotion() {
        yield return Hold(1f);
        Vector2 anchor = (Vector2)Stage.Stride.transform.position + new Vector2(0f, 9f);
        HoverTarget target = Spawn(anchor, new HoverTargetSettings { path = HoverPathKind.Patrol, extent = new Vector2(3f, 0f), period = 4f, bobHeight = 0.2f, bobPeriod = 1.3f });
        Transform visual = target.transform.Find("Hover Visual");
        Assert.That(visual, Is.Not.Null, "the picture is built");
        Assert.That(visual.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(5), "board, mast, hub and two blades");

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float previousX = target.Position.x;
        bool leansIntoMotion = false;
        bool leansAgainst = false;
        for (float elapsed = 0f; elapsed < 4.2f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            Vector2 at = target.Position;
            minX = Mathf.Min(minX, at.x);
            maxX = Mathf.Max(maxX, at.x);
            minY = Mathf.Min(minY, at.y);
            maxY = Mathf.Max(maxY, at.y);
            float lean = Mathf.DeltaAngle(0f, visual.localEulerAngles.z);
            float heading = at.x - previousX;
            if (Mathf.Abs(heading) > 0.02f && Mathf.Abs(lean) > 1f) {
                if (lean * heading < 0f) {
                    leansIntoMotion = true;
                }
                else {
                    leansAgainst = true;
                }
            }
            previousX = at.x;
        }
        Report($"x {minX:F2} to {maxX:F2} (anchor {anchor.x:F2}), y {minY:F2} to {maxY:F2}, leans into motion {leansIntoMotion}");
        Assert.That(minX, Is.EqualTo(anchor.x - 3f).Within(0.25f), "reaches its left end");
        Assert.That(maxX, Is.EqualTo(anchor.x + 3f).Within(0.25f), "and its right end");
        Assert.That(maxY - minY, Is.GreaterThan(0.25f), "bobs");
        Assert.That(leansIntoMotion, Is.True, "tips its leading side down");
        Assert.That(leansAgainst, Is.False, "never the other way");
        Assert.That(target.transform.position.x, Is.EqualTo(target.Position.x).Within(0.5f), "the body follows");
        Assert.That(target.Spin, Is.EqualTo(1f), "the rotor spins");
    }

    [UnityTest]
    public IEnumerator TheRotorBladesSwingInLengthWhileItSpins() {
        yield return Hold(0.5f);
        HoverTarget target = Spawn((Vector2)Stage.Stride.transform.position + new Vector2(0f, 9f), new HoverTargetSettings { extent = Vector2.zero, bobHeight = 0f });
        Transform blade = target.transform.Find("Hover Visual/Blade 0");
        Transform other = target.transform.Find("Hover Visual/Blade 1");
        float shortest = float.PositiveInfinity;
        float longest = float.NegativeInfinity;
        bool opposite = true;
        for (int frame = 0; frame < 90; frame++) {
            yield return null;
            shortest = Mathf.Min(shortest, blade.localScale.x);
            longest = Mathf.Max(longest, blade.localScale.x);
            opposite &= Mathf.Abs(blade.localScale.x + other.localScale.x) < 1e-3f;
        }
        Assert.That(longest, Is.GreaterThan(0.8f * target.Settings.boardSize * 0.78f), "swings out");
        Assert.That(shortest, Is.LessThan(-0.8f * target.Settings.boardSize * 0.78f), "and round to the other side");
        Assert.That(opposite, Is.True, "the second blade is half a turn behind");
    }

    [UnityTest]
    public IEnumerator AShotScoresTenAndKnocksWithAPopupThatRisesFadesAndReturnsToThePool() {
        List<(int points, Vector2 position, bool broke)> scores = new List<(int, Vector2, bool)>();
        Stage.Character.Events.TargetScored += scores.Add;
        yield return Hold(1f);
        yield return AimAheadOfTheCharacter();
        HoverTarget target = SpawnStill();
        yield return Hold(0.2f);

        yield return AimAndClick(FramesBeforeFirstClick + 6);

        Assert.That(scores.Count, Is.EqualTo(1), "one shot, one score");
        Assert.That(scores[0].points, Is.EqualTo(10));
        Assert.That(scores[0].broke, Is.False);
        Assert.That(scores[0].position, Is.EqualTo(target.Position).Using(new UnityEngine.TestTools.Utils.Vector2EqualityComparer(0.5f)), "at the target");
        Assert.That(target.HitCount, Is.EqualTo(1));
        Assert.That(Audio.PlaysOf(SoundId.WoodKnock), Is.EqualTo(1), "the wood knock");
        Assert.That(Audio.PlaysOf(SoundId.TargetHit), Is.Zero, "and not the generic thud on top of it");
        Assert.That(Audio.PlaysOf(SoundId.Ricochet), Is.Zero);

        int popup = ActivePopup();
        Assert.That(popup, Is.GreaterThanOrEqualTo(0), "a popup is showing");
        Assert.That(Effects.Popups.TextOf(popup), Is.EqualTo("+10"));
        Assert.That(Effects.Popups.ActiveCount, Is.EqualTo(1));

        EffectsSettings settings = Stage.Character.Parts.Effects;
        float startY = Effects.Popups.PositionOf(popup).y;
        float startAlpha = Effects.Popups.AlphaOf(popup);
        float previousY = startY;
        float previousAlpha = startAlpha;
        bool risesAndFades = true;
        for (float elapsed = 0f; elapsed < settings.targets.popupLifetime - 0.1f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            if (!Effects.Popups.IsActive(popup)) {
                break;
            }
            float y = Effects.Popups.PositionOf(popup).y;
            float alpha = Effects.Popups.AlphaOf(popup);
            risesAndFades &= y >= previousY - 1e-4f && alpha <= previousAlpha + 1e-4f;
            previousY = y;
            previousAlpha = alpha;
        }
        Report($"popup rose {previousY - startY:F2} of {settings.targets.popupRise:F2}, alpha {startAlpha:F2} to {previousAlpha:F2}");
        Assert.That(risesAndFades, Is.True, "never sinks and never gets more opaque");
        Assert.That(previousY - startY, Is.GreaterThan(settings.targets.popupRise * 0.7f), "rose most of the way");
        Assert.That(previousAlpha, Is.LessThan(startAlpha * 0.4f), "faded");

        yield return Hold(0.4f);
        Assert.That(Effects.Popups.ActiveCount, Is.Zero, "back in the pool");
        Assert.That(Effects.Popups.IsActive(popup), Is.False);
    }

    [UnityTest]
    public IEnumerator TheThirdHitScoresFiftyAndBreaksTheBoardWhichDropsSplintersAndComesBack() {
        List<(int points, Vector2 position, bool broke)> scores = new List<(int, Vector2, bool)>();
        Stage.Character.Events.TargetScored += scores.Add;
        yield return Hold(1f);
        yield return AimAheadOfTheCharacter();
        HoverTarget target = SpawnStill();
        Collider2D collider = target.GetComponent<Collider2D>();
        Transform visual = target.transform.Find("Hover Visual");
        yield return Hold(0.2f);

        yield return AimAndClick(FramesBeforeFirstClick + 3 * FramesBetweenShots + 5);

        Assert.That(scores.ConvertAll(score => score.points), Is.EqualTo(new List<int> { 10, 10, 50 }), "ten a hit, fifty for the one that breaks it");
        Assert.That(scores[2].broke, Is.True);
        Assert.That(target.IsBroken, Is.True);
        Assert.That(target.BreakCount, Is.EqualTo(1));
        Assert.That(collider.enabled, Is.False, "it cannot be hit while it falls");
        Assert.That(Audio.PlaysOf(SoundId.WoodKnock), Is.EqualTo(2));
        Assert.That(Audio.PlaysOf(SoundId.WoodBreak), Is.EqualTo(1), "the crack and clatter");
        Assert.That(Audio.PlaysOf(SoundId.TargetHit), Is.Zero);
        Assert.That(Effects.SplinterCount, Is.GreaterThan(4), "splinters fly");
        Assert.That(target.Spin, Is.LessThan(1f), "the rotor runs down");

        bool sawBig = false;
        for (int index = 0; index < PointsPopups.Size; index++) {
            if (Effects.Popups.IsActive(index) && Effects.Popups.TextOf(index) == "+50") {
                sawBig = true;
                Assert.That(Effects.Popups.SizeOf(index), Is.GreaterThan(Stage.Character.Parts.Effects.targets.hitTextSize), "bigger");
                Assert.That(Effects.Popups.ColorOf(index), Is.Not.EqualTo(Stage.Character.Parts.Effects.targets.hitTextColor), "and another colour");
            }
        }
        Assert.That(sawBig, Is.True, "a +50 popup is showing");

        // It drops: lower than where it was hit, until gone.
        float hitY = aimedPoint.y;
        float lowest = target.Position.y;
        for (int frame = 0; frame < 120 && target.Phase != HoverPhase.Gone; frame++) {
            yield return null;
            lowest = Mathf.Min(lowest, target.Position.y);
        }
        Report($"hit at y {hitY:F2}, fell to {lowest:F2}");
        Assert.That(lowest, Is.LessThan(hitY - 0.5f), "fell");
        Assert.That(target.Phase, Is.EqualTo(HoverPhase.Gone));
        Assert.That(visual.gameObject.activeSelf, Is.False, "out of sight");

        bool rose = false;
        float risenAt = 0f;
        for (float elapsed = 0f; elapsed < 6f && target.Phase != HoverPhase.Hovering; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            if (target.Phase == HoverPhase.Rising) {
                rose = true;
                risenAt = Mathf.Max(risenAt, target.Spin);
                Assert.That(collider.enabled, Is.False, "not hittable while it rises");
            }
        }
        Assert.That(rose, Is.True, "it rose back in");
        Assert.That(target.Phase, Is.EqualTo(HoverPhase.Hovering), "and hovers again");
        Assert.That(risenAt, Is.GreaterThan(0f), "with the rotor spinning up");
        Assert.That(collider.enabled, Is.True);
        Assert.That(visual.gameObject.activeSelf, Is.True);
        Assert.That(target.HitsTaken, Is.Zero, "at full health");
        Assert.That(Vector2.Distance(target.Position, aimedPoint), Is.LessThan(0.2f), "back on its route");
    }

    [UnityTest]
    public IEnumerator APlainShootingTargetStillMakesTheGenericThudAndScoresNothing() {
        int scored = 0;
        Stage.Character.Events.TargetScored += _ => scored++;
        yield return Hold(1f);
        yield return AimAheadOfTheCharacter();
        GameObject box = new GameObject("scenarioTarget");
        box.transform.position = new Vector3(aimedPoint.x, aimedPoint.y, 0f);
        box.AddComponent<BoxCollider2D>().size = new Vector2(1f, 6f);
        box.AddComponent<SpriteRenderer>();
        ShootingTarget target = box.AddComponent<ShootingTarget>();
        yield return Hold(0.2f);

        yield return AimAndClick(FramesBeforeFirstClick + 6);

        Assert.That(target.HitCount, Is.EqualTo(1));
        Assert.That(scored, Is.Zero, "a plain target pays nothing");
        Assert.That(Audio.PlaysOf(SoundId.TargetHit), Is.EqualTo(1), "the generic thud");
        Assert.That(Audio.PlaysOf(SoundId.WoodKnock), Is.Zero);
        Assert.That(Effects.Popups.ActiveCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ManyScoresAtOnceReuseThePoolWithoutGrowingIt() {
        yield return Hold(0.5f);
        for (int score = 0; score < PointsPopups.Size * 2; score++) {
            Stage.Character.Events.RaiseTargetScored((10 + score, new Vector2(score, 5f), false));
        }
        Assert.That(Effects.Popups.ActiveCount, Is.EqualTo(PointsPopups.Size));
        yield return Hold(Stage.Character.Parts.Effects.targets.popupLifetime + 0.3f);
        Assert.That(Effects.Popups.ActiveCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator SavesAPictureOfTheHoverTargets() {
        yield return Hold(1f);
        Vector2 middle = (Vector2)Stage.Stride.transform.position + new Vector2(2f, 4f);
        HoverTarget still = Spawn(middle + new Vector2(-6f, 2f), new HoverTargetSettings { extent = Vector2.zero, bobHeight = 0f, path = HoverPathKind.Loop });
        HoverTarget leaning = Spawn(middle + new Vector2(-1f, 3f), new HoverTargetSettings { path = HoverPathKind.Patrol, extent = new Vector2(2f, 0f), period = 3f, startShare = 0.25f });
        HoverTarget broken = Spawn(middle + new Vector2(4f, 2f), new HoverTargetSettings { extent = Vector2.zero, bobHeight = 0f, path = HoverPathKind.Loop, respawnDelay = 30f });
        yield return Hold(0.3f);
        Effects.Popups.Show(10, still.Position, false);
        Effects.Popups.Show(10, leaning.Position + new Vector2(0.2f, 0.4f), false);
        Effects.Popups.Show(50, broken.Position, true);
        // Stop one in the middle of running down its rotor, on the way down.
        broken.OnShot(new ShotHit(broken.GetComponent<Collider2D>(), (broken.Position, Vector2.left, Vector2.right), (1f, 0f)));
        broken.OnShot(new ShotHit(broken.GetComponent<Collider2D>(), (broken.Position, Vector2.left, Vector2.right), (1f, 0f)));
        broken.OnShot(new ShotHit(broken.GetComponent<Collider2D>(), (broken.Position, Vector2.left, Vector2.right), (1f, 0f)));
        yield return Hold(0.12f);
        Effects.Popups.Tick(0.25f);

        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        Texture2D picture = Capture(middle, 6.5f);
        File.WriteAllBytes(Path.Combine(folder, "hover-targets.png"), picture.EncodeToPNG());
        Object.Destroy(picture);
        Assert.That(File.Exists(Path.Combine(folder, "hover-targets.png")), Is.True);
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
