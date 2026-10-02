using System;
using UnityEngine;

// Tilts the head per frame: toward a look direction, tucked toward the chest, or held level; returns to rest when nothing asks.
[Serializable]
public class HeadLook {
    [SerializeField] private Transform head;
    [Tooltip("Share of the look elevation the head follows.")]
    [SerializeField] private float followShare = 0.6f;
    [SerializeField] private float maxTilt = 30f;
    [Tooltip("How the head follows its requested tilt: it lags slightly and settles after jolts.")]
    [SerializeField] private SpringSettings poseSpring = new SpringSettings(3.5f, 0.5f, 0f);
    [Tooltip("Largest correction applied to keep the head level when the torso leans.")]
    [SerializeField] private float maxLevelCorrection = 40f;
    [Tooltip("Distance from the head bone pivot to the top of the head along the bone.")]
    [SerializeField] private float crownDistance = 0.63f;

    private Transform root;
    private int authoredFacingSign;
    private float restAngle;
    private SecondOrderDynamics tilt;
    private float? requestedTilt;

    public void Initialize((Transform characterRoot, int authoredFacingSign) rig) {
        root = rig.characterRoot;
        authoredFacingSign = rig.authoredFacingSign;
        restAngle = head.localEulerAngles.z;
        tilt = new SecondOrderDynamics(0f);
    }

    public Vector2 CrownPosition => head.TransformPoint(new Vector3(crownDistance, 0f, 0f));

    // Valid for the current frame only.
    public void LookToward(Vector2 worldDirection) => requestedTilt = TiltFor(worldDirection);

    // Keeps the head upright and level whatever the torso lean, looking straight ahead. Valid for the current frame only.
    public void HoldLevel() => requestedTilt = Mathf.Clamp(TiltToUpright(), -maxLevelCorrection, maxLevelCorrection);

    // Bends the head forward, chin toward the chest, by the given angle (e.g. to roll over the shoulders). Valid for the current frame only.
    public void TuckChin(float degrees) => requestedTilt = -degrees * authoredFacingSign;

    public void ApplyPose(float deltaTime) {
        float currentTilt = tilt.Update((requestedTilt ?? 0f, deltaTime), poseSpring);
        requestedTilt = null;
        head.localRotation = Quaternion.Euler(0f, 0f, restAngle + currentTilt);
    }

    // How far the head must turn from its rest angle for its bone (neck to crown) to point straight up,
    // measured in the authored rig; the head's parent space has the same handedness.
    private float TiltToUpright() {
        float restRadians = restAngle * Mathf.Deg2Rad;
        Vector2 restDirection = head.parent.TransformVector(new Vector2(Mathf.Cos(restRadians), Mathf.Sin(restRadians)));
        Vector2 authored = root.InverseTransformVector(restDirection);
        return Mathf.DeltaAngle(Mathf.Atan2(authored.y, authored.x) * Mathf.Rad2Deg, 90f);
    }

    // Elevation is measured in the authored rig, so mirroring the root needs no special case.
    // Chin-up turns the head toward its back: clockwise (negative) for a left-facing rig, counterclockwise for a right-facing one.
    private float TiltFor(Vector2 worldDirection) {
        Vector2 authored = root.InverseTransformVector(worldDirection);
        float elevation = Mathf.Atan2(authored.y, authored.x * authoredFacingSign) * Mathf.Rad2Deg;
        return Mathf.Clamp(elevation * followShare, -maxTilt, maxTilt) * authoredFacingSign;
    }
}
