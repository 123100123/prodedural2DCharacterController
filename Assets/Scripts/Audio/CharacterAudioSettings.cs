using System;
using UnityEngine;

// How loud the procedural sounds are and how much each play varies: a master volume and mute, a volume per category, and
// the impact speeds that decide how loud a footstep or landing is. A class with initializers, so a scene saved before a
// field existed keeps its default. Named apart from UnityEngine.AudioSettings.
[Serializable]
public class CharacterAudioSettings {
    [Header("Mix")]
    [Tooltip("Scales every sound; 0 is silence (the debug panel offers only number rows, so 0 there is the mute).")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Tooltip("Silences every sound whatever the volumes.")]
    public bool mute;
    [Tooltip("Footsteps, the jump whoosh and the roll.")]
    [Range(0f, 1f)] public float stepsVolume = 1f;
    [Tooltip("The landing thump.")]
    [Range(0f, 1f)] public float landingVolume = 1f;
    [Tooltip("Gunshot, dry click, reload clicks and the shell casing's tinkle.")]
    [Range(0f, 1f)] public float weaponVolume = 1f;
    [Tooltip("Bullets striking a target or a wall.")]
    [Range(0f, 1f)] public float impactsVolume = 1f;
    [Tooltip("How far each play's pitch varies around its own, as a share (0.08 is up to 8% either way); volume varies about half as much.")]
    [Range(0f, 0.5f)] public float pitchJitter = 0.08f;

    [Header("Impact scaling")]
    [Tooltip("Foot plants slower than this impact speed (u/s) make no sound. A steady walk plants at about 0.6 u/s, a run at 2.5 to 3.")]
    public float footMinImpact = 0.25f;
    [Tooltip("Impact speed (u/s) of a foot plant that sounds loudest.")]
    public float footFullImpact = 3.5f;
    [Tooltip("Landings slower than this impact speed (u/s) make no sound.")]
    public float landingMinImpact = 2.5f;
    [Tooltip("Impact speed (u/s) of the loudest, deepest landing.")]
    public float landingFullImpact = 20f;

    public CharacterAudioSettings Clone() => (CharacterAudioSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the audio host holds by reference.
    public void CopyFrom(CharacterAudioSettings source) {
        masterVolume = source.masterVolume;
        mute = source.mute;
        stepsVolume = source.stepsVolume;
        landingVolume = source.landingVolume;
        weaponVolume = source.weaponVolume;
        impactsVolume = source.impactsVolume;
        pitchJitter = source.pitchJitter;
        footMinImpact = source.footMinImpact;
        footFullImpact = source.footFullImpact;
        landingMinImpact = source.landingMinImpact;
        landingFullImpact = source.landingFullImpact;
    }
}
