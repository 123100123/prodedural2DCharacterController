using System;
using UnityEngine;

// Tilts the head per frame: toward a look direction, tucked toward the chest, or held level; returns to rest when
// nothing asks. Holding level or looking, the head is stabilized in the world: it cancels a share of the torso's tilt
// at once, without a spring's lag, so it stays steady while the torso leans and flexes with every step. Only the
// requested tilt (where to look, how far to tuck) eases through a spring.
[Serializable]
public class HeadLook {
    [SerializeField] private Transform head;
    [Tooltip("Share of the look elevation the head follows.")]
    [SerializeField] private float followShare = 0.6f;
    [SerializeField] private float maxTilt = 30f;
    [Tooltip("How the head follows its requested tilt (looking, tucking): it lags slightly and settles after jolts.")]
    [SerializeField] private SpringSettings poseSpring = new SpringSettings(3.5f, 0.5f, 0f);
    [Tooltip("How strongly the head keeps level in the world while holding level or looking: 0 rides the torso, " +
             "1 cancels all of its tilt.")]
    [Range(0f, 1f)] [SerializeField] private float stabilization = 0.8f;
    [Tooltip("Seconds the stabilization takes to fade in or out when the head starts or stops holding level.")]
    [SerializeField] private float stabilizationFade = 0.2f;
    [Tooltip("Largest correction applied to keep the head level when the torso leans.")]
    [SerializeField] private float maxLevelCorrection = 40f;
    [Tooltip("Distance from the head bone pivot to the top of the head along the bone.")]
    [SerializeField] private float crownDistance = 0.63f;

    private Transform root;
    private int authoredFacingSign;
    private float restAngle;
    private SecondOrderDynamics tilt;
    private float? requestedTilt;
    private bool isLevelRequested;

    public void Initialize((Transform characterRoot, int authoredFacingSign) rig) {
        root = rig.characterRoot;
        authoredFacingSign = rig.authoredFacingSign;
        restAngle = head.localEulerAngles.z;
        tilt = new SecondOrderDynamics(0f);
    }

    public Vector2 CrownPosition => head.TransformPoint(new Vector3(crownDistance, 0f, 0f));
    public float Stabilization { get => stabilization; set => stabilization = Mathf.Clamp01(value); }
    public SpringSettings PoseSpring { get => poseSpring; set => poseSpring = value; }
    // How much of the torso's tilt the head cancels right now (fades toward Stabilization while holding level).
    public float LevelWeight { get; private set; }

    // Valid for the current frame only.
    public void LookToward(Vector2 worldDirection) {
        requestedTilt = TiltFor(worldDirection);
        isLevelRequested = true;
    }

    // Keeps the head upright, looking straight ahead, whatever the torso does. Valid for the current frame only.
    public void HoldLevel() {
        requestedTilt = 0f;
        isLevelRequested = true;
    }

    // Bends the head forward, chin toward the chest, by the given angle (e.g. to roll over the shoulders). Valid for the current frame only.
    public void TuckChin(float degrees) => requestedTilt = -degrees * authoredFacingSign;

    public void ApplyPose(float deltaTime) {
        float requested = tilt.Update((requestedTilt ?? 0f, deltaTime), poseSpring);
        LevelWeight = Mathf.MoveTowards(LevelWeight, isLevelRequested ? stabilization : 0f, deltaTime / Mathf.Max(0.01f, stabilizationFade));
        float levelling = LevelWeight * Mathf.Clamp(TiltToUpright(), -maxLevelCorrection, maxLevelCorrection);
        requestedTilt = null;
        isLevelRequested = false;
        head.localRotation = Quaternion.Euler(0f, 0f, restAngle + requested + levelling);
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
