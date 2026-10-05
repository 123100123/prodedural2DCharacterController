using System;
using UnityEngine;

[Serializable]
public class ClimbSettings {
    [Header("Detection")]
    [Tooltip("Highest ledge top above the stance that a standing jump-climb can reach.")]
    public float maxGrabRise = 4.5f;
    [Tooltip("How far ahead of the body ledges are searched.")]
    public float searchDistance = 1.6f;
    public float searchStep = 0.1f;
    [Tooltip("In the air, a ledge is grabbed between this far below the shoulders and this share of arm reach above them.")]
    public float airGrabBelowShoulder = 2.0f;
    [Range(0f, 1f)] public float airGrabAboveShoulderShare = 0.8f;
    [Tooltip("Space needed on top of the ledge to stand: depth along the climb and height.")]
    public Vector2 standingRoom = new Vector2(0.8f, 4.3f);
    [Tooltip("The top must continue at least this far past the edge (room for the standing stance).")]
    public float minTopDepth = 0.8f;
    public LayerMask ledgeMask = Physics2D.DefaultRaycastLayers;

    [Header("Body keyframes (x toward the ledge from the edge, y from the ledge top)")]
    public float hangInset = 0.45f;
    [Tooltip("Body pivot depth below the ledge top while hanging at arm's length.")]
    public float hangDrop = 2.5f;
    public Vector2 pulledUp = new Vector2(-0.25f, 0.5f);
    public Vector2 mounted = new Vector2(0.3f, 1.6f);
    [Tooltip("Body distance past the edge when standing on top; keep it within minTopDepth minus the stance half width.")]
    public float standingInset = 0.45f;

    [Header("Limbs (relative to the corner, x toward the ledge)")]
    public Vector2 frontGrip = new Vector2(0.05f, 0.02f);
    public Vector2 backGrip = new Vector2(0.25f, 0.02f);
    public Vector2 hangingLeadFoot = new Vector2(0.15f, -2.0f);
    public Vector2 hangingTrailFoot = new Vector2(-0.1f, -2.1f);
    [Tooltip("Foot height below the body while scrambling up the wall face.")]
    public float scrambleFootDrop = 1.7f;
    public float mountFootInset = 0.3f;
    public float limbPoseEase = 0.4f;

    [Header("Timing")]
    [Tooltip("Body speed while reaching the hang position.")]
    public float reachSpeed = 7f;
    public float minReachDuration = 0.15f;
    public float hangDuration = 0.1f;
    public float pullDuration = 0.35f;
    public float mountDuration = 0.25f;
    public float standDuration = 0.25f;

    public ClimbSettings Clone() => (ClimbSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the state holds by reference.
    public void CopyFrom(ClimbSettings source) {
        maxGrabRise = source.maxGrabRise;
        searchDistance = source.searchDistance;
        searchStep = source.searchStep;
        airGrabBelowShoulder = source.airGrabBelowShoulder;
        airGrabAboveShoulderShare = source.airGrabAboveShoulderShare;
        standingRoom = source.standingRoom;
        minTopDepth = source.minTopDepth;
        // The ledge mask is scene wiring, not a tuned value: a loaded preset leaves it alone.
        hangInset = source.hangInset;
        hangDrop = source.hangDrop;
        pulledUp = source.pulledUp;
        mounted = source.mounted;
        standingInset = source.standingInset;
        frontGrip = source.frontGrip;
        backGrip = source.backGrip;
        hangingLeadFoot = source.hangingLeadFoot;
        hangingTrailFoot = source.hangingTrailFoot;
        scrambleFootDrop = source.scrambleFootDrop;
        mountFootInset = source.mountFootInset;
        limbPoseEase = source.limbPoseEase;
        reachSpeed = source.reachSpeed;
        minReachDuration = source.minReachDuration;
        hangDuration = source.hangDuration;
        pullDuration = source.pullDuration;
        mountDuration = source.mountDuration;
        standDuration = source.standDuration;
    }
}
