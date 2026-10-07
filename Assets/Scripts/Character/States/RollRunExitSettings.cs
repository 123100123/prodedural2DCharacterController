using System;
using UnityEngine;

// Leaving a roll into a run: with the run intent along the roll, the recovery keeps the roll's momentum (only its drag
// slows it, no settling brake), and hands over to the run part way through instead of at its end.
[Serializable]
public class RollRunExitSettings {
    [Tooltip("Off: the recovery always brakes as without the run intent and stands up at its end, run intent or not.")]
    public bool enabled = true;
    [Tooltip("With the run intent along the roll, the recovery hands over to the run once this share of it has passed.")]
    [Range(0f, 1f)] public float runExitShare = 0.35f;
    [Tooltip("The hand-over also waits until the pelvis is at most this far below its standing height, so the walk's body " +
             "spring does not jerk it up (the roll's own end leaves it about the recovery crouch below).")]
    public float maxStandGap = 0.45f;

    // Whether the recovery keeps the roll's momentum this frame.
    public bool KeepsTravelSpeed(bool runsAlong) => enabled && runsAlong;

    // Whether the recovery hands over to the run now. standGap: how far the pelvis is below its standing height.
    public bool HandsOver((bool runsAlong, float recoverProgress, float standGap) recovery) {
        return KeepsTravelSpeed(recovery.runsAlong) && recovery.recoverProgress >= runExitShare && recovery.standGap <= maxStandGap;
    }

    public RollRunExitSettings Clone() => (RollRunExitSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the state holds by reference.
    public void CopyFrom(RollRunExitSettings source) {
        enabled = source.enabled;
        runExitShare = source.runExitShare;
        maxStandGap = source.maxStandGap;
    }
}
