using System;
using UnityEngine;

// Fires shots at a limited rate. For now a shot is a debug message and a debug ray.
[Serializable]
public class Pistol {
    [SerializeField] private float cooldown = 0.18f;
    [Tooltip("Muzzle distance in front of the hand along the aim.")]
    [SerializeField] private float muzzleOffset = 0.15f;
    [SerializeField] private float debugRayLength = 12f;

    private float readyTime;

    public Vector2 MuzzleFor((Vector2 hand, Vector2 aimDirection) grip) => grip.hand + grip.aimDirection * muzzleOffset;

    public bool TryFire((Vector2 muzzle, Vector2 direction) shot, float now) {
        if (now < readyTime) {
            return false;
        }

        readyTime = now + cooldown;
        Debug.Log($"Pistol fired from {shot.muzzle} toward {shot.direction}");
        Debug.DrawRay(shot.muzzle, shot.direction * debugRayLength, Color.yellow, cooldown);
        return true;
    }
}
