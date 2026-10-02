using UnityEngine;

// Ballistic relations used to tune jumps by height instead of raw speed.
public static class JumpMath {
    public static float LaunchSpeedForHeight(float gravity, float height) => Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height));
}
