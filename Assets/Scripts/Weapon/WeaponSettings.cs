using System;
using UnityEngine;

// The pistol's ammunition, shot and reload. A class with initializers, so a scene saved before a field existed keeps its default.
[Serializable]
public class WeaponSettings {
    [Header("Magazine")]
    public int magazineSize = 12;
    [Tooltip("Seconds a reload takes, from the press (or the empty click) to a full magazine.")]
    public float reloadDuration = 1.1f;

    [Header("Shot")]
    [Tooltip("How far a bullet flies before it gives up.")]
    public float range = 40f;
    public float damage = 1f;
    [Tooltip("Momentum given to a dynamic Rigidbody2D at the point it is hit.")]
    public float impulse = 6f;
    [Tooltip("What a bullet can hit. The character's own colliders are always ignored.")]
    public LayerMask hitMask = Physics2D.DefaultRaycastLayers;

    [Header("Tracer")]
    public float tracerWidth = 0.05f;
    [Tooltip("Seconds a tracer takes to fade out.")]
    public float tracerDuration = 0.09f;
    public Color tracerColor = new Color(1f, 0.85f, 0.45f, 1f);

    [Header("Reload motion")]
    [Tooltip("How far below its grip the support hand goes to fetch the magazine.")]
    public float reloadHandDrop = 0.6f;
    [Tooltip("How far the pistol tilts up during the reload, in degrees.")]
    public float reloadTilt = 14f;

    public WeaponSettings Clone() => (WeaponSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the weapon holds by reference.
    public void CopyFrom(WeaponSettings source) {
        magazineSize = source.magazineSize;
        reloadDuration = source.reloadDuration;
        range = source.range;
        damage = source.damage;
        impulse = source.impulse;
        hitMask = source.hitMask;
        tracerWidth = source.tracerWidth;
        tracerDuration = source.tracerDuration;
        tracerColor = source.tracerColor;
        reloadHandDrop = source.reloadHandDrop;
        reloadTilt = source.reloadTilt;
    }
}
