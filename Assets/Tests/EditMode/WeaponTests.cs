using NUnit.Framework;
using UnityEngine;

public class WeaponTests {
    private static readonly (Vector2 muzzle, Vector2 direction) Shot = (new Vector2(0f, 1000f), Vector2.up);

    private GameObject owner;
    private CharacterEvents events;
    private WeaponSettings settings;
    private WeaponController weapon;
    private int fired;
    private int dryFired;
    private int reloadsStarted;
    private int reloadsFinished;

    [SetUp]
    public void Build() {
        owner = new GameObject("weaponTestOwner");
        events = new CharacterEvents();
        settings = new WeaponSettings { magazineSize = 3, reloadDuration = 1f };
        weapon = new WeaponController(new Pistol { Cooldown = 0f }, settings, (events, owner.transform));
        fired = dryFired = reloadsStarted = reloadsFinished = 0;
        events.Fired += _ => fired++;
        events.DryFired += _ => dryFired++;
        events.ReloadStarted += _ => reloadsStarted++;
        events.ReloadFinished += _ => reloadsFinished++;
    }

    [TearDown]
    public void Discard() {
        weapon.Dispose();
        Object.DestroyImmediate(owner);
    }

    [Test]
    public void EachShotTakesARound() {
        Assert.That(weapon.Magazine.Rounds, Is.EqualTo(3));
        Assert.That(weapon.PullTrigger(Shot, 0f), Is.EqualTo(TriggerResult.Fired));
        Assert.That(weapon.PullTrigger(Shot, 0.1f), Is.EqualTo(TriggerResult.Fired));
        Assert.That(weapon.Magazine.Rounds, Is.EqualTo(1));
        Assert.That(fired, Is.EqualTo(2));
    }

    [Test]
    public void ATracerFliesWithEveryShot() {
        weapon.PullTrigger(Shot, 0f);
        Assert.That(weapon.Tracers.ActiveCount, Is.EqualTo(1));
        weapon.Tick(0.5f, settings.tracerDuration + 0.01f, false);
        Assert.That(weapon.Tracers.ActiveCount, Is.Zero, "the tracer fades out");
    }

    [Test]
    public void TheShotCooldownBlocksAFastSecondPull() {
        WeaponController slow = new WeaponController(new Pistol { Cooldown = 0.5f }, settings, (events, owner.transform));
        Assert.That(slow.PullTrigger(Shot, 0f), Is.EqualTo(TriggerResult.Fired));
        Assert.That(slow.PullTrigger(Shot, 0.2f), Is.EqualTo(TriggerResult.Blocked));
        Assert.That(slow.Magazine.Rounds, Is.EqualTo(2), "a blocked pull costs no round");
        slow.Dispose();
    }

    [Test]
    public void PullingTheTriggerOnAnEmptyMagazineClicksAndStartsAReload() {
        EmptyTheMagazine();

        Assert.That(weapon.PullTrigger(Shot, 1f), Is.EqualTo(TriggerResult.DryFired));
        Assert.That(dryFired, Is.EqualTo(1));
        Assert.That(reloadsStarted, Is.EqualTo(1));
        Assert.That(weapon.IsReloading, Is.True);
        Assert.That(fired, Is.EqualTo(3), "no shot comes out");
    }

    [Test]
    public void CannotFireWhileReloadingAndRepeatedPullsDoNotClickAgain() {
        EmptyTheMagazine();
        weapon.PullTrigger(Shot, 1f);

        Assert.That(weapon.PullTrigger(Shot, 1.2f), Is.EqualTo(TriggerResult.Blocked));
        Assert.That(dryFired, Is.EqualTo(1));
        Assert.That(reloadsStarted, Is.EqualTo(1));
    }

    [Test]
    public void AReloadRefillsTheMagazineAfterItsDuration() {
        EmptyTheMagazine();
        weapon.PullTrigger(Shot, 1f);

        weapon.Tick(1.9f, 0.016f, false);
        Assert.That(weapon.IsReloading, Is.True, "not done yet");
        Assert.That(weapon.Magazine.Rounds, Is.Zero);
        Assert.That(weapon.ReloadProgress(1.5f), Is.EqualTo(0.5f).Within(0.001f));

        weapon.Tick(2.01f, 0.016f, false);
        Assert.That(weapon.IsReloading, Is.False);
        Assert.That(weapon.Magazine.Rounds, Is.EqualTo(3));
        Assert.That(reloadsFinished, Is.EqualTo(1));
        Assert.That(weapon.PullTrigger(Shot, 2.1f), Is.EqualTo(TriggerResult.Fired), "it fires again");
    }

    [Test]
    public void TheReloadButtonReloadsAPartlyEmptyMagazine() {
        weapon.PullTrigger(Shot, 0f);

        weapon.Tick(0.5f, 0.016f, true);
        Assert.That(weapon.IsReloading, Is.True);
        Assert.That(reloadsStarted, Is.EqualTo(1));

        weapon.Tick(1.6f, 0.016f, false);
        Assert.That(weapon.Magazine.Rounds, Is.EqualTo(3));
    }

