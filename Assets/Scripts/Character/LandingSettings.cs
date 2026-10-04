using System;
using UnityEngine;

// How the character comes down: it reaches for the ground in the last moments of a fall, and the touchdown squashes the
// body and costs some speed in proportion to how hard it lands, handing the rest of the momentum back to the gait. A
// class so a setting added later keeps its default in scenes saved before it existed.
[Serializable]
public class LandingSettings {
    [Header("Reaching for the ground")]
    [Tooltip("Seconds before touchdown at which the legs start reaching for the ground below.")]
    public float prepTime = 0.25f;
    [Tooltip("The ground is looked for at least this far below the body, however slow the fall.")]
    public float lookAheadMin = 0.5f;

    [Header("Impact")]
    [Tooltip("Impact speed (u/s) of a light landing: below it the landing is as gentle as it gets.")]
    public float lightImpactSpeed = 6f;
    [Tooltip("Impact speed (u/s) of a hard landing: above it the landing is as hard as it gets.")]
    public float hardImpactSpeed = 20f;

    [Header("Squash")]
    [Tooltip("How far below standing height the body is placed at a light touchdown.")]
    public float sinkLight = 0.15f;
    [Tooltip("How far below standing height the body is placed at a hard touchdown.")]
    public float sinkHard = 0.7f;
    [Tooltip("Share of the landing speed carried into the body spring: the squash on touchdown.")]
    [Range(0f, 1f)] public float speedCarry = 0.6f;
    [Tooltip("Cap on the carried landing speed, so very long falls do not fold the body into the ground.")]
    public float maxCarriedSpeed = 9f;
    [Tooltip("How much lower (units) the pelvis is held at a hard landing before it recovers, on top of the squash.")]
    public float compressionHard = 0.5f;
    [Tooltip("How the held compression recovers.")]
    public SpringSettings compressionSpring = new SpringSettings(3f, 0.8f, 0f);
    [Tooltip("Forward fold of the torso at a hard landing, as a kick (degrees per second) the chest spring turns into a fold.")]
    public float torsoFoldKick = 120f;

    [Header("Momentum")]
    [Tooltip("Share of the horizontal speed a hard landing costs.")]
    [Range(0f, 1f)] public float speedLossHard = 0.2f;
    [Tooltip("Landing faster than this share of the walk speed puts the feet where a moving gait has them, and the gait carries on; slower, the character stands.")]
    [Range(0f, 1f)] public float movingLandingShare = 0.6f;

    public LandingSettings Clone() => (LandingSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the states hold by reference.
    public void CopyFrom(LandingSettings source) {
        prepTime = source.prepTime;
        lookAheadMin = source.lookAheadMin;
        lightImpactSpeed = source.lightImpactSpeed;
        hardImpactSpeed = source.hardImpactSpeed;
        sinkLight = source.sinkLight;
        sinkHard = source.sinkHard;
        speedCarry = source.speedCarry;
        maxCarriedSpeed = source.maxCarriedSpeed;
        compressionHard = source.compressionHard;
        compressionSpring = source.compressionSpring;
        torsoFoldKick = source.torsoFoldKick;
        speedLossHard = source.speedLossHard;
        movingLandingShare = source.movingLandingShare;
    }
}
