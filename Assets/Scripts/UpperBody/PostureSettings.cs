using System;
using UnityEngine;

// How the locomotion posture leans: at rest, into acceleration, and how quickly it follows.
[Serializable]
public struct PostureSettings {
    [Tooltip("Forward lean in degrees when standing still.")]
    public float idleLean;
    [Tooltip("Lean in degrees per unit of forward acceleration of the gait (units per second squared).")]
    public float leanPerAcceleration;
    [Tooltip("Most the lean into acceleration (or back, braking) adds, in degrees.")]
    public float maxAccelerationLean;
    [Tooltip("Cap on the whole lean in degrees, so a sprint start does not fold the body over.")]
    public float maxTotalLean;
    [Tooltip("How the posture follows its lean: slow and critically damped, so it carries the body's inertia and never " +
             "follows individual steps.")]
    public SpringSettings spring;

    public static PostureSettings Default => new PostureSettings {
        idleLean = 0f, leanPerAcceleration = 0.5f, maxAccelerationLean = 8f, maxTotalLean = 22f,
        spring = new SpringSettings(1.6f, 1f, 0f),
    };
}
