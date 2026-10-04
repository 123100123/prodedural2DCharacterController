using System;
using UnityEngine;

// How the follow camera frames the aim and shakes: where it leans while aiming and how much shake each moment adds.
// A class with initializers, so a scene saved before a field existed keeps its default.
[Serializable]
public class CameraFeelSettings {
    [Header("Aim framing")]
    [Tooltip("Share of the way from the character to the pointer that the camera leans while aiming.")]
    [Range(0f, 1f)] public float aimFraction = 0.3f;
    [Tooltip("The furthest the aim moves the camera from the character, in world units, horizontally and vertically.")]
    public Vector2 maxAimOffset = new Vector2(3.5f, 2f);
    [Tooltip("How the camera eases into the aim framing when aiming starts and out of it when it ends.")]
    public SpringSettings aimBlendSpring = new SpringSettings(2.5f, 1f, 0f);

    [Header("Shake")]
    [Tooltip("Accessibility switch: off, nothing shakes the camera.")]
    public bool shakeEnabled = true;
    [Tooltip("Scales every shake, 1 is the full effect and 0 is none.")]
    [Range(0f, 1f)] public float shakeAmount = 1f;
    [Tooltip("Furthest the camera is shoved by a shake at full trauma, in world units.")]
    public float maxShakeOffset = 0.5f;
    [Tooltip("Furthest the camera rolls at full trauma, in degrees.")]
    public float maxShakeRoll = 2f;
    [Tooltip("How fast the shake wanders, in noise cycles per second.")]
    public float shakeFrequency = 22f;
    [Tooltip("Trauma lost per second; a full 1 settles in 1 / this many seconds.")]
    public float traumaDecay = 1.6f;

    [Header("Trauma sources")]
    [Tooltip("Trauma from each shot.")]
    public float firedTrauma = 0.25f;
    [Tooltip("Trauma from a bullet striking something.")]
    public float shotHitTrauma = 0.04f;
    [Tooltip("Landings slower than this impact speed (u/s) shake nothing.")]
    public float landingTraumaThreshold = 10f;
    [Tooltip("Trauma per unit of impact speed above the threshold.")]
    public float landingTraumaPerSpeed = 0.05f;

    public CameraFeelSettings Clone() => (CameraFeelSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the camera holds by reference.
    public void CopyFrom(CameraFeelSettings source) {
        aimFraction = source.aimFraction;
        maxAimOffset = source.maxAimOffset;
        aimBlendSpring = source.aimBlendSpring;
        shakeEnabled = source.shakeEnabled;
        shakeAmount = source.shakeAmount;
        maxShakeOffset = source.maxShakeOffset;
        maxShakeRoll = source.maxShakeRoll;
        shakeFrequency = source.shakeFrequency;
        traumaDecay = source.traumaDecay;
        firedTrauma = source.firedTrauma;
        shotHitTrauma = source.shotHitTrauma;
        landingTraumaThreshold = source.landingTraumaThreshold;
        landingTraumaPerSpeed = source.landingTraumaPerSpeed;
    }
}
