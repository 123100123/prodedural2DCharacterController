using System;
using UnityEngine;

// Moments other systems react to (camera kicks and effects now; sound later), raised by the locomotion and upper body.
public sealed class CharacterEvents {
    public event Action<(Vector2 position, float impactSpeed)> FootPlanted;
    public event Action<(Vector2 position, float impactSpeed)> Landed;
    public event Action<Vector2> Jumped;
    public event Action<(Vector2 muzzle, Vector2 direction)> Fired;
    // A bullet struck a collider.
    public event Action<ShotHit> ShotHit;
    // The trigger was pulled on an empty magazine (a reload starts with it).
    public event Action<(Vector2 muzzle, Vector2 direction)> DryFired;
    // A reload began, with its duration in seconds.
    public event Action<float> ReloadStarted;
    // A reload ended, with the rounds now in the magazine.
    public event Action<int> ReloadFinished;
    // A roll's hands met the ground: where, and which way (+1 right, -1 left) the roll travels.
    public event Action<(Vector2 position, int direction)> Rolled;

    public void RaiseFootPlanted((Vector2 position, float impactSpeed) plant) => FootPlanted?.Invoke(plant);

    public void RaiseLanded((Vector2 position, float impactSpeed) landing) => Landed?.Invoke(landing);

    public void RaiseJumped(Vector2 launchVelocity) => Jumped?.Invoke(launchVelocity);

    public void RaiseFired((Vector2 muzzle, Vector2 direction) shot) => Fired?.Invoke(shot);

    public void RaiseShotHit(ShotHit hit) => ShotHit?.Invoke(hit);

    public void RaiseDryFired((Vector2 muzzle, Vector2 direction) shot) => DryFired?.Invoke(shot);

    public void RaiseReloadStarted(float duration) => ReloadStarted?.Invoke(duration);

    public void RaiseReloadFinished(int rounds) => ReloadFinished?.Invoke(rounds);

    public void RaiseRolled((Vector2 position, int direction) roll) => Rolled?.Invoke(roll);
}
