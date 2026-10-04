using System;
using UnityEngine;

// How the body reacts to a long fall: after a short while the arms begin to flail in large, fast, uneven arcs that grow
// the longer the fall lasts and the faster it gets, and the legs and torso loosen a little. A class so a setting added
// later keeps its default in scenes saved before it existed.
[Serializable]
public class FallSecondarySettings {
    [Header("Build-up")]
    [Tooltip("Seconds of falling before the secondary motion starts to appear.")]
    public float onsetTime = 0.5f;
    [Tooltip("Seconds of falling at which it is at full strength.")]
    public float fullTime = 1.8f;
    [Tooltip("Downward speeds (u/s) over which the fall speed adds to the strength: x where it starts, y where it is complete.")]
    public Vector2 speedBand = new Vector2(6f, 16f);
    [Tooltip("The share of full strength a long fall reaches even while it is still slow.")]
    [Range(0f, 1f)] public float slowFallStrength = 0.5f;

    [Header("Arms")]
    [Tooltip("How far (degrees) the arms swing either way about their pose at full strength.")]
    public float armAmplitude = 75f;
    [Tooltip("Flail speed (cycles per second) when it begins and at full strength.")]
    public Vector2 flailRate = new Vector2(0.9f, 2.4f);
    [Tooltip("How much the flail speed wanders up and down, as a share of itself, so it never settles into a rhythm.")]
    [Range(0f, 0.5f)] public float rateWobble = 0.2f;
    [Tooltip("How fast the flail speed wanders (cycles per second).")]
    public float wobbleRate = 0.31f;
    [Tooltip("How much of the arm swing is a faster second wave on top of the first.")]
    [Range(0f, 1f)] public float secondWave = 0.3f;
    [Tooltip("The back arm flails this many times as fast as the front arm, so the arms never fall in step.")]
    public float backArmRate = 1.13f;
    [Tooltip("Extra elbow bend that comes and goes with the flail.")]
    [Range(0f, 1f)] public float elbowFlail = 0.5f;
    [Tooltip("Degrees the arms rise as the fall reaches full speed (the air lifting them).")]
    public float armLiftWithSpeed = 25f;
    [Tooltip("Degrees the arms trail behind for each unit per second squared of forward acceleration.")]
    public float armLagPerAcceleration = 4f;
    [Tooltip("The most the arms trail behind, in degrees.")]
    public float maxArmLag = 30f;

    [Header("Legs and torso")]
    [Tooltip("How far (x forward, y up) the feet drift about their pose at full strength.")]
    public Vector2 legLoosen = new Vector2(0.12f, 0.1f);
    [Tooltip("Degrees the torso pitches forward at full strength.")]
    public float forwardPitch = 6f;

    [Header("Landing and aiming")]
    [Tooltip("The strength is cut by this share as the landing nears, so the arms come in for the touchdown.")]
    [Range(0f, 1f)] public float landingDamping = 0.6f;
    [Tooltip("Seconds the motion takes to give way to aiming, and to return after it.")]
    public float aimBlendTime = 0.15f;

    public FallSecondarySettings Clone() => (FallSecondarySettings)MemberwiseClone();

    // A copy made field by field into this instance, which the airborne state holds by reference.
    public void CopyFrom(FallSecondarySettings source) {
        onsetTime = source.onsetTime;
        fullTime = source.fullTime;
        speedBand = source.speedBand;
        slowFallStrength = source.slowFallStrength;
        armAmplitude = source.armAmplitude;
        flailRate = source.flailRate;
        rateWobble = source.rateWobble;
        wobbleRate = source.wobbleRate;
        secondWave = source.secondWave;
        backArmRate = source.backArmRate;
        elbowFlail = source.elbowFlail;
        armLiftWithSpeed = source.armLiftWithSpeed;
        armLagPerAcceleration = source.armLagPerAcceleration;
        maxArmLag = source.maxArmLag;
        legLoosen = source.legLoosen;
        forwardPitch = source.forwardPitch;
        landingDamping = source.landingDamping;
        aimBlendTime = source.aimBlendTime;
    }
}
