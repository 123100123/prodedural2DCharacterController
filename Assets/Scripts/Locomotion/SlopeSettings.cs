using System;
using UnityEngine;

// Walking, running, sliding and rolling on slopes, and the feet on them. A class with initializers, so a scene saved before
// a field existed keeps its default. Every "at 45°" value scales with the slope's steepness (none on flat ground).
[Serializable]
public class SlopeSettings {
    [Tooltip("Steepest surface (degrees from flat) that counts as ground to walk on; steeper faces are walls.")]
    [Range(10f, 80f)] public float maxSlopeAngle = 50f;

    [Header("Gait")]
    [Tooltip("Share of the flat-ground speed kept along the surface going up a 45° slope (horizontal speed is also scaled by the slope's cosine).")]
    [Range(0.2f, 1.5f)] public float uphillSpeedFactor = 0.75f;
    [Tooltip("Share of the flat-ground speed along the surface going down a 45° slope: a little faster, still under control.")]
    [Range(0.2f, 1.5f)] public float downhillSpeedFactor = 1.1f;
    [Tooltip("Share of the step length taken off on a 45° slope, so the legs stay within reach of feet above and below the hip.")]
    [Range(0f, 0.8f)] public float strideShortening = 0.45f;
    [Tooltip("How much lower the pelvis rides on a 45° slope: the feet ahead and behind are higher and lower than the ground under the hip.")]
    public float bodyDrop = 0.3f;

    [Header("Feet")]
    [Tooltip("How far a planted foot turns to lie along the ground (0 stays level, 1 follows the slope).")]
    [Range(0f, 1f)] public float ankleAlignWeight = 1f;
    [Tooltip("Seconds a foot takes to turn onto the ground after it plants, and back to level once it lifts.")]
    public float ankleBlendTime = 0.06f;

    [Header("Slide")]
    [Tooltip("Speed a running slide gains per second going down a slope (and loses going up), times the sine of the slope.")]
    public float slideSlopeAcceleration = 16f;
    [Tooltip("A slide never speeds up past this (u/s) going downhill; one that starts faster keeps its speed.")]
    public float slideMaxSpeed = 15f;

    public SlopeSettings Clone() => (SlopeSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the gait and the moves hold by reference.
    public void CopyFrom(SlopeSettings source) {
        maxSlopeAngle = source.maxSlopeAngle;
        uphillSpeedFactor = source.uphillSpeedFactor;
        downhillSpeedFactor = source.downhillSpeedFactor;
        strideShortening = source.strideShortening;
        bodyDrop = source.bodyDrop;
        ankleAlignWeight = source.ankleAlignWeight;
        ankleBlendTime = source.ankleBlendTime;
        slideSlopeAcceleration = source.slideSlopeAcceleration;
        slideMaxSpeed = source.slideMaxSpeed;
    }
}
