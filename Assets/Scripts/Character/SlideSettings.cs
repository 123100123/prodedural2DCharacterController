using System;
using UnityEngine;

// The running slide: when a crouch pressed at speed starts one, how fast it shoots forward and slows, and its low pose.
// A class with initializers, so a scene saved before a field existed keeps its default. Offsets are x forward of the body
// pivot (pelvis) in the direction of the slide, y up from the ground.
[Serializable]
public class SlideSettings {
    [Tooltip("Off: a crouch pressed while running only crouches, as before.")]
    public bool enabled = true;

    [Header("Start")]
    [Tooltip("Without the run input held, a crouch press slides only at this share of the run speed or faster.")]
    [Range(0f, 1.5f)] public float entrySpeedShare = 0.6f;
    [Tooltip("The slide starts at least this fast (u/s); a faster character keeps its own speed.")]
    public float boostSpeed = 10f;
    [Tooltip("Seconds after a slide before another can start.")]
    public float cooldown = 0.4f;

    [Header("Motion")]
    [Tooltip("How quickly the slide slows, in u/s².")]
    public float friction = 8f;
    [Tooltip("The slide ends when it is this slow (u/s), handing over to the crouch walk (or the stand).")]
    public float minSpeed = 2f;
    [Tooltip("Fraction of the remaining height change the body covers per 60 Hz frame as it drops into the slide and follows the ground.")]
    [Range(0.01f, 1f)] public float groundFollowEase = 0.25f;

    [Header("Pose")]
    [Tooltip("Height of the body pivot (pelvis) above the ground while sliding.")]
    public float pelvisHeight = 0.85f;
    [Tooltip("Degrees the torso leans back (negative leans forward).")]
    public float leanBack = 28f;
    [Tooltip("The lead foot, stretched ahead along the ground (the leg reaches at most its length): x forward of the pelvis, y above the ground.")]
    public Vector2 leadFoot = new Vector2(2.2f, 0.05f);
    [Tooltip("The trail foot, folded under the body with the knee up: x forward of the pelvis (negative is behind), y above the ground. " +
             "Behind the hip the rig's backward knee would bend into the ground.")]
    public Vector2 trailFoot = new Vector2(0.25f, 0f);
    [Tooltip("The back hand, trailing near the ground: x forward of its shoulder (negative is behind), y above the ground.")]
    public Vector2 trailingHand = new Vector2(-0.8f, 0.15f);
    [Tooltip("The front hand, held forward for balance: x forward of its shoulder, y up from it.")]
    public Vector2 balanceHand = new Vector2(0.9f, -0.35f);
    [Tooltip("Fraction of the remaining pose distance the feet cover per 60 Hz frame.")]
    [Range(0.01f, 1f)] public float limbPoseEase = 0.35f;

    public SlideSettings Clone() => (SlideSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the slide holds by reference.
    public void CopyFrom(SlideSettings source) {
        enabled = source.enabled;
        entrySpeedShare = source.entrySpeedShare;
        boostSpeed = source.boostSpeed;
        cooldown = source.cooldown;
        friction = source.friction;
        minSpeed = source.minSpeed;
        groundFollowEase = source.groundFollowEase;
        pelvisHeight = source.pelvisHeight;
        leanBack = source.leanBack;
        leadFoot = source.leadFoot;
        trailFoot = source.trailFoot;
        trailingHand = source.trailingHand;
        balanceHand = source.balanceHand;
        limbPoseEase = source.limbPoseEase;
    }
}
