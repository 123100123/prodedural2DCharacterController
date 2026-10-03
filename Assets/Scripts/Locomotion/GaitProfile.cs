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
    [Tooltip("Share of the stance's travel the foot lands ahead of the hip; the rest it leaves behind. 0.5 centres the " +
             "stance (walking); a runner lands close under the body and pushes off far behind, about a third ahead.")]
    [Range(0.2f, 0.6f)] public float contactLeadShare;
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
        speed = 3.5f, acceleration = 9f, deceleration = 13f,
        dutyFactor = 0.62f, contactLeadShare = 0.5f, stepLengthPerRootSpeed = 0.84f, minStepLength = 0.45f, maxStepLength = 2.2f,
        minSwingDuration = 0.25f, maxSwingDuration = 0.55f, liftHeight = 0.32f,
        pelvisDrop = 0.14f, bobAmplitude = 0.09f, lowAfterContact = 0.06f, surge = 0.05f, plantImpulse = 0.08f, pushOffImpulse = 0f,
        lean = 3.5f, pitchOscillation = 0.6f, armSwing = 25f, armBend = 0.15f,
    };

    // A driving run: the foot lands close under the body and pushes off far behind (a third of the stance ahead), so the
    // push-off leg straightens out behind the hip in one line with the forward-leaning torso; the swinging knee drives
    // up in front (StepCurves.Run) and the bent arms pump. About three steps a second, each foot down a little over a
    // third of the cycle, so both feet leave the ground. The body is lowest as the hip passes over the foot and highest
    // in flight; plants and push-offs only accent the bob.
    public static GaitProfile Run => new GaitProfile {
        speed = 9f, acceleration = 14f, deceleration = 18f,
        dutyFactor = 0.36f, contactLeadShare = 0.35f, stepLengthPerRootSpeed = 0.92f, minStepLength = 0.55f, maxStepLength = 3.2f,
        minSwingDuration = 0.18f, maxSwingDuration = 0.45f, liftHeight = 0.65f,
        pelvisDrop = 0.16f, bobAmplitude = 0.12f, lowAfterContact = 0.13f, surge = 0.08f, plantImpulse = 0.08f, pushOffImpulse = 0.3f,
        lean = 17f, pitchOscillation = 1f, armSwing = 45f, armBend = 0.5f,
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
            contactLeadShare = Mathf.Lerp(a.contactLeadShare, b.contactLeadShare, t),
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
