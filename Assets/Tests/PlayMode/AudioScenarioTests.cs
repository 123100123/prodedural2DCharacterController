using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The procedural audio on the real character: walking and running play footsteps, a shot plays the gunshot, the reload
// plays the magazine out at once and in near its end, landings scale with the impact, bullet strikes sound by what they
// hit, and the mix mutes. Sounds are counted on the audio host, so nothing here depends on hearing (the test runner may
// have the Editor's audio muted).
public class AudioScenarioTests : ScenarioFixture {
    private const int FramesBetweenShots = 14;
    private const int FramesBeforeFirstClick = 30;

    private Vector2 aimRequest;

    private CharacterAudio Audio => Stage.Character.Audio;
    private CharacterAudioSettings Settings => Stage.Character.Parts.Audio;
    private Vector2 Feet => new Vector2(Stage.Stride.transform.position.x, Stage.Stride.StanceHeight);

    private IEnumerator FindWhereTheMouseAims() {
        aimRequest = (Vector2)Stage.Stride.transform.position + new Vector2(3f, 1.5f);
        for (int frame = 0; frame < 8; frame++) {
            Stage.AimAndFire(aimRequest, false);
            yield return null;
        }
    }

    // Tall walls on both sides, so a shot meets one wherever the editor remaps the pointer.
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
    public IEnumerator TheSceneHasOneAudioListenerAndTheHostAddsNone() {
        yield return Hold(0.5f);
        Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Audio.AddedListener, Is.False, "the camera already had one");
    }

    [UnityTest]
    public IEnumerator WalkingPlaysFootstepsAndRunningPlaysMoreOfThem() {
        float strongestPlant = 0f;
        System.Collections.Generic.List<float> impacts = new System.Collections.Generic.List<float>();
        Stage.Character.Events.FootPlanted += plant => {
            strongestPlant = Mathf.Max(strongestPlant, plant.impactSpeed);
            impacts.Add(plant.impactSpeed);
        };
        yield return Hold(1f);
        Report($"after settling: {Audio.PlaysOf(SoundId.Footstep)} footsteps; impacts " + string.Join(", ", impacts.ConvertAll(i => i.ToString("F2"))));

        Stage.Press(Key.D);
        yield return Hold(2f);
        int walking = Audio.PlaysOf(SoundId.Footstep);
        Report($"walking: {walking} footsteps, strongest plant {strongestPlant:F2} u/s (silent below {Settings.footMinImpact})");
        Report("walk impacts: " + string.Join(", ", impacts.ConvertAll(i => i.ToString("F2"))));
        Assert.That(walking, Is.GreaterThan(2), "the feet planted and made sound");

        impacts.Clear();
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2f);
        Report("run impacts: " + string.Join(", ", impacts.ConvertAll(i => i.ToString("F2"))));
        int running = Audio.PlaysOf(SoundId.Footstep) - walking;
        Report($"running: {running} footsteps, strongest plant {strongestPlant:F2} u/s");
        Assert.That(running, Is.GreaterThan(2));
    }

    [UnityTest]
    public IEnumerator AGentleFootPlantIsSilentAndAFirmOneIsHeard() {
        yield return Hold(1f);
        int before = Audio.PlaysOf(SoundId.Footstep);
        Stage.Character.Events.RaiseFootPlanted((Feet, Settings.footMinImpact * 0.5f));
        Assert.That(Audio.PlaysOf(SoundId.Footstep), Is.EqualTo(before));
        Stage.Character.Events.RaiseFootPlanted((Feet, Settings.footFullImpact));
        Assert.That(Audio.PlaysOf(SoundId.Footstep), Is.EqualTo(before + 1));
    }

    [UnityTest]
    public IEnumerator FiringPlaysTheGunshotAndTheCasingTinklesWhenItHitsTheGround() {
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnWallsAround();
        int fired = 0;
        Stage.Character.Events.Fired += _ => fired++;

        yield return AimAndClick(FramesBeforeFirstClick + 2 * FramesBetweenShots);
        Report($"fired {fired}, gunshots {Audio.PlaysOf(SoundId.Gunshot)}, casing tinkles {Audio.PlaysOf(SoundId.Casing)}, ricochets {Audio.PlaysOf(SoundId.Ricochet)}");
        Assert.That(fired, Is.EqualTo(2));
        Assert.That(Audio.PlaysOf(SoundId.Gunshot), Is.EqualTo(2), "one gunshot per shot");
        Assert.That(Audio.PlaysOf(SoundId.Ricochet), Is.GreaterThan(0), "bullets struck plain walls");

        yield return Hold(1.5f);
        Assert.That(Audio.PlaysOf(SoundId.Casing), Is.GreaterThan(0), "a casing touched down");
        Assert.That(Audio.PlaysOf(SoundId.Casing), Is.LessThanOrEqualTo(fired * 6), "and tinkled only on its first contact");
    }

    [UnityTest]
    public IEnumerator ADryClickPlaysItsSound() {
        yield return Hold(0.5f);
        Stage.Character.Events.RaiseDryFired((Feet + Vector2.up, Vector2.right));
        Assert.That(Audio.PlaysOf(SoundId.DryClick), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AReloadDropsTheMagazineAtOnceAndSeatsTheNewOneNearTheEnd() {
        yield return Hold(0.5f);
        const float duration = 1.1f;
        float seats = AudioMath.MagazineInDelay(duration);
        Stage.Character.Events.RaiseReloadStarted(duration);
        Assert.That(Audio.PlaysOf(SoundId.MagazineOut), Is.EqualTo(1), "the magazine drops at once");
        Assert.That(Audio.PlaysOf(SoundId.MagazineIn), Is.Zero);

        yield return Hold(seats * 0.7f);
        Assert.That(Audio.PlaysOf(SoundId.MagazineIn), Is.Zero, "not yet");
        yield return Hold(seats * 0.5f);
        Assert.That(Audio.PlaysOf(SoundId.MagazineIn), Is.EqualTo(1), "seated by the scheduled time");
        yield return Hold(1f);
        Assert.That(Audio.PlaysOf(SoundId.MagazineIn), Is.EqualTo(1), "and only once");
    }

    [UnityTest]
    public IEnumerator TheRealReloadKeyPlaysBothMagazineSounds() {
        Settings.masterVolume = 1f;
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnWallsAround();
        yield return AimAndClick(FramesBeforeFirstClick + FramesBetweenShots);
        Stage.Press(Key.R);
        yield return null;
        Stage.Release(Key.R);
        yield return null;
        Assert.That(Audio.PlaysOf(SoundId.MagazineOut), Is.EqualTo(1));
        yield return Hold(Stage.Character.Parts.Weapon.reloadDuration + 0.3f);
        Assert.That(Audio.PlaysOf(SoundId.MagazineIn), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AHardLandingPlaysTheThumpAndAGentleOneIsSilent() {
        yield return Hold(0.5f);
        Stage.Character.Events.RaiseLanded((Feet, Settings.landingMinImpact * 0.5f));
        Assert.That(Audio.PlaysOf(SoundId.Landing), Is.Zero, "a gentle landing is silent");
        Stage.Character.Events.RaiseLanded((Feet, Settings.landingFullImpact));
        Assert.That(Audio.PlaysOf(SoundId.Landing), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator JumpingAndRollingPlayTheirSounds() {
        yield return Hold(0.5f);
        Stage.Character.Events.RaiseJumped(new Vector2(0f, 10f));
        Stage.Character.Events.RaiseRolled((Feet, 1));
        Assert.That(Audio.PlaysOf(SoundId.Jump), Is.EqualTo(1));
        Assert.That(Audio.PlaysOf(SoundId.Roll), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ABulletThudsOnATargetAndTicksOffPlainLevel() {
        yield return Hold(0.5f);
        GameObject box = new GameObject("scenarioTarget");
        box.transform.position = (Vector3)(Feet + new Vector2(3f, 2f));
        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        box.AddComponent<SpriteRenderer>();
        box.AddComponent<ShootingTarget>();
        GameObject plain = new GameObject("scenarioPlain");
        plain.transform.position = (Vector3)(Feet + new Vector2(-3f, 2f));
        BoxCollider2D plainCollider = plain.AddComponent<BoxCollider2D>();

        Stage.Character.Events.RaiseShotHit(new ShotHit(collider, (collider.transform.position, Vector2.left, Vector2.right), (1f, 1f)));
        Assert.That(Audio.PlaysOf(SoundId.TargetHit), Is.EqualTo(1));
        Assert.That(Audio.PlaysOf(SoundId.Ricochet), Is.Zero);

        Stage.Character.Events.RaiseShotHit(new ShotHit(plainCollider, (plain.transform.position, Vector2.right, Vector2.left), (1f, 1f)));
        Assert.That(Audio.PlaysOf(SoundId.TargetHit), Is.EqualTo(1));
        Assert.That(Audio.PlaysOf(SoundId.Ricochet), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AMasterVolumeOfZeroAndTheMuteSwitchSilenceEverything() {
        yield return Hold(0.5f);
        int before = Audio.TotalPlays;
        Settings.masterVolume = 0f;
        Stage.Character.Events.RaiseFootPlanted((Feet, 9f));
        Stage.Character.Events.RaiseFired((Feet + Vector2.up, Vector2.right));
        Assert.That(Audio.TotalPlays, Is.EqualTo(before), "master volume 0");

        Settings.masterVolume = 1f;
        Settings.mute = true;
        Stage.Character.Events.RaiseLanded((Feet, 25f));
        Stage.Character.Events.RaiseFired((Feet + Vector2.up, Vector2.right));
        Assert.That(Audio.TotalPlays, Is.EqualTo(before), "muted");

        Settings.mute = false;
        Settings.weaponVolume = 0f;
        Stage.Character.Events.RaiseFired((Feet + Vector2.up, Vector2.right));
        Assert.That(Audio.PlaysOf(SoundId.Gunshot), Is.Zero, "the weapon category at 0");
        int landings = Audio.PlaysOf(SoundId.Landing);
        Stage.Character.Events.RaiseLanded((Feet, 25f));
        Assert.That(Audio.PlaysOf(SoundId.Landing), Is.EqualTo(landings + 1), "other categories still sound");
    }

    [UnityTest]
    public IEnumerator ABurstOfMoreSoundsThanVoicesStealsVoicesWithoutAddingAny() {
        yield return Hold(0.5f);
        int played = 0;
        Audio.Played += _ => played++;
        for (int step = 0; step < CharacterAudio.VoiceCount * 3; step++) {
            Stage.Character.Events.RaiseFootPlanted((Feet, 9f));
        }
        Assert.That(played, Is.EqualTo(CharacterAudio.VoiceCount * 3));
        int pooled = 0;
        foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            pooled += source.gameObject.name == "Audio" ? 1 : 0;
        }
        Assert.That(pooled, Is.EqualTo(CharacterAudio.VoiceCount), "the pool never grows");
    }
}
