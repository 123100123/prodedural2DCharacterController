using System;
using UnityEngine;

// Hanging from a ledge: what the player's input chooses there, letting go, and the idle sway. A class with initializers,
// so a scene saved before a field existed keeps its default.
[Serializable]
public class LedgeHangSettings {
    [Tooltip("Off: the hang always carries straight on into the climb, as before.")]
    public bool holdable = true;
    [Tooltip("How far the stick (or a key) must be pushed up, down, toward or away from the ledge to count as a choice while hanging.")]
    [Range(0.1f, 1f)] public float intentThreshold = 0.5f;
    [Tooltip("Seconds after letting go of a ledge before the same ledge can be grabbed again.")]
    public float regrabLockout = 0.3f;

    [Header("Idle hang")]
    [Tooltip("How far the hanging body sways out from the wall and back.")]
    public float swayAmplitude = 0.06f;
    [Tooltip("Seconds per sway.")]
    public float swayPeriod = 2.6f;
    [Tooltip("How far the hanging body rises and sinks with each breath.")]
    public float breathAmplitude = 0.035f;
    [Tooltip("Seconds per breath.")]
    public float breathPeriod = 1.9f;

    public LedgeHangSettings Clone() => (LedgeHangSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the climb holds by reference.
    public void CopyFrom(LedgeHangSettings source) {
        holdable = source.holdable;
        intentThreshold = source.intentThreshold;
        regrabLockout = source.regrabLockout;
        swayAmplitude = source.swayAmplitude;
        swayPeriod = source.swayPeriod;
        breathAmplitude = source.breathAmplitude;
        breathPeriod = source.breathPeriod;
    }
}
