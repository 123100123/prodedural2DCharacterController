using System;
using UnityEngine;

// When the grounded gait starts, stops and changes between walking and running.
[Serializable]
public struct GaitTransitionSettings {
    [Header("Idle")]
    [Tooltip("Ground speed above which a standing character starts stepping.")]
    public float startSpeed;
    [Tooltip("Ground speed below which a moving character stops stepping and settles. Lower than the start speed, so " +
             "a speed hovering around one threshold cannot start and stop the gait every frame.")]
    public float stopSpeed;

    [Header("Walk to run")]
    [Tooltip("Share of the speed range between full walk and full run at which the gait starts to blend into a run.")]
    [Range(0f, 1f)] public float runBlendStart;
    [Tooltip("Share of the speed range between full walk and full run at which the gait is a full run.")]
    [Range(0f, 1f)] public float runBlendEnd;
    [Tooltip("Fastest the walk/run blend may change, in shares per second; smooths sudden speed changes such as wall stops.")]
    public float runBlendRate;

    [Tooltip("Most the swing's share of the cycle changes per gait cycle. Walking feet swing for about two fifths of the " +
             "cycle and running feet for two thirds; with the lifts fixed half a cycle apart, that change moves the " +
             "contacts, and a steady ramp moves them by the same small amount every step instead of stretching one " +
             "interval into a limp.")]
    [Range(0.02f, 1f)] public float swingShareChangePerCycle;
    [Tooltip("Speeding up, swings are timed for at least this share of the intended speed, as people do when setting " +
             "off: the first steps already take the target gait's rhythm rather than a shuffle. Below 1, a run's first " +
             "swings land early and the stances after them run longer than the legs reach.")]
    [Range(0f, 1f)] public float settingOffShare;

    [Header("Overlap")]
    [Tooltip("How far through its swing a walking foot must be before the other foot may lift: late, so a foot is always down.")]
    [Range(0f, 1f)] public float walkLiftOverlap;
    [Tooltip("How far through its swing a running foot must be before the other foot may lift: mid-swing, so both feet leave the ground.")]
    [Range(0f, 1f)] public float runLiftOverlap;

    public static GaitTransitionSettings Default => new GaitTransitionSettings {
        startSpeed = 0.35f, stopSpeed = 0.15f,
        runBlendStart = 0.1f, runBlendEnd = 0.8f, runBlendRate = 4f, swingShareChangePerCycle = 0.15f, settingOffShare = 1f,
        walkLiftOverlap = 0.8f, runLiftOverlap = 0.5f,
    };
}
