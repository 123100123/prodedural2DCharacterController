using System;
using UnityEngine;

[Serializable]
public class JumpSettings {
    [Tooltip("Apex height of the body above take-off.")]
    public float jumpHeight = 2.4f;
    [Tooltip("Crouch time before take-off.")]
    public float windupDuration = 0.12f;
    public float crouchDepth = 0.45f;

    public float LaunchSpeed(float gravity) => JumpMath.LaunchSpeedForHeight(gravity, jumpHeight);
}
