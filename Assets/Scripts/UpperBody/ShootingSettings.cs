using System;
using UnityEngine;

[Serializable]
public class ShootingSettings {
    [Tooltip("Camera that converts the pointer to world space; falls back to the main camera.")]
    public Camera aimCamera;
    [Tooltip("Share of full arm length used when aiming, keeping a slight elbow bend.")]
    [Range(0.5f, 1f)] public float armExtension = 0.92f;
    [Tooltip("Aim is kept within this angle above and below horizontal in front of the character, so the arm never swings over the head.")]
    [Range(10f, 89f)] public float maxAimElevation = 80f;
    [Tooltip("How far behind the front hand, along the aim, the support hand grips.")]
    public float supportHandOffset = 0.1f;

    [Header("Recoil")]
    [Tooltip("Speed at which a shot throws the grip back and up.")]
    public float recoilKick = 5f;
    public SpringSettings recoilSpring = new SpringSettings(6f, 0.4f, 0f);
    [Tooltip("Upward share of the recoil relative to its backward push.")]
    public float recoilRise = 0.6f;
    [Tooltip("Backward chest lean speed (degrees per second) a shot gives the torso.")]
    public float recoilTorsoKick = 60f;

    public ShootingSettings Clone() => (ShootingSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the shooting stance holds by reference. The camera is a
    // scene reference, not a setting, and stays as it is.
    public void CopyFrom(ShootingSettings source) {
        armExtension = source.armExtension;
        maxAimElevation = source.maxAimElevation;
        supportHandOffset = source.supportHandOffset;
        recoilKick = source.recoilKick;
        recoilSpring = source.recoilSpring;
        recoilRise = source.recoilRise;
        recoilTorsoKick = source.recoilTorsoKick;
    }
}
