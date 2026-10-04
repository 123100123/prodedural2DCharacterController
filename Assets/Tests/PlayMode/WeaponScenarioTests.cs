using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The pistol on the real character: aiming and clicking (through the virtual mouse, like a player) fires a hitscan shot
// that hurts a shooting target, shoves a dynamic body and draws a tracer; the magazine empties, clicks dry and reloads.
public class WeaponScenarioTests : ScenarioFixture {
    private const int FramesBetweenShots = 14;
    private const int FramesBeforeFirstClick = 30;

    // The editor remaps the virtual mouse's position, so the point asked for is not the point the game reads: the pointer
    // is held still for a few frames and the point the stance really aims at is measured.
    private Vector2 aimRequest;
    private Vector2 aimedPoint;

    private WeaponController Weapon => Stage.Character.Parts.WeaponController;
    private WeaponSettings Settings => Stage.Character.Parts.Weapon;

    private IEnumerator FindWhereTheMouseAims() {
        aimRequest = (Vector2)Stage.Stride.transform.position + new Vector2(3f, 1.5f);
        for (int frame = 0; frame < 8; frame++) {
            Stage.AimAndFire(aimRequest, false);
            yield return null;
        }
        Camera camera = Camera.main;
        Vector2 pointer = Stage.Character.LatestCommands.PointerScreenPosition;
        aimedPoint = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z));
        Report($"aim request {aimRequest}, the game reads the pointer at {pointer}, aims at {aimedPoint}");
        Assert.That(aimedPoint.x, Is.GreaterThan(Stage.Stride.transform.position.x + 2f), "the aim point lies ahead of the character");
    }

    // A tall box centred on the aimed point (moved along x by the offset), hit by any bullet flying roughly at it.
    private ShootingTarget SpawnTarget(float xOffset = 0f, bool dynamicBody = false) {
        GameObject box = new GameObject("scenarioTarget");
        box.transform.position = new Vector3(aimedPoint.x + xOffset, aimedPoint.y, 0f);
        box.AddComponent<BoxCollider2D>().size = new Vector2(1f, 6f);
        box.AddComponent<SpriteRenderer>();
        if (dynamicBody) {
            Rigidbody2D rigidbody = box.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.mass = 1f;
        }
        return box.AddComponent<ShootingTarget>();
    }

    // Clicks once every few frames for the given frames while aiming at the same screen point; the first frames only
    // raise the arms.
    private IEnumerator AimAndClick(int frames) {
        for (int frame = 0; frame < frames; frame++) {
            bool isClicking = frame >= FramesBeforeFirstClick && (frame - FramesBeforeFirstClick) % FramesBetweenShots < 3;
            Stage.AimAndFire(aimRequest, isClicking);
            yield return null;
        }
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator AShotHitsTheTargetAndHurtsItAndTheShotHitEventCarriesTheHit() {
        ShotHit? lastHit = null;
        int shotHits = 0;
        int tracersSeen = 0;
        Stage.Character.Events.ShotHit += hit => {
            lastHit = hit;
            shotHits++;
            tracersSeen = Mathf.Max(tracersSeen, Weapon.Tracers.ActiveCount);
        };
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        ShootingTarget target = SpawnTarget();

        yield return AimAndClick(FramesBeforeFirstClick + 2 * FramesBetweenShots);

        Assert.That(shotHits, Is.GreaterThan(0), "the ShotHit event fires");
        Assert.That(target.HitCount, Is.EqualTo(shotHits), "the target was notified of each hit");
        Assert.That(target.Health, Is.LessThan(target.MaxHealth));
        Assert.That(lastHit.Value.Collider, Is.SameAs(target.GetComponent<BoxCollider2D>()));
        Assert.That(lastHit.Value.Normal.x, Is.LessThan(-0.5f), "the surface faces the shooter");
        Assert.That(lastHit.Value.Direction.x, Is.GreaterThan(0.5f), "the bullet travelled toward the target");
        Assert.That(lastHit.Value.Damage, Is.EqualTo(Settings.damage));
        Assert.That(tracersSeen, Is.GreaterThan(0), "a tracer was drawn");
        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(Weapon.Magazine.Capacity - 2), "two shots left the pistol");
    }

    [UnityTest]
    public IEnumerator ATargetShotToPiecesVanishesThenRespawnsWithFullHealth() {
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        ShootingTarget target = SpawnTarget();

        yield return AimAndClick(FramesBeforeFirstClick + 3 * FramesBetweenShots + 5);

        Assert.That(target.IsDestroyed, Is.True, "three hits finish it");
        Assert.That(target.GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(target.GetComponent<BoxCollider2D>().enabled, Is.False);

        yield return Hold(3.5f);
        Assert.That(target.IsDestroyed, Is.False, "it comes back");
        Assert.That(target.Health, Is.EqualTo(target.MaxHealth));
        Assert.That(target.GetComponent<SpriteRenderer>().enabled, Is.True);
        Assert.That(target.GetComponent<BoxCollider2D>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator ABulletPushesADynamicBodyAwayFromTheShooter() {
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        ShootingTarget target = SpawnTarget(dynamicBody: true);
        Rigidbody2D rigidbody = target.GetComponent<Rigidbody2D>();

        float fastest = 0f;
        for (int frame = 0; frame < FramesBeforeFirstClick + 20; frame++) {
            Stage.AimAndFire(aimRequest, frame >= FramesBeforeFirstClick && frame < FramesBeforeFirstClick + 3);
            yield return null;
            fastest = Mathf.Max(fastest, rigidbody.linearVelocity.x);
        }
        Stage.StopAiming();

        Assert.That(target.HitCount, Is.GreaterThan(0));
        Assert.That(fastest, Is.GreaterThan(1f), "the impulse set it moving away");
    }

    [UnityTest]
    public IEnumerator ABulletStopsAtWhateverIsInTheWay() {
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        ShootingTarget near = SpawnTarget(-1.5f);
        ShootingTarget far = SpawnTarget(1.5f);

        yield return AimAndClick(FramesBeforeFirstClick + 2 * FramesBetweenShots);

        Assert.That(near.HitCount, Is.GreaterThan(0), "the nearer target takes the bullet");
        Assert.That(far.HitCount, Is.Zero, "the one behind it is shielded");
    }

    [UnityTest]
    public IEnumerator TheReloadKeyRefillsAPartlyEmptyMagazineAfterTheReloadDuration() {
        int started = 0;
        int finished = 0;
        Stage.Character.Events.ReloadStarted += _ => started++;
        Stage.Character.Events.ReloadFinished += _ => finished++;
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnTarget();
        yield return AimAndClick(FramesBeforeFirstClick + 2 * FramesBetweenShots);
        int remaining = Weapon.Magazine.Rounds;
        Assert.That(remaining, Is.LessThan(Weapon.Magazine.Capacity), "some rounds are gone");

        Stage.Press(Key.R);
        yield return null;
        Stage.Release(Key.R);
        yield return null;
        Assert.That(Weapon.IsReloading, Is.True);
        Assert.That(started, Is.EqualTo(1));
        yield return Hold(Settings.reloadDuration * 0.5f);
        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(remaining), "still not loaded halfway");
        Assert.That(finished, Is.Zero);

        yield return Hold(Settings.reloadDuration * 0.6f);
        Assert.That(Weapon.IsReloading, Is.False);
        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(Weapon.Magazine.Capacity));
        Assert.That(finished, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AnEmptyMagazineClicksDryAndReloadsBySelf() {
        Settings.magazineSize = 2;
        int fired = 0;
        int dry = 0;
        Stage.Character.Events.Fired += _ => fired++;
        Stage.Character.Events.DryFired += _ => dry++;
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnTarget();

        yield return AimAndClick(FramesBeforeFirstClick + 3 * FramesBetweenShots + 5);

        Assert.That(fired, Is.EqualTo(2), "only two rounds");
        Assert.That(dry, Is.EqualTo(1), "the third click is dry");
        Assert.That(Weapon.IsReloading, Is.True, "and starts the reload");

        yield return Hold(Settings.reloadDuration + 0.3f);
        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator TheSupportHandDropsToTheMagazineWhileAimingAndReloading() {
        ArmLimb supportArm = (ArmLimb)typeof(CharacterDriver).GetField("backArm", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Stage.Character);
        yield return Hold(1f);
        yield return FindWhereTheMouseAims();
        SpawnTarget();
        yield return AimAndClick(FramesBeforeFirstClick + 3);
        Assert.That(Weapon.Magazine.Rounds, Is.LessThan(Weapon.Magazine.Capacity));

        float restHandY = 0f;
        float lowestHandY = float.MaxValue;
        for (int frame = 0; frame < 90; frame++) {
            Stage.AimAndFire(aimRequest, false);
            if (frame == 5) {
                Stage.Press(Key.R);
            }
            if (frame == 6) {
                Stage.Release(Key.R);
            }
            yield return null;
            if (frame == 4) {
                restHandY = supportArm.HandPosition.y;
            }
            lowestHandY = Mathf.Min(lowestHandY, supportArm.HandPosition.y);
        }
        Stage.StopAiming();

        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(Weapon.Magazine.Capacity));
        Assert.That(restHandY - lowestHandY, Is.GreaterThan(0.2f), "the support hand went down for the magazine");
    }
}
