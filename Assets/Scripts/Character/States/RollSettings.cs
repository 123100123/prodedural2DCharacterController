using System;
using UnityEngine;

[Serializable]
public class RollSettings {
    public float duration = 0.7f;
    [Tooltip("Ground distance covered in the facing direction.")]
    public float distance = 3.2f;
    public float cooldown = 0.3f;

    [Tooltip("Body pivot height above the ground over normalized roll time: crouch, hop, curled roll, stand.")]
    public AnimationCurve bodyHeight = new AnimationCurve(
        new Keyframe(0f, 2.4f), new Keyframe(0.1f, 1.95f), new Keyframe(0.32f, 2.5f),
        new Keyframe(0.55f, 1.5f), new Keyframe(0.8f, 1.5f), new Keyframe(1f, 2.4f));

    [Header("Phases (normalized time)")]
    public Vector2 travelWindow = new Vector2(0.1f, 0.9f);
    public Vector2 spinWindow = new Vector2(0.12f, 0.85f);
    [Tooltip("Curl ramps in over x..y and back out over z..w.")]
    public Vector4 curlWindow = new Vector4(0.08f, 0.22f, 0.8f, 0.95f);

    [Header("Tucked pose (x forward, y up from the body pivot, at the start of the roll)")]
    public float chestCurlAngle = 70f;
    public Vector2 leadFootTucked = new Vector2(0.6f, -1.0f);
    public Vector2 trailFootTucked = new Vector2(0.3f, -1.15f);
    public Vector2 handsTucked = new Vector2(0.75f, -0.55f);
    [Tooltip("Fraction of the remaining limb pose distance covered per 60 Hz frame.")]
    public float limbPoseEase = 0.5f;
    [Tooltip("Fraction of the remaining height difference covered per 60 Hz frame.")]
    public float groundFollowEase = 0.5f;
    [Tooltip("Deepest drop below the body the roll follows before it becomes a fall.")]
    public float maxGroundDrop = 3.5f;

    public float TravelSpeed => distance / ((travelWindow.y - travelWindow.x) * duration);

    public float CurlAmount(float t) => Mathf.Min(Mathf.InverseLerp(curlWindow.x, curlWindow.y, t), Mathf.InverseLerp(curlWindow.w, curlWindow.z, t));

    public float SpinAmount(float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(spinWindow.x, spinWindow.y, t));

    public bool IsTravelling(float t) => t >= travelWindow.x && t <= travelWindow.y;
}
