using System;
using UnityEngine;

// When a planted foot trailing the hip quickens the cadence, and when it takes an emergency balance step.
[Serializable]
public struct BalanceSettings {
    [Tooltip("A planted foot trailing this much further than a normal stance ends starts quickening the cadence.")]
    public float catchUpMargin;
    [Tooltip("Cadence gained per unit of trailing beyond the catch-up margin.")]
    public float catchUpGain;
    [Tooltip("Fastest the cadence may run while catching up, as a multiple of the normal cadence.")]
    [Range(1f, 3f)] public float maxCatchUp;
    [Tooltip("A planted foot trailing this much further than a normal stance ends takes an emergency balance step at once, " +
             "re-phasing the gait. Larger than the catch-up margin: routine speed changes never reach it.")]
    public float emergencyMargin;
    [Tooltip("Seconds after an emergency balance step before another may be taken.")]
    public float emergencyCooldown;

    public static BalanceSettings Default => new BalanceSettings {
        catchUpMargin = 0.1f, catchUpGain = 2f, maxCatchUp = 1.6f,
        emergencyMargin = 0.7f, emergencyCooldown = 0.4f,
    };
}
