using System;
using UnityEngine;

// The pistol's firing rate and where its muzzle sits. What a shot does is the weapon controller's business.
[Serializable]
public class Pistol {
    [SerializeField] private float cooldown = 0.18f;
    [Tooltip("Muzzle distance in front of the hand along the aim.")]
    [SerializeField] private float muzzleOffset = 0.15f;

    private float readyTime;

    // Tuning: seconds between shots, and how far in front of the hand the muzzle is.
    public float Cooldown { get => cooldown; set => cooldown = value; }
    public float MuzzleOffset { get => muzzleOffset; set => muzzleOffset = value; }

    public Vector2 MuzzleFor((Vector2 hand, Vector2 aimDirection) grip) => grip.hand + grip.aimDirection * muzzleOffset;

    // True, and the pistol is busy for the cooldown, when it is ready to fire.
    public bool TryFire(float now) {
        if (now < readyTime) {
            return false;
        }

        readyTime = now + cooldown;
        return true;
    }
}
