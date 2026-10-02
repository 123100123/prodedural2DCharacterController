using System;
using UnityEngine;

// How one gait (walk or run) moves. The gait in use is a blend of the walk and run profiles,
// so idle, walking and running flow into each other instead of switching.
[Serializable]
public struct GaitProfile {
    [Tooltip("Ground speed in units per second.")]
    public float speed;
    [Tooltip("Seconds a foot carries weight per stride. Stance shorter than swing gives a run's flight phase.")]
    public float stanceTime;
    [Tooltip("Seconds a foot is in the air per step.")]
    public float swingTime;
    [Tooltip("Smallest foot travel relative to the hip, so slow movement does not shuffle in tiny steps.")]
    public float minExcursion;
    public float liftHeight;
    [Tooltip("How far the pelvis sinks below standing height at full speed.")]
    public float pelvisDrop;
    [Tooltip("Downward body velocity per unit of foot impact speed when a foot plants.")]
    public float plantImpulse;
    [Tooltip("Upward body velocity when a foot pushes off: the bounce of a run.")]
    public float pushOffImpulse;
    [Tooltip("Share of the way the body leans toward the supporting foot.")]
    public float weightShift;
    [Tooltip("Sustained forward chest lean in degrees at full speed.")]
    public float lean;
    [Tooltip("Arm swing in degrees at full speed.")]
    public float armSwing;
    [Tooltip("0 hangs the arms straight, 1 bends the elbows like a sprinter.")]
    [Range(0f, 1f)] public float armBend;

    public static GaitProfile Walk => new GaitProfile {
        speed = 3.5f, stanceTime = 0.6f, swingTime = 0.38f, minExcursion = 0.7f, liftHeight = 0.45f,
        pelvisDrop = 0f, plantImpulse = 0.25f, pushOffImpulse = 0f, weightShift = 0.2f,
        lean = 3f, armSwing = 22f, armBend = 0.1f,
    };

    public static GaitProfile Run => new GaitProfile {
        speed = 8.5f, stanceTime = 0.27f, swingTime = 0.36f, minExcursion = 1.2f, liftHeight = 0.8f,
        pelvisDrop = 0.25f, plantImpulse = 0.35f, pushOffImpulse = 1.5f, weightShift = 0.05f,
        lean = 12f, armSwing = 45f, armBend = 0.6f,
    };

    // How far a foot travels relative to the hip while planted: the body moves over it for one stance.
    public float FootExcursion(float groundSpeed) => Mathf.Max(minExcursion, Mathf.Abs(groundSpeed) * stanceTime);

    public static GaitProfile Blend((GaitProfile walk, GaitProfile run) profiles, float runShare) {
        GaitProfile a = profiles.walk;
        GaitProfile b = profiles.run;
        float t = Mathf.Clamp01(runShare);
        return new GaitProfile {
            speed = Mathf.Lerp(a.speed, b.speed, t),
            stanceTime = Mathf.Lerp(a.stanceTime, b.stanceTime, t),
            swingTime = Mathf.Lerp(a.swingTime, b.swingTime, t),
            minExcursion = Mathf.Lerp(a.minExcursion, b.minExcursion, t),
            liftHeight = Mathf.Lerp(a.liftHeight, b.liftHeight, t),
            pelvisDrop = Mathf.Lerp(a.pelvisDrop, b.pelvisDrop, t),
            plantImpulse = Mathf.Lerp(a.plantImpulse, b.plantImpulse, t),
            pushOffImpulse = Mathf.Lerp(a.pushOffImpulse, b.pushOffImpulse, t),
            weightShift = Mathf.Lerp(a.weightShift, b.weightShift, t),
            lean = Mathf.Lerp(a.lean, b.lean, t),
            armSwing = Mathf.Lerp(a.armSwing, b.armSwing, t),
            armBend = Mathf.Lerp(a.armBend, b.armBend, t),
        };
    }
}
