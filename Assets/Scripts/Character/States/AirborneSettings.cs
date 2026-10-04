using System;
using UnityEngine;

[Serializable]
public class AirborneSettings {
    [Tooltip("Top horizontal speed steered in the air; speed carried in from the ground beyond it is kept.")]
    public float airSpeed = 6f;
    public float airAcceleration = 30f;
    [Tooltip("Speed lost per second with no input in the air: a gentle drag, so a jump keeps its momentum.")]
    public float neutralDrag = 2f;
    [Tooltip("Grace period after walking off an edge in which jump still works.")]
    public float coyoteTime = 0.1f;
    [Tooltip("A jump pressed this long before landing fires on touchdown.")]
    public float jumpBufferTime = 0.15f;

    [Header("Leg pose")]
    [Tooltip("Fraction of the remaining pose distance the feet cover per 60 Hz frame (the poses themselves are AirPoseSettings).")]
    public float legPoseEase = 0.3f;

    public AirborneSettings Clone() => (AirborneSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the states hold by reference.
    public void CopyFrom(AirborneSettings source) {
        airSpeed = source.airSpeed;
        airAcceleration = source.airAcceleration;
        neutralDrag = source.neutralDrag;
        coyoteTime = source.coyoteTime;
        jumpBufferTime = source.jumpBufferTime;
        legPoseEase = source.legPoseEase;
    }
}
