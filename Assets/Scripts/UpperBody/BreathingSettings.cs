using System;
using UnityEngine;

// How the chest breathes while the character stands still.
[Serializable]
public struct BreathingSettings {
    [Tooltip("Seconds per breath.")]
    public float period;
    [Tooltip("Chest extension in degrees with full lungs.")]
    public float pitch;
    [Tooltip("How far the chest rises along the spine with full lungs, lifting the shoulders, arms and head.")]
    public float rise;

    public static BreathingSettings Default => new BreathingSettings { period = 4f, pitch = 3.5f, rise = 0.07f };
}
