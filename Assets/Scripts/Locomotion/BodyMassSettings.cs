using System;
using UnityEngine;

// How the body rides its feet as a mass on springs, and how high the planted legs let it ride.
[Serializable]
public struct BodyMassSettings {
    [Tooltip("Carries the body along its anchor: lags a little at speed and surges with the steps.")]
    public SpringSettings horizontalSpring;
    [Tooltip("Carries the body's height: firm and well damped, above the step rate (about 3.5 per second running), so the " +
             "gait's bob makes the motion instead of the spring resonating. The bob is aimed ahead by this spring's lag.")]
    public SpringSettings verticalSpring;
    [Tooltip("Share of the leg length a supporting leg may extend to; the pelvis never rises higher than its planted legs " +
             "reach. Below 1, so the knees always keep some bend in reserve.")]
    [Range(0.8f, 1f)] public float legExtension;
    [Tooltip("Most the leg-reach limit may lower the pelvis below its gait height; a foot further away is about to step anyway.")]
    public float maxReachDrop;
    [Tooltip("Largest downward push a foot plant may give the body, in units per second.")]
    public float maxPlantImpulse;
    [Tooltip("Share of the gap to a stance centred over the planted support foot the pelvis takes at mid-stance: 0 rides " +
             "the anchor alone, 1 centres the pelvis over its support.")]
    [Range(0f, 1f)] public float supportCoupling;
    [Tooltip("Most the support foot may shift the pelvis away from its anchor.")]
    public float maxSupportOffset;

    public static BodyMassSettings Default => new BodyMassSettings {
        horizontalSpring = new SpringSettings(4f, 0.9f, 0.3f),
        verticalSpring = new SpringSettings(6f, 0.9f, 0.2f),
        legExtension = 0.95f, maxReachDrop = 0.23f, maxPlantImpulse = 0.7f, supportCoupling = 0.6f, maxSupportOffset = 0.3f,
    };
}
