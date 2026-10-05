using System;
using UnityEngine;

// Wall slide and wall jump: when a falling character pressing into a wall catches it, how fast it slides down, what
// counts as a wall, how a jump kicks off it, and the pose against it. A class with initializers, so a scene saved before
// a field existed keeps its default.
[Serializable]
public class WallSettings {
    [Tooltip("Off: walls are only collision, as before.")]
    public bool enabled = true;

    [Header("Slide")]
    [Tooltip("Fastest the character slides down a wall, in u/s.")]
    public float maxSlideSpeed = 4f;
    [Tooltip("How quickly the fall speed is brought to the slide speed, in u/s²: braking a fast fall or speeding up from a stop.")]
    public float slideAcceleration = 40f;
    [Tooltip("A rising character catches the wall once its upward speed is below this (u/s), just before the top of the jump.")]
    public float catchRiseSpeed = 1f;
    [Tooltip("Seconds the player may let go of (or push away from) the wall before the character drops off it.")]
    public float releaseGrace = 0.12f;

    [Header("Detection")]
    [Tooltip("How far past the side of the body a wall still counts as touched, in units.")]
    public float detectionDistance = 0.15f;
    [Tooltip("Height of the chest probe above the body pivot.")]
    public float chestProbeHeight = 0.9f;
    [Tooltip("The wall must reach this far below the chest probe (to the knees) for the body to slide on it.")]
    public float minWallHeight = 1.9f;
    [Tooltip("No slide starts this close above the ground (the gap below the standing feet); a slide goes on down to the ground.")]
    public float minGroundClearance = 0.5f;

    [Header("Wall jump")]
    [Tooltip("Launch velocity of a wall jump: x away from the wall, y up (u/s). The jump button held through the rise gives the full height.")]
    public Vector2 jumpVelocity = new Vector2(7f, 11f);
    [Tooltip("Seconds after a wall jump in which steering back toward that wall is ignored.")]
    public float steeringLockout = 0.15f;
    [Tooltip("Seconds after dropping off a wall in which a jump still kicks off it.")]
    public float coyoteTime = 0.12f;

    [Header("Pose")]
    [Tooltip("Degrees the torso leans out from the wall (forward, as the back is to the wall).")]
    public float leanOut = 8f;
    [Tooltip("Height of the braced hand on the wall above the shoulder.")]
    public float handAboveShoulder = 0.9f;
    [Tooltip("The free arm's hand, relative to its shoulder: x forward, y up.")]
    public Vector2 freeHand = new Vector2(0.6f, -1.1f);
    [Tooltip("Distance from hip to foot as a share of the leg's length, for the front and the back foot on the wall (lower is more bent).")]
    public Vector2 footReach = new Vector2(0.88f, 0.96f);
    [Tooltip("Gap kept between the feet and the wall face.")]
    public float footWallGap = 0.08f;
    [Tooltip("Fraction of the remaining pose distance the feet cover per 60 Hz frame.")]
    public float limbPoseEase = 0.35f;

    public WallSettings Clone() => (WallSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the wall slide holds by reference.
    public void CopyFrom(WallSettings source) {
        enabled = source.enabled;
        maxSlideSpeed = source.maxSlideSpeed;
        slideAcceleration = source.slideAcceleration;
        catchRiseSpeed = source.catchRiseSpeed;
        releaseGrace = source.releaseGrace;
        detectionDistance = source.detectionDistance;
        chestProbeHeight = source.chestProbeHeight;
        minWallHeight = source.minWallHeight;
        minGroundClearance = source.minGroundClearance;
        jumpVelocity = source.jumpVelocity;
        steeringLockout = source.steeringLockout;
        coyoteTime = source.coyoteTime;
        leanOut = source.leanOut;
        handAboveShoulder = source.handAboveShoulder;
        freeHand = source.freeHand;
        footReach = source.footReach;
        footWallGap = source.footWallGap;
        limbPoseEase = source.limbPoseEase;
    }
}
