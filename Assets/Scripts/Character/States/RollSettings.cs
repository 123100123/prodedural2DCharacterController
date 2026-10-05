using System;
using UnityEngine;

// Dive roll tuning. Offsets are x forward, y up; heights are body pivot (pelvis) heights above the ground;
// pitch is the forward rotation of the body in degrees (360 is a full roll).
[Serializable]
public class RollSettings {
    [Tooltip("Whether the roll input starts a roll. Off for now; the roll code stays in place.")]
    public bool enabled;
    public float cooldown = 0.3f;
    [Tooltip("Fraction of the remaining speed, height and pitch change covered per 60 Hz frame.")]
    public float speedEase = 0.15f;
    public float groundFollowEase = 0.35f;
    [Tooltip("How tightly the rolling body follows its ground contact as the hips come back down.")]
    public float rollContactEase = 0.7f;
    public float limbPoseEase = 0.4f;
    [Tooltip("Deepest drop below the body the roll follows before it becomes a fall.")]
    public float maxGroundDrop = 3.5f;
    [Tooltip("Gap kept between the ground and the lowest of head, shoulders and chest. During the roll phase the body stays in contact at this gap.")]
    public float groundClearance = 0.05f;

    [Header("1. Dive")]
    public float diveDuration = 0.22f;
    public float diveSpeed = 6.5f;
    public float diveLean = 55f;
    public float diveBodyHeight = 1.8f;
    [Tooltip("Arms reach ahead this many degrees below horizontal.")]
    public float diveArmAngle = 20f;
    [Range(0.5f, 1f)] public float diveArmExtension = 0.95f;
    [Tooltip("The lead foot pushes off for this share of the dive before leaving the ground.")]
    [Range(0f, 1f)] public float pushOffShare = 0.5f;

    [Header("2. Hand contact")]
    [Tooltip("Where the hands aim on the ground, ahead of the shoulders.")]
    public float handReachAhead = 0.7f;
    public float contactLean = 100f;
    public float contactBodyHeight = 1.5f;
    public float contactSpeed = 5f;
    [Tooltip("A hand this close above the ground counts as touching it.")]
    public float contactTolerance = 0.15f;
    [Tooltip("Longest wait for hand contact before rolling anyway.")]
    public float maxContactTime = 0.3f;
    public float backHandSpacing = 0.15f;

    [Header("Extended legs during dive and contact (body space at the start)")]
    public Vector2 leadFootExtended = new Vector2(-0.2f, -2.0f);
    public Vector2 trailFootExtended = new Vector2(-0.7f, -1.8f);

    [Header("3. Roll")]
    public float rollDuration = 0.42f;
    public float rollSpeed = 4.5f;
    public float chestCurlAngle = 45f;
    [Tooltip("Body pitch range (degrees) in which the chest curls and the chin tucks. Only while inverted do they swing the head up, away from the ground.")]
    public Vector2 invertedPitchWindow = new Vector2(90f, 300f);
    [Tooltip("Hips radius: the pelvis never sinks closer to the ground than this.")]
    public float hipRadius = 0.4f;
    [Tooltip("Head bend toward the chest while the hands touch down and the body rolls, so it rolls over the shoulders, not the head.")]
    public float chinTuckAngle = 60f;
    [Tooltip("Hands stay planted at the contact point for this share of the roll, then tuck.")]
    [Range(0f, 1f)] public float handPinShare = 0.3f;
    public Vector2 leadFootTucked = new Vector2(0.6f, -1.0f);
    public Vector2 trailFootTucked = new Vector2(0.3f, -1.15f);
    public Vector2 handsTucked = new Vector2(0.75f, -0.55f);

    [Header("4. Recovery")]
    public float recoverDuration = 0.3f;
    public float recoverSpeed = 1.5f;
    [Tooltip("Crouch left when the roll hands over to walking; the gait rises out of it.")]
    public float recoverCrouch = 0.35f;

    public RollSettings Clone() => (RollSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the state holds by reference.
    public void CopyFrom(RollSettings source) {
        enabled = source.enabled;
        cooldown = source.cooldown;
        speedEase = source.speedEase;
        groundFollowEase = source.groundFollowEase;
        rollContactEase = source.rollContactEase;
        limbPoseEase = source.limbPoseEase;
        maxGroundDrop = source.maxGroundDrop;
        groundClearance = source.groundClearance;
        diveDuration = source.diveDuration;
        diveSpeed = source.diveSpeed;
        diveLean = source.diveLean;
        diveBodyHeight = source.diveBodyHeight;
        diveArmAngle = source.diveArmAngle;
        diveArmExtension = source.diveArmExtension;
        pushOffShare = source.pushOffShare;
        handReachAhead = source.handReachAhead;
        contactLean = source.contactLean;
        contactBodyHeight = source.contactBodyHeight;
        contactSpeed = source.contactSpeed;
        contactTolerance = source.contactTolerance;
        maxContactTime = source.maxContactTime;
        backHandSpacing = source.backHandSpacing;
        leadFootExtended = source.leadFootExtended;
        trailFootExtended = source.trailFootExtended;
        rollDuration = source.rollDuration;
        rollSpeed = source.rollSpeed;
        chestCurlAngle = source.chestCurlAngle;
        invertedPitchWindow = source.invertedPitchWindow;
        hipRadius = source.hipRadius;
        chinTuckAngle = source.chinTuckAngle;
        handPinShare = source.handPinShare;
        leadFootTucked = source.leadFootTucked;
        trailFootTucked = source.trailFootTucked;
        handsTucked = source.handsTucked;
        recoverDuration = source.recoverDuration;
        recoverSpeed = source.recoverSpeed;
        recoverCrouch = source.recoverCrouch;
    }
}
