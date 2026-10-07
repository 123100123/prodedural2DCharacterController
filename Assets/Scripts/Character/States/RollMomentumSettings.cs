using System;
using UnityEngine;

// How the roll carries the momentum it starts with: the roll keeps a share of the incoming speed and adds a forward
// throw (harder from a run than from standing), then lets the speed decay through its phases with per-phase drag and
// brakes instead of easing toward fixed speeds. Drags are a share of the speed lost per second (1/s); brakes are a
// constant deceleration (u/s²).
[Serializable]
public class RollMomentumSettings {
    [Header("Entry and throw")]
    [Tooltip("Share of the incoming speed (along the roll) the roll keeps.")]
    [Range(0f, 1.5f)] public float inheritShare = 0.9f;
    [Tooltip("Forward throw added to a roll started standing still (u/s).")]
    public float standingThrow = 2.5f;
    [Tooltip("Forward throw added to a roll started at the full run speed (u/s); in between it blends with the speed.")]
    public float runningThrow = 4f;
    [Tooltip("The kept speed plus the throw never exceeds this (u/s).")]
    public float maxEntrySpeed = 12f;
    [Tooltip("The throw is spread over this long from the start of the dive, so the speed rises instead of snapping (s).")]
    public float throwTime = 0.08f;

    [Header("Decay")]
    [Tooltip("Drag while diving (share of the speed lost per second).")]
    public float diveDrag = 0.6f;
    [Tooltip("Extra deceleration while the hands take the dive (u/s²).")]
    public float contactBrake = 2f;
    [Tooltip("Drag while rolling over (share of the speed lost per second).")]
    public float rollDrag = 0.9f;
    [Tooltip("Drag while getting up (share of the speed lost per second).")]
    public float recoverDrag = 1.2f;
    [Tooltip("Getting up with no move input brakes this hard toward a stop (u/s²); with walk input it pulls the speed " +
             "toward the walk speed at this rate. With the run intent along the roll only the drag applies.")]
    public float settleBrake = 8f;
    [Tooltip("Until the recovery the roll never travels slower than this (u/s), so a standing roll still moves.")]
    public float minRollSpeed = 2.5f;

    [Header("Dive arc")]
    [Tooltip("Pelvis rise early in a dive started standing still (u), on top of the dive's height.")]
    public float standingLift = 0.05f;
    [Tooltip("Pelvis rise early in a dive started at the full run speed (u).")]
    public float runningLift = 0.25f;

    public RollMomentumSettings Clone() => (RollMomentumSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the state holds by reference.
    public void CopyFrom(RollMomentumSettings source) {
        inheritShare = source.inheritShare;
        standingThrow = source.standingThrow;
        runningThrow = source.runningThrow;
        maxEntrySpeed = source.maxEntrySpeed;
        throwTime = source.throwTime;
        diveDrag = source.diveDrag;
        contactBrake = source.contactBrake;
        rollDrag = source.rollDrag;
        recoverDrag = source.recoverDrag;
        settleBrake = source.settleBrake;
        minRollSpeed = source.minRollSpeed;
        standingLift = source.standingLift;
        runningLift = source.runningLift;
    }
}
