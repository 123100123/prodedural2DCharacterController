using System;
using UnityEngine;

// The look of the lighting: a readable ambient light everywhere, a soft light about the player and a short, warm,
// shadow-casting flash at the muzzle of the pistol. A class so a setting added later keeps its default in scenes saved
// before it existed.
[Serializable]
public class LightingSettings {
    [Header("Ambient (global light)")]
    [Tooltip("The cool, dim light that keeps the whole scene readable.")]
    public Color ambientColor = new Color(0.624f, 0.706f, 0.863f);
    [Tooltip("Never set so low that the scene becomes hard to see: 0.4 is a dusk, 1 is plain daylight.")]
    public float ambientIntensity = 0.65f;

    [Header("Player light")]
    public bool playerEnabled = true;
    public Color playerColor = new Color(1f, 0.85f, 0.63f);
    public float playerIntensity = 0.6f;
    public float playerInnerRadius = 0.5f;
    public float playerOuterRadius = 6f;
    [Tooltip("How softly the light fades out at its edge: 1 is the softest.")]
    [Range(0f, 1f)] public float playerFalloff = 0.7f;
    [Tooltip("Where the light sits relative to the pelvis (x forward of the facing is ignored: the light is centred, y up).")]
    public float playerHeight = 1.2f;

    [Header("Muzzle flash")]
    public Color muzzleColor = new Color(1f, 0.604f, 0.235f);
    public float muzzlePeakIntensity = 2.5f;
    public float muzzleInnerRadius = 0.4f;
    public float muzzleOuterRadius = 5f;
    [Tooltip("Seconds from the shot until the flash is gone.")]
    public float muzzleDuration = 0.09f;
    [Tooltip("How fast the flash dies: 1 fades evenly, higher values drop quickly and linger faintly.")]
    public float muzzleDecayExponent = 3f;
    [Tooltip("The share of its radius the flash shrinks by as it dies.")]
    [Range(0f, 1f)] public float muzzleRadiusDecay = 0.35f;
    public bool muzzleCastsShadows = true;
    [Range(0f, 1f)] public float muzzleShadowIntensity = 0.6f;

    public LightingSettings Clone() => (LightingSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the lighting controller holds.
    public void CopyFrom(LightingSettings source) {
        ambientColor = source.ambientColor;
        ambientIntensity = source.ambientIntensity;
        playerEnabled = source.playerEnabled;
        playerColor = source.playerColor;
        playerIntensity = source.playerIntensity;
        playerInnerRadius = source.playerInnerRadius;
        playerOuterRadius = source.playerOuterRadius;
        playerFalloff = source.playerFalloff;
        playerHeight = source.playerHeight;
        muzzleColor = source.muzzleColor;
        muzzlePeakIntensity = source.muzzlePeakIntensity;
        muzzleInnerRadius = source.muzzleInnerRadius;
        muzzleOuterRadius = source.muzzleOuterRadius;
        muzzleDuration = source.muzzleDuration;
        muzzleDecayExponent = source.muzzleDecayExponent;
        muzzleRadiusDecay = source.muzzleRadiusDecay;
        muzzleCastsShadows = source.muzzleCastsShadows;
        muzzleShadowIntensity = source.muzzleShadowIntensity;
    }
}
