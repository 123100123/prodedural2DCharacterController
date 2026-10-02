using System;
using UnityEngine;

// How the arms swing with the legs; the size of the swing and the elbow bend come from the gait.
[Serializable]
public struct ArmSwingSettings {
    public SpringSettings spring;
    [Tooltip("Cycle share the arm swing lags the legs: the hand drags behind the shoulder.")]
    [Range(0f, 0.25f)] public float swingLag;
    [Tooltip("Extra elbow bend while the arm is swinging forward.")]
    [Range(0f, 1f)] public float forwardElbowFlex;
    [Tooltip("How much a fully bent arm shortens toward the shoulder.")]
    [Range(0f, 0.8f)] public float bentArmShortening;
    [Tooltip("How far forward (degrees) a fully bent arm is carried.")]
    public float bentArmForwardAngle;

    public static ArmSwingSettings Default => new ArmSwingSettings {
        spring = new SpringSettings(3f, 0.5f, 0f), swingLag = 0.06f, forwardElbowFlex = 0.35f,
        bentArmShortening = 0.45f, bentArmForwardAngle = 35f,
    };
}
