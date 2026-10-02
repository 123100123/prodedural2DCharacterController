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

    public static GaitTransitionSettings Default => new GaitTransitionSettings {
        startSpeed = 0.35f, stopSpeed = 0.15f,
        runBlendStart = 0.1f, runBlendEnd = 0.8f, runBlendRate = 4f,
    };
}
