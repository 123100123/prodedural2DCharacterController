using UnityEngine;

// Ballistic relations used to tune jumps by height instead of raw speed.
public static class JumpMath {
    public static float LaunchSpeedForHeight(float gravity, float height) => Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height));

    // The apex height of a jump whose button was released after being held for holdTime: a tap rises only the hop
    // height, a hold of the full time rises the whole height, and holds in between rise in between.
    public static float HeldJumpHeight(float holdTime, (float hopHeight, float fullHeight, float fullHoldTime) jump) {
        float share = Mathf.Clamp01(holdTime / Mathf.Max(0.001f, jump.fullHoldTime));
        float hop = Mathf.Min(jump.hopHeight, jump.fullHeight);
        return Mathf.Lerp(hop, jump.fullHeight, Mathf.SmoothStep(0f, 1f, share));
    }

    // Releasing the button cuts the rise: the upward speed is capped to what reaches the held height from the ground.
    public static float CutRise(float verticalSpeed, float gravity, float heldHeight) {
        return Mathf.Min(verticalSpeed, LaunchSpeedForHeight(gravity, heldHeight));
    }
}
