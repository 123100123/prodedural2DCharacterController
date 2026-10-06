using System;
using UnityEngine;

// What the procedural effects (dust, sparks, smoke, shell casings) do and how much of it: a switch, a scale per kind
// where 0 turns that kind off, and the impact speeds that decide how big a puff is. A class with initializers, so a
// scene saved before a field existed keeps its default.
[Serializable]
public class EffectsSettings {
    [Header("General")]
    [Tooltip("Accessibility and performance switch: off, nothing is emitted.")]
    public bool enabled = true;

    [Header("Dust")]
    [Tooltip("Scales every dust puff (count and size); 0 is no dust.")]
    public float dustScale = 1f;
    [Tooltip("Foot plants slower than this impact speed (u/s) raise no dust.")]
    public float footDustMinImpact = 1.5f;
    [Tooltip("Impact speed (u/s) of a foot plant that raises the biggest puff.")]
    public float footDustFullImpact = 5f;
    [Tooltip("Landings slower than this impact speed (u/s) raise no dust.")]
    public float landingDustMinImpact = 4f;
    [Tooltip("Impact speed (u/s) of a landing that raises the biggest spray.")]
    public float landingDustFullImpact = 20f;
    [Tooltip("Puffs raised under the feet at a jump take-off, before the dust scale.")]
    public float jumpDustCount = 4f;
    [Tooltip("Puffs raised by the hands touching down in a roll, before the dust scale.")]
    public float rollDustCount = 6f;
    [Tooltip("How long a puff lives, in seconds.")]
    public float dustLifetime = 0.7f;

    [Header("Gun")]
    [Tooltip("Scales the sparks at the muzzle and where bullets strike; 0 is no sparks.")]
    public float sparkScale = 1f;
    [Tooltip("Scales the smoke curling from the muzzle; 0 is no smoke.")]
    public float smokeScale = 1f;
    [Tooltip("Seconds a shell casing lies on the ground before it is gone; 0 ejects no casings.")]
    public float casingLifetime = 4f;
    [Tooltip("Seconds the casing takes to fade out at the end of its life.")]
    public float casingFadeTime = 0.8f;
    [Tooltip("How fast a casing is thrown out of the pistol, in u/s.")]
    public float casingEjectSpeed = 3.5f;

    [Header("Targets")]
    [Tooltip("Points popups and wood splinters of the targets that pay out.")]
    public TargetEffectsSettings targets = new TargetEffectsSettings();

    public EffectsSettings Clone() {
        EffectsSettings copy = (EffectsSettings)MemberwiseClone();
        copy.targets = targets.Clone();
        return copy;
    }

    // A copy made field by field into this instance, which the effects hold by reference.
    public void CopyFrom(EffectsSettings source) {
        enabled = source.enabled;
        dustScale = source.dustScale;
        footDustMinImpact = source.footDustMinImpact;
        footDustFullImpact = source.footDustFullImpact;
        landingDustMinImpact = source.landingDustMinImpact;
        landingDustFullImpact = source.landingDustFullImpact;
        jumpDustCount = source.jumpDustCount;
        rollDustCount = source.rollDustCount;
        dustLifetime = source.dustLifetime;
        sparkScale = source.sparkScale;
        smokeScale = source.smokeScale;
        casingLifetime = source.casingLifetime;
        casingFadeTime = source.casingFadeTime;
        casingEjectSpeed = source.casingEjectSpeed;
        targets.CopyFrom(source.targets);
    }
}

// The points popups a target's score raises and the wood splinters of a target breaking. A class of its own with
// initializers, so a scene saved before it existed keeps these defaults.
[Serializable]
public class TargetEffectsSettings {
    [Tooltip("Seconds a popup takes to rise and fade out.")]
    public float popupLifetime = 1.2f;
    [Tooltip("How far a popup rises, in world units.")]
    public float popupRise = 1.5f;
    [Tooltip("Letter size of the popup for a hit, and for the hit that breaks the target.")]
    public float hitTextSize = 0.12f;
    public float breakTextSize = 0.2f;
    [Tooltip("How far above the target's middle a popup starts, in world units.")]
    public float popupLift = 1.6f;
    public Color hitTextColor = new Color(1f, 0.97f, 0.82f, 1f);
    public Color breakTextColor = new Color(1f, 0.78f, 0.12f, 1f);
    [Tooltip("Splinters knocked off by a hit, and sprayed by the hit that breaks the target; 0 is none.")]
    public float hitSplinters = 2f;
    public float breakSplinters = 16f;

    public TargetEffectsSettings Clone() => (TargetEffectsSettings)MemberwiseClone();

    public void CopyFrom(TargetEffectsSettings source) {
        popupLifetime = source.popupLifetime;
        popupRise = source.popupRise;
        hitTextSize = source.hitTextSize;
        breakTextSize = source.breakTextSize;
        popupLift = source.popupLift;
        hitTextColor = source.hitTextColor;
        breakTextColor = source.breakTextColor;
        hitSplinters = source.hitSplinters;
        breakSplinters = source.breakSplinters;
    }
}
