using System;
using UnityEngine;

// How a jump is loaded, launched and cut short. The launch carries the ground speed the character has (see
// JumpMomentum); how long it loads shrinks as it moves faster, so a run jumps off its stride.
[Serializable]
public class JumpSettings {
    [Header("Windup")]
    [Tooltip("Crouch time before take-off when standing; shorter the faster the character is moving.")]
    public float windupDuration = 0.16f;
    [Tooltip("How far the pelvis sinks while loading a standing jump.")]
    public float crouchDepth = 0.6f;
    [Tooltip("The shortest windup, however fast the character runs.")]
    public float minWindup = 0.02f;
    [Tooltip("Shares of the run speed between which the windup fades from full to its minimum: x where it starts to shrink, y where it is gone.")]
    public Vector2 windupSpeedBand = new Vector2(0.15f, 0.85f);

    [Header("Launch")]
    [Tooltip("Apex height of the body above take-off, holding the button.")]
    public float jumpHeight = 2.4f;
    [Tooltip("A jump from a full crouch rises this share of the full height.")]
    [Range(0.3f, 1f)] public float crouchJumpScale = 0.85f;
    [Tooltip("Apex height of a tap: the button released at once.")]
    public float minHopHeight = 0.8f;
    [Tooltip("Holding the button this long gives the full height; shorter holds give heights between the hop and the full jump.")]
    public float fullHoldTime = 0.2f;

    [Header("Momentum")]
    [Tooltip("Share of the ground speed the jump keeps.")]
    [Range(0f, 1.5f)] public float carry = 1f;
    [Tooltip("Extra forward speed at a full run, fading to none as the character slows: a running jump goes further than the speed alone.")]
    public float forwardImpulse = 1.2f;

    public float LaunchSpeed(float gravity) => JumpMath.LaunchSpeedForHeight(gravity, jumpHeight);

    public JumpSettings Clone() => (JumpSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the states hold by reference.
    public void CopyFrom(JumpSettings source) {
        windupDuration = source.windupDuration;
        crouchDepth = source.crouchDepth;
        minWindup = source.minWindup;
        windupSpeedBand = source.windupSpeedBand;
        jumpHeight = source.jumpHeight;
        crouchJumpScale = source.crouchJumpScale;
        minHopHeight = source.minHopHeight;
        fullHoldTime = source.fullHoldTime;
        carry = source.carry;
        forwardImpulse = source.forwardImpulse;
    }
}