    [Test]
    public void ThereIsNoReloadWhenTheMagazineIsFullOrAlreadyReloading() {
        weapon.Tick(0f, 0.016f, true);
        Assert.That(weapon.IsReloading, Is.False, "full");
        Assert.That(reloadsStarted, Is.Zero);

        weapon.PullTrigger(Shot, 0f);
        weapon.Tick(0.1f, 0.016f, true);
        weapon.Tick(0.2f, 0.016f, true);
        Assert.That(reloadsStarted, Is.EqualTo(1), "a second press mid-reload changes nothing");
    }

    [Test]
    public void AReloadKeepsRunningWithoutAnyTriggerPulls() {
        weapon.PullTrigger(Shot, 0f);
        weapon.TryReload(0.1f);
        for (float now = 0.1f; now < 1.2f; now += 0.1f) {
            weapon.Tick(now, 0.1f, false);
        }
        Assert.That(weapon.Magazine.Rounds, Is.EqualTo(3));
    }

    [Test]
    public void AShrunkMagazineDropsItsExtraRoundsAndAGrownFullOneStaysFull() {
        Magazine magazine = new Magazine(12);
        magazine.Resize(8);
        Assert.That(magazine.Rounds, Is.EqualTo(8), "full stays full");
        magazine.Resize(12);
        magazine.TryConsumeRound();
        magazine.Resize(20);
        Assert.That(magazine.Rounds, Is.EqualTo(11), "a part-empty magazine does not grow to fill");
        magazine.Resize(6);
        Assert.That(magazine.Rounds, Is.EqualTo(6), "a shrunk one drops the extra rounds");
        Assert.That(new Magazine(2).Rounds, Is.EqualTo(2));
    }

    [Test]
    public void TheReloadHandDropsHoldsAndSlapsBackToTheGrip() {
        Assert.That(ReloadMotion.HandDrop(0f), Is.Zero);
        Assert.That(ReloadMotion.HandDrop(0.15f), Is.InRange(0.2f, 0.8f), "on the way down");
        Assert.That(ReloadMotion.HandDrop(0.45f), Is.EqualTo(1f), "at the magazine");
        Assert.That(ReloadMotion.HandDrop(0.72f), Is.InRange(0.1f, 0.9f), "on the way up");
        Assert.That(ReloadMotion.HandDrop(0.9f), Is.Zero, "back at the grip before the end");
        Assert.That(ReloadMotion.HandDrop(1f), Is.Zero);
    }

    [Test]
    public void ThePistolTiltsUpDuringTheReloadAndSettlesLevel() {
        Assert.That(ReloadMotion.Tilt(0f), Is.Zero);
        Assert.That(ReloadMotion.Tilt(0.5f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(ReloadMotion.Tilt(0.8f), Is.GreaterThan(1f), "kicks up a little at the slap");
        Assert.That(ReloadMotion.Tilt(1f), Is.Zero);
    }

    [Test]
    public void TiltingRaisesTheAimTowardUpWhicheverWayTheCharacterFaces() {
        Vector2 right = ReloadMotion.TiltedUp(Vector2.right, (1, 10f));
        Assert.That(Vector2.SignedAngle(Vector2.right, right), Is.EqualTo(10f).Within(0.01f));
        Vector2 left = ReloadMotion.TiltedUp(Vector2.left, (-1, 10f));
        Assert.That(Vector2.SignedAngle(Vector2.left, left), Is.EqualTo(-10f).Within(0.01f), "facing left, up is clockwise");
        Assert.That(left.y, Is.GreaterThan(0f));
    }

    [Test]
    public void ATracerFadesFromFullToNothing() {
        Assert.That(TracerFade.Alpha(0f, 0.1f), Is.EqualTo(1f));
        Assert.That(TracerFade.Alpha(0.05f, 0.1f), Is.InRange(0.1f, 0.9f));
        Assert.That(TracerFade.Alpha(0.1f, 0.1f), Is.Zero);
        Assert.That(TracerFade.Alpha(0.2f, 0.1f), Is.Zero);
        Assert.That(TracerFade.Alpha(0f, 0f), Is.Zero);
    }

    [Test]
    public void WeaponSettingsSurviveAPresetRoundTripAndAnOldPresetKeepsTheDefaults() {
        MovementPreset preset = MovementPreset.CodeDefaults();
        preset.weapon.reloadDuration = 2.5f;
        preset.weapon.range = 12f;
        MovementPreset loaded = MovementPreset.FromJson(preset.ToJson());
        Assert.That(loaded.weapon.reloadDuration, Is.EqualTo(2.5f));
        Assert.That(loaded.weapon.range, Is.EqualTo(12f));

        MovementPreset old = MovementPreset.FromJson("{\"version\":2,\"pistolCooldown\":0.3}");
        Assert.That(old.weapon.magazineSize, Is.EqualTo(12));
        Assert.That(old.weapon.reloadDuration, Is.EqualTo(1.1f));
        Assert.That(old.pistolCooldown, Is.EqualTo(0.3f));
    }

    [Test]
    public void ACopiedPresetHasItsOwnWeaponSettings() {
        MovementPreset original = MovementPreset.CodeDefaults();
        MovementPreset copy = original.Copy();
        copy.weapon.damage = 9f;
        Assert.That(original.weapon.damage, Is.EqualTo(1f));
    }

    private void EmptyTheMagazine() {
        for (int shot = 0; shot < 3; shot++) {
            weapon.PullTrigger(Shot, shot * 0.1f);
        }
        Assert.That(weapon.Magazine.IsEmpty);
    }
}
