using System;
using UnityEngine;

[Serializable]
public class AirborneSettings {
    [Tooltip("Top horizontal speed steered in the air.")]
    public float airSpeed = 6f;
    public float airAcceleration = 30f;
    [Tooltip("Grace period after walking off an edge in which jump still works.")]
    public float coyoteTime = 0.1f;
    [Tooltip("A jump pressed this long before landing fires on touchdown.")]
    public float jumpBufferTime = 0.15f;

    [Header("Leg pose (x forward, y down from the body pivot)")]
    public Vector2 leadFootTucked = new Vector2(0.55f, -1.5f);
    public Vector2 trailFootTucked = new Vector2(-0.15f, -1.75f);
    public Vector2 leadFootReaching = new Vector2(0.3f, -2.05f);
    public Vector2 trailFootReaching = new Vector2(-0.2f, -2.15f);
    [Tooltip("Vertical speed at which legs are fully tucked (rising) or fully reaching (falling).")]
    public float poseBlendSpeed = 4f;
    [Tooltip("Fraction of the remaining pose distance covered per 60 Hz frame.")]
    public float legPoseEase = 0.3f;

    [Header("Landing")]
    public float landingDipPerSpeed = 0.04f;
    public float maxLandingDip = 0.6f;
}
