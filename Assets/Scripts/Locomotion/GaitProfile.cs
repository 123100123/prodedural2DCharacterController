using System;
using UnityEngine;

// How one gait (walk or run) moves. The gait in use is a blend of the walk and run profiles,
// so idle, walking and running flow into each other instead of switching.
[Serializable]
public struct GaitProfile {
    [Tooltip("Ground speed in units per second.")]
    public float speed;
    [Tooltip("Speed gained per second when starting to move or speeding up.")]
    public float acceleration;
    [Tooltip("Speed lost per second when stopping, slowing or turning.")]
    public float deceleration;

    [Header("Timing")]
    [Tooltip("Share of the cycle each foot is on the ground: about 0.6 walking (always a foot down), under 0.5 running (a flight phase).")]
    [Range(0.2f, 0.8f)] public float dutyFactor;
    [Tooltip("Step length = this x square root of speed (the walk ratio): short steps when slow, longer ones when fast.")]
    public float stepLengthPerRootSpeed;
    public float minStepLength;
    public float maxStepLength;
    public float minSwingDuration;
    public float maxSwingDuration;
    public float liftHeight;

    [Header("Body")]
    [Tooltip("How far the pelvis sinks below standing height at full speed: knees bend a little walking, more running.")]
    public float pelvisDrop;
    [Tooltip("Vertical bob at full speed, twice per stride.")]
    public float bobAmplitude;
    [Tooltip("Cycle share after a foot lands when the body is lowest: just after contact walking, mid-stance running.")]
    public float lowAfterContact;
    [Tooltip("Forward-backward body surge at full speed: fastest through the low point, slowest over the top.")]
    public float surge;
    [Tooltip("Downward body velocity per unit of foot impact speed when a foot plants.")]
    public float plantImpulse;
    [Tooltip("Upward body velocity when a foot pushes off: the bounce of a run.")]
    public float pushOffImpulse;

    [Header("Upper body")]
    [Tooltip("Sustained forward lean of the posture in degrees at full speed: the body line of the gait.")]
    public float lean;
    [Tooltip("Secondary chest pitch swing in degrees at full speed: flexing as weight is taken, extending over the " +
             "supporting leg. Small next to the lean.")]
    public float pitchOscillation;
    [Tooltip("Arm swing in degrees at full speed.")]
    public float armSwing;
    [Tooltip("0 hangs the arms straight, 1 bends the elbows like a sprinter.")]
    [Range(0f, 1f)] public float armBend;

    public static GaitProfile Walk => new GaitProfile {
        speed = 3.5f, acceleration = 10f, deceleration = 14f,
        dutyFactor = 0.62f, stepLengthPerRootSpeed = 0.9f, minStepLength = 0.45f, maxStepLength = 2.2f,
        minSwingDuration = 0.25f, maxSwingDuration = 0.55f, liftHeight = 0.4f,
        pelvisDrop = 0.17f, bobAmplitude = 0.12f, lowAfterContact = 0.06f, surge = 0.05f, plantImpulse = 0.1f, pushOffImpulse = 0f,
        lean = 5f, pitchOscillation = 1f, armSwing = 25f, armBend = 0.15f,
    };

    // Quick steps with firm plants and a push-off bounce: about three steps a second, each foot down for a third of the
    // cycle, so the feet stay close under the body and both leave the ground between steps. The bounce comes from the
    // plants and push-offs rather than a timed bob: running steps come as fast as the body's vertical spring responds,
    // so a timed bob would lag a quarter step and peak at touchdown instead of in the air.
    public static GaitProfile Run => new GaitProfile {
        speed = 8.5f, acceleration = 18f, deceleration = 30f,
        dutyFactor = 0.33f, stepLengthPerRootSpeed = 0.92f, minStepLength = 0.8f, maxStepLength = 3.6f,
        minSwingDuration = 0.25f, maxSwingDuration = 0.55f, liftHeight = 0.55f,
        pelvisDrop = 0.38f, bobAmplitude = 0f, lowAfterContact = 0.165f, surge = 0.02f, plantImpulse = 0.5f, pushOffImpulse = 2.2f,
        lean = 14f, pitchOscillation = 1.5f, armSwing = 45f, armBend = 0.6f,
    };

    public static GaitProfile Blend((GaitProfile walk, GaitProfile run) profiles, float runShare) {
        GaitProfile a = profiles.walk;
        GaitProfile b = profiles.run;
        float t = Mathf.Clamp01(runShare);
        return new GaitProfile {
            speed = Mathf.Lerp(a.speed, b.speed, t),
            acceleration = Mathf.Lerp(a.acceleration, b.acceleration, t),
            deceleration = Mathf.Lerp(a.deceleration, b.deceleration, t),
            dutyFactor = Mathf.Lerp(a.dutyFactor, b.dutyFactor, t),
            stepLengthPerRootSpeed = Mathf.Lerp(a.stepLengthPerRootSpeed, b.stepLengthPerRootSpeed, t),
            minStepLength = Mathf.Lerp(a.minStepLength, b.minStepLength, t),
            maxStepLength = Mathf.Lerp(a.maxStepLength, b.maxStepLength, t),
            minSwingDuration = Mathf.Lerp(a.minSwingDuration, b.minSwingDuration, t),
            maxSwingDuration = Mathf.Lerp(a.maxSwingDuration, b.maxSwingDuration, t),
            liftHeight = Mathf.Lerp(a.liftHeight, b.liftHeight, t),
            pelvisDrop = Mathf.Lerp(a.pelvisDrop, b.pelvisDrop, t),
            bobAmplitude = Mathf.Lerp(a.bobAmplitude, b.bobAmplitude, t),
            lowAfterContact = Mathf.Lerp(a.lowAfterContact, b.lowAfterContact, t),
            surge = Mathf.Lerp(a.surge, b.surge, t),
            plantImpulse = Mathf.Lerp(a.plantImpulse, b.plantImpulse, t),
            pushOffImpulse = Mathf.Lerp(a.pushOffImpulse, b.pushOffImpulse, t),
            lean = Mathf.Lerp(a.lean, b.lean, t),
            pitchOscillation = Mathf.Lerp(a.pitchOscillation, b.pitchOscillation, t),
            armSwing = Mathf.Lerp(a.armSwing, b.armSwing, t),
            armBend = Mathf.Lerp(a.armBend, b.armBend, t),
        };
    }
}
