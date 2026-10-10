using System;
using UnityEngine;

// How the sidearm sits on the character: where the holster is on the front thigh, how the pistol is held in the hand,
// how hard a shot twists it and how the draw and holster run. A class with initializers, so a value added later keeps its
// default in scenes saved before it. The offsets are in the local space of the bone they hang from (the front thigh for
// the holster, the forearm for the grip).
[Serializable]
public class PistolHandlingSettings {
    [Header("Holster")]
    [Tooltip("Where the pistol's grip sits in the holster, in the front thigh bone's space (x down the thigh from the hip; " +
             "y across it, toward the front). The pouch runs on down the thigh from there.")]
    public Vector2 holsterOffset = new Vector2(0.22f, -0.1f);
    [Tooltip("Degrees the holster (and the pistol in it) is turned from the thigh bone's axis: 0 points the barrel down the " +
             "thigh with the grip up at the hip, pointing back.")]
    public float holsterAngle = 0f;

    [Header("Grip")]
    [Tooltip("Where the pistol's grip sits in the hand, in the forearm bone's space beyond its end.")]
    public Vector2 gripOffset = new Vector2(0.04f, -0.08f);
    [Tooltip("Degrees a shot twists the barrel up, per unit of the grip's recoil.")]
    public float recoilTwist = 80f;
    [Tooltip("Size of the pistol and holster pictures.")]
    public float pistolScale = 1f;
    [Tooltip("Seconds the pistol takes to move to its new place after it changes hands or holster.")]
    public float attachEaseTime = 0.1f;

    [Header("Draw and holster")]
    [Tooltip("Seconds from the hand leaving its swing to the pistol at the aim.")]
    public float drawDuration = 0.35f;
    [Tooltip("Seconds from the aim back to the hand free of the holstered pistol.")]
    public float holsterDuration = 0.4f;
    [Tooltip("Seconds for the same holster when a roll, climb or wall slide takes the arms.")]
    public float quickHolsterDuration = 0.2f;
    [Tooltip("Share of the draw at which the hand has reached the holster and grips the pistol.")]
    public float gripShare = 0.3f;
    [Tooltip("Share of the draw from which the support hand joins the grip (it lets go at the mirrored point of the holster).")]
    public float supportJoinShare = 0.65f;
    [Tooltip("Share of the holster at which the pistol goes into the holster and leaves the hand.")]
    public float insertShare = 0.7f;
    [Tooltip("How far the hand's path bows up off the straight line, as a share of the line's length, at its middle.")]
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
