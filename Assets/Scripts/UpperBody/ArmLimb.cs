using System;
using UnityEngine;

// One arm (shoulder and forearm bones) posed by two-bone IK toward a per-frame reach request,
// easing back to its authored rest pose when nothing asks for it.
[Serializable]
public class ArmLimb {
    private const float RelaxedWeight = 0.001f;

    [SerializeField] private Transform shoulder;
    [SerializeField] private Transform forearm;
    [Tooltip("Forearm bone length; the bone has no end joint to measure it from.")]
    [SerializeField] private float forearmLength = 0.9f;
    [Tooltip("+1 bends the elbow below/behind the shoulder-to-hand line (natural arm), -1 above/in front of it.")]
    [SerializeField] private int elbowBendSide = 1;
    [Tooltip("Fraction of the remaining pose change covered per 60 Hz frame.")]
    [SerializeField] private float poseEase = 0.35f;

    private int authoredFacingSign;
    private float upperLength;
    private (float upper, float forearm) restAngles;
    private Vector2 easedTarget;
    private Vector2? requestedTarget;
    private float weight;

    public Vector2 ShoulderPosition => shoulder.position;
    public float Reach => upperLength + forearmLength;
    // The forearm bone and its length: the hand is a point at the end of it, and held things hang from there.
    public Transform Forearm => forearm;
    public float ForearmLength => forearmLength;
    public Vector2 HandPosition => forearm.TransformPoint(new Vector3(forearmLength, 0f, 0f));

    public void Initialize(int rigAuthoredFacingSign) {
        authoredFacingSign = rigAuthoredFacingSign;
        upperLength = forearm.localPosition.magnitude;
        restAngles = (shoulder.localEulerAngles.z, forearm.localEulerAngles.z);
        easedTarget = HandPosition;
    }

    // Valid for the current frame only; an arm without a request relaxes.
    public void ReachToward(Vector2 worldTarget) => requestedTarget = worldTarget;

    // Where something asked the hand to go this frame so far (null: nothing has), so a later layer can blend over it.
    public Vector2? RequestedTarget => requestedTarget;

    public void ApplyPose(float deltaTime) {
        float ease = FrameRateEase.FractionForFrame(poseEase, deltaTime);
        EaseTowardRequest(ease);
        requestedTarget = null;
        PoseBones();
    }

    private void EaseTowardRequest(float ease) {
        if (requestedTarget.HasValue) {
            easedTarget = weight <= RelaxedWeight ? HandPosition : Vector2.Lerp(easedTarget, requestedTarget.Value, ease);
        }
        weight = Mathf.Lerp(weight, requestedTarget.HasValue ? 1f : 0f, ease);
    }

    private void PoseBones() {
        if (weight <= RelaxedWeight) {
            SetLocalAngles(restAngles);
            return;
        }

        (float upper, float forearm) reachAngles = SolveLocalAngles(easedTarget);
        SetLocalAngles((
            Mathf.LerpAngle(restAngles.upper, reachAngles.upper, weight),
            Mathf.LerpAngle(restAngles.forearm, reachAngles.forearm, weight)));
    }

    // Solved in the shoulder's parent space, which already contains any facing mirror of the rig root.
    private (float upper, float forearm) SolveLocalAngles(Vector2 worldTarget) {
        Transform parent = shoulder.parent;
        Vector2 shoulderPoint = shoulder.localPosition;
        Vector2 target = parent.InverseTransformPoint(worldTarget);

        (Vector2 elbow, Vector2 hand) = TwoBoneIk.Solve((shoulderPoint, target, ElbowHintFor(target - shoulderPoint)), (upperLength, forearmLength));
        float upperAngle = AngleOf(elbow - shoulderPoint);
        return (upperAngle, AngleOf(hand - elbow) - upperAngle);
    }

    // The elbow always bends to the same side of the shoulder-to-target line (below it when reaching forward,
    // behind it when hanging), so it never flips sides as the target sweeps around the shoulder.
    // The parent space has the authored rig's handedness, so the authored facing picks the side.
    private Vector2 ElbowHintFor(Vector2 reach) {
        Vector2 clockwisePerpendicular = new Vector2(reach.y, -reach.x);
        return clockwisePerpendicular * (elbowBendSide * authoredFacingSign);
    }

    private void SetLocalAngles((float upper, float forearm) angles) {
        shoulder.localRotation = Quaternion.Euler(0f, 0f, angles.upper);
        forearm.localRotation = Quaternion.Euler(0f, 0f, angles.forearm);
    }

    private static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
}
