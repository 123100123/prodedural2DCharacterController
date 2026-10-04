using System.Collections.Generic;
using UnityEngine;

public enum TriggerResult {
    // A round left the pistol.
    Fired,
    // The magazine was empty: the trigger clicked and a reload began.
    DryFired,
    // Nothing happened: the pistol is reloading or still cooling down from the last shot.
    Blocked,
}

// The pistol as a weapon: its magazine and reload, and what a shot does (a hitscan ray from the muzzle, a tracer, the
// bullet's effect on whatever it strikes). The shooting stance only decides when the trigger is pulled and how the
// arms move; every moment is announced through the character's events.
public sealed class WeaponController {
    private const int MaxRayHits = 8;

    private readonly Pistol pistol;
    private readonly WeaponSettings settings;
    private readonly CharacterEvents events;
    private readonly Transform owner;
    private readonly TracerPool tracers = new TracerPool();
    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[MaxRayHits];
    private readonly List<IShotReceiver> receivers = new List<IShotReceiver>();

    public WeaponController(Pistol pistol, WeaponSettings settings, (CharacterEvents events, Transform owner) character) {
        this.pistol = pistol;
        this.settings = settings;
        events = character.events;
        owner = character.owner;
        Magazine = new Magazine(settings.magazineSize);
    }

    public Magazine Magazine { get; }
    public TracerPool Tracers => tracers;
    public bool IsReloading => Magazine.IsReloading;

    public Vector2 MuzzleFor((Vector2 hand, Vector2 aimDirection) grip) => pistol.MuzzleFor(grip);

    public float ReloadProgress(float now) => Magazine.ReloadProgress(now);

    // Once per frame, whether or not the character is aiming: a reload runs to its end regardless.
    public void Tick(float now, float deltaTime, bool reloadPressed) {
        Magazine.Resize(settings.magazineSize);
        tracers.Tick(deltaTime);
        if (Magazine.CompleteReloadIfDue(now)) {
            events.RaiseReloadFinished(Magazine.Rounds);
        }
        if (reloadPressed) {
            TryReload(now);
        }
    }

    // False when the magazine is full or a reload is already under way.
    public bool TryReload(float now) {
        if (!Magazine.TryStartReload(now, settings.reloadDuration)) {
            return false;
        }
        events.RaiseReloadStarted(settings.reloadDuration);
        return true;
    }

    public TriggerResult PullTrigger((Vector2 muzzle, Vector2 direction) shot, float now) {
        if (Magazine.IsReloading) {
            return TriggerResult.Blocked;
        }
        if (Magazine.IsEmpty) {
            events.RaiseDryFired(shot);
            TryReload(now);
            return TriggerResult.DryFired;
        }
        if (!pistol.TryFire(now)) {
            return TriggerResult.Blocked;
        }

        Magazine.TryConsumeRound();
        events.RaiseFired(shot);
        Hitscan(shot.muzzle, shot.direction.normalized);
        return TriggerResult.Fired;
    }

    public void Dispose() => tracers.Dispose();

    private void Hitscan(Vector2 origin, Vector2 direction) {
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = settings.hitMask, useTriggers = false };
        int count = Physics2D.Raycast(origin, direction, filter, rayHits, settings.range);
        for (int index = 0; index < count; index++) {
            Collider2D collider = rayHits[index].collider;
            if (collider == null || collider.transform.IsChildOf(owner)) {
                continue;
            }
            ShotHit hit = new ShotHit(collider, (rayHits[index].point, rayHits[index].normal, direction), (settings.damage, settings.impulse));
            tracers.Spawn((origin, hit.Point), settings);
            Strike(hit);
            return;
        }
        tracers.Spawn((origin, origin + direction * settings.range), settings);
    }

    // The bullet's effect: a push on a dynamic body, then every receiver on the collider's object and its body's object.
    private void Strike(ShotHit hit) {
        Rigidbody2D body = hit.Collider.attachedRigidbody;
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic) {
            body.AddForceAtPosition(hit.Direction * hit.Impulse, hit.Point, ForceMode2D.Impulse);
        }

        Notify(hit.Collider.gameObject, hit);
        if (body != null && body.gameObject != hit.Collider.gameObject) {
            Notify(body.gameObject, hit);
        }
        events.RaiseShotHit(hit);
    }

    private void Notify(GameObject target, ShotHit hit) {
        target.GetComponents(receivers);
        for (int index = 0; index < receivers.Count; index++) {
            receivers[index].OnShot(hit);
        }
    }
}
