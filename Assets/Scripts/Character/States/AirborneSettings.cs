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
    [Tooltip("Share of the landing speed carried into the body spring: the squash on touchdown.")]
    [Range(0f, 1f)] public float landingSpeedCarry = 0.6f;
    [Tooltip("Cap on the carried landing speed, so very long falls do not fold the body into the ground.")]
    public float maxCarriedLandingSpeed = 9f;
    [Tooltip("How far below standing height the body may be placed at touchdown.")]
    public float touchdownSink = 0.3f;
}
