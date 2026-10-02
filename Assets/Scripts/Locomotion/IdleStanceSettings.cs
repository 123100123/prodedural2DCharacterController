using System;
using UnityEngine;

// How the character stands still: where its feet go and how it steps them there.
[Serializable]
public struct IdleStanceSettings {
    [Tooltip("Half the distance between the feet when standing.")]
    public float stanceHalfWidth;
    [Tooltip("How far ahead of the pelvis the standing feet are centred, so they stand under the torso rather than behind it.")]
    public float stanceLead;
    [Tooltip("An idle foot further than this from its stance spot takes a settling step.")]
    public float footTolerance;
    [Tooltip("Seconds a settling step takes.")]
    public float settleStepDuration;
    [Tooltip("How high a settling step lifts the foot.")]
    public float settleLiftHeight;

    public static IdleStanceSettings Default => new IdleStanceSettings {
        stanceHalfWidth = 0.22f, stanceLead = 0.1f, footTolerance = 0.18f, settleStepDuration = 0.22f, settleLiftHeight = 0.25f,
    };
}
