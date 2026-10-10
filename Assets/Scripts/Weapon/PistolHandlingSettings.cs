using System;
using UnityEngine;

// How the sidearm sits on the character: where the holster is on the hip, how the pistol is held in the hand and how
// hard a shot twists it. A class with initializers, so a value added later keeps its default in scenes saved before it.
// The offsets are in the local space of the bone they hang from (the pelvis for the holster, the forearm for the grip).
[Serializable]
public class PistolHandlingSettings {
    [Header("Holster")]
    [Tooltip("Where the holster hangs, in the pelvis bone's space (x along the spine, up the body; y toward the back).")]
    public Vector2 holsterOffset = new Vector2(-0.12f, -0.4f);
    [Tooltip("Degrees the holster (and the pistol in it) is turned from the pelvis bone's axis.")]
    public float holsterAngle = -184f;

    [Header("Grip")]
    [Tooltip("Where the pistol's grip sits in the hand, in the forearm bone's space beyond its end.")]
    public Vector2 gripOffset = new Vector2(0.04f, -0.08f);
    [Tooltip("Degrees a shot twists the barrel up, per unit of the grip's recoil.")]
    public float recoilTwist = 80f;
    [Tooltip("Size of the pistol and holster pictures.")]
    public float pistolScale = 1f;
    [Tooltip("Seconds the pistol takes to move to its new place after it changes hands or holster.")]
    public float attachEaseTime = 0.1f;

    [Header("Draw and holster (not used yet)")]
    public float drawDuration = 0.35f;
    public float holsterDuration = 0.4f;
    public float quickHolsterDuration = 0.2f;
    public float gripShare = 0.3f;
    public float supportJoinShare = 0.65f;
    public float insertShare = 0.7f;
    public float handArcLift = 0.25f;

    public PistolHandlingSettings Clone() => (PistolHandlingSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the view holds by reference.
    public void CopyFrom(PistolHandlingSettings source) {
        holsterOffset = source.holsterOffset;
        holsterAngle = source.holsterAngle;
        gripOffset = source.gripOffset;
        recoilTwist = source.recoilTwist;
        pistolScale = source.pistolScale;
        attachEaseTime = source.attachEaseTime;
        drawDuration = source.drawDuration;
        holsterDuration = source.holsterDuration;
        quickHolsterDuration = source.quickHolsterDuration;
        gripShare = source.gripShare;
        supportJoinShare = source.supportJoinShare;
        insertShare = source.insertShare;
        handArcLift = source.handArcLift;
    }
}
