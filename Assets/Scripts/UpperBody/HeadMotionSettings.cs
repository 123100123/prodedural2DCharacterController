using System;
using UnityEngine;

// How the head follows its requested tilt and how strongly it keeps level in the world.
[Serializable]
public struct HeadMotionSettings {
    [Tooltip("How the head follows its requested tilt (looking, tucking): it lags slightly and settles after jolts.")]
    public SpringSettings poseSpring;
    [Tooltip("How strongly the head keeps level in the world while holding level or looking: 0 rides the torso, " +
             "1 cancels all of its tilt.")]
    [Range(0f, 1f)] public float stabilization;
    [Tooltip("Seconds the stabilization takes to fade in or out when the head starts or stops holding level.")]
    public float stabilizationFade;

    public static HeadMotionSettings Default => new HeadMotionSettings {
        poseSpring = new SpringSettings(4f, 0.9f, 0f), stabilization = 0.6f, stabilizationFade = 0.2f,
    };
}
