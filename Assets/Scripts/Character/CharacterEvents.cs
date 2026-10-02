using System;
using UnityEngine;

// Moments other systems react to (camera kicks now; sound and VFX later), raised by the locomotion and upper body.
public sealed class CharacterEvents {
    public event Action<(Vector2 position, float impactSpeed)> FootPlanted;
    public event Action<(Vector2 position, float impactSpeed)> Landed;
    public event Action<Vector2> Jumped;
    public event Action<(Vector2 muzzle, Vector2 direction)> Fired;

    public void RaiseFootPlanted((Vector2 position, float impactSpeed) plant) => FootPlanted?.Invoke(plant);

    public void RaiseLanded((Vector2 position, float impactSpeed) landing) => Landed?.Invoke(landing);

    public void RaiseJumped(Vector2 launchVelocity) => Jumped?.Invoke(launchVelocity);

    public void RaiseFired((Vector2 muzzle, Vector2 direction) shot) => Fired?.Invoke(shot);
}
