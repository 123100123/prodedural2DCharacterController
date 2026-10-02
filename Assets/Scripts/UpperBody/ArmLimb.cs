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
    [Tooltip("Elbow bend direction in the authored rig: x toward the back, y up.")]
    [SerializeField] private Vector2 authoredElbowHint = new Vector2(1f, -1f);
    [Tooltip("Fraction of the remaining pose change covered per 60 Hz frame.")]
    [SerializeField] private float poseEase = 0.35f;

    private Transform root;
    private float upperLength;
    private (float upper, float forearm) restAngles;
    private Vector2 easedTarget;
    private Vector2? requestedTarget;
    private float weight;

    public Vector2 ShoulderPosition => shoulder.position;
    public float Reach => upperLength + forearmLength;
    public Vector2 HandPosition => forearm.TransformPoint(new Vector3(forearmLength, 0f, 0f));

    public void Initialize(Transform characterRoot) {
        root = characterRoot;
        upperLength = forearm.localPosition.magnitude;
        restAngles = (shoulder.localEulerAngles.z, forearm.localEulerAngles.z);
        easedTarget = HandPosition;
    }

    // Valid for the current frame only; an arm without a request relaxes.
    public void ReachToward(Vector2 worldTarget) => requestedTarget = worldTarget;

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

    // Solved in the shoulder's parent space, which already contains any facing mirror of the root.
    private (float upper, float forearm) SolveLocalAngles(Vector2 worldTarget) {
        Transform parent = shoulder.parent;
        Vector2 shoulderPoint = shoulder.localPosition;
        Vector2 target = parent.InverseTransformPoint(worldTarget);
        Vector2 elbowHint = parent.InverseTransformVector(root.TransformVector(authoredElbowHint));

        (Vector2 elbow, Vector2 hand) = TwoBoneIk.Solve((shoulderPoint, target, elbowHint), (upperLength, forearmLength));
        float upperAngle = AngleOf(elbow - shoulderPoint);
        return (upperAngle, AngleOf(hand - elbow) - upperAngle);
    }

    private void SetLocalAngles((float upper, float forearm) angles) {
        shoulder.localRotation = Quaternion.Euler(0f, 0f, angles.upper);
        forearm.localRotation = Quaternion.Euler(0f, 0f, angles.forearm);
    }

    private static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
}
