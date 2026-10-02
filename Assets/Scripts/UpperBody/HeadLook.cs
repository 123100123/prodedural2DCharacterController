using System;
using UnityEngine;

// Tilts the head toward a per-frame look direction (chin up or down), returning to rest when nothing asks for it.
[Serializable]
public class HeadLook {
    [SerializeField] private Transform head;
    [Tooltip("Share of the look elevation the head follows.")]
    [SerializeField] private float followShare = 0.6f;
    [SerializeField] private float maxTilt = 30f;
    [SerializeField] private float poseEase = 0.25f;
    [Tooltip("Distance from the head bone pivot to the top of the head along the bone.")]
    [SerializeField] private float crownDistance = 0.63f;

    private Transform root;
    private int authoredFacingSign;
    private float restAngle;
    private float tilt;
    private float? requestedTilt;

    public void Initialize((Transform characterRoot, int authoredFacingSign) rig) {
        root = rig.characterRoot;
        authoredFacingSign = rig.authoredFacingSign;
        restAngle = head.localEulerAngles.z;
    }

    public Vector2 CrownPosition => head.TransformPoint(new Vector3(crownDistance, 0f, 0f));

    // Valid for the current frame only.
    public void LookToward(Vector2 worldDirection) => requestedTilt = TiltFor(worldDirection);

    // Bends the head forward, chin toward the chest, by the given angle (e.g. to roll over the shoulders). Valid for the current frame only.
    public void TuckChin(float degrees) => requestedTilt = -degrees * authoredFacingSign;

    public void ApplyPose(float deltaTime) {
        tilt = Mathf.Lerp(tilt, requestedTilt ?? 0f, FrameRateEase.FractionForFrame(poseEase, deltaTime));
        requestedTilt = null;
        head.localRotation = Quaternion.Euler(0f, 0f, restAngle + tilt);
    }

    // Elevation is measured in the authored rig, so mirroring the root needs no special case.
    // Chin-up turns the head toward its back: clockwise (negative) for a left-facing rig, counterclockwise for a right-facing one.
    private float TiltFor(Vector2 worldDirection) {
        Vector2 authored = root.InverseTransformVector(worldDirection);
        float elevation = Mathf.Atan2(authored.y, authored.x * authoredFacingSign) * Mathf.Rad2Deg;
        return Mathf.Clamp(elevation * followShare, -maxTilt, maxTilt) * authoredFacingSign;
    }
}
