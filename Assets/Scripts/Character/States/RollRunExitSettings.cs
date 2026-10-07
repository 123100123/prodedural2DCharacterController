using System;
using UnityEngine;

// Leaving a roll into a run: with the run intent along the roll, the recovery keeps the roll's travel speed instead of
// easing down to the recovery speed, and hands over to the run part way through instead of at its end.
[Serializable]
public class RollRunExitSettings {
    [Tooltip("Off: the roll always eases down to the recovery speed and stands up at the end of the recovery, run intent or not.")]
    public bool enabled = true;
    [Tooltip("With the run intent along the roll, the recovery hands over to the run once this share of it has passed.")]
    [Range(0f, 1f)] public float runExitShare = 0.35f;

    // Whether the recovery keeps the roll's travel speed this frame.
    public bool KeepsTravelSpeed(bool runsAlong) => enabled && runsAlong;

    // Whether the recovery hands over to the run now.
    public bool HandsOver((bool runsAlong, float recoverProgress) recovery) {
        return KeepsTravelSpeed(recovery.runsAlong) && recovery.recoverProgress >= runExitShare;
    }
}
