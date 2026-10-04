using System;
using UnityEngine;

// How the character crouches: how low the pelvis sinks, how fast it gets there and what that does to the stance, the
// torso and the arms. The crouch walk's own gait is a GaitProfile on the StrideController.
[Serializable]
public struct CrouchSettings {
    [Tooltip("How far the pelvis (and the top of the collision hull) sinks when fully crouched.")]
    public float depth;
    [Tooltip("How the crouch share rises and falls; the feet stay planted while the pelvis follows it.")]
    public SpringSettings spring;
    [Tooltip("Crouched, the standing feet are this many times further apart.")]
    public float stanceWidthScale;
    [Tooltip("Forward lean (degrees) of the posture when crouching still; walking, the gait's lean takes over.")]
    public float idleLean;
    [Tooltip("Extra forward fold (degrees) of the chest when crouched.")]
    public float chestFold;
    [Tooltip("Elbow bend added to the arms when crouched, so they are held in close.")]
    [Range(0f, 1f)] public float idleArmBend;

    public static CrouchSettings Default => new CrouchSettings {
        depth = 0.75f, spring = new SpringSettings(3.5f, 0.9f, 0f), stanceWidthScale = 1.15f,
        idleLean = 10f, chestFold = 8f, idleArmBend = 0.35f,
    };
}
