using System;
using UnityEngine;

// One airborne pose: where the feet reach, how the arms are held, and how the torso carries itself. The feet are in
// body space (x forward of the pelvis pivot, y up, so reaching down is negative); the arms are degrees forward of
// plumb; the lean and the chest pitch are degrees forward.
[Serializable]
public struct AirPose {
    public Vector2 leadFoot;
    public Vector2 trailFoot;
    [Tooltip("Degrees the front arm is raised forward of hanging.")]
    public float armFront;
    [Tooltip("Degrees the back arm is raised forward of hanging.")]
    public float armBack;
    [Range(0f, 1f)] public float elbowBend;
    [Tooltip("Forward lean of the whole posture, in degrees.")]
    public float lean;
    [Tooltip("Forward flex of the chest on top of the lean, in degrees (negative extends it back).")]
    public float chestPitch;

    public static AirPose Lerp(AirPose a, AirPose b, float t) {
        return new AirPose {
            leadFoot = Vector2.Lerp(a.leadFoot, b.leadFoot, t),
            trailFoot = Vector2.Lerp(a.trailFoot, b.trailFoot, t),
            armFront = Mathf.Lerp(a.armFront, b.armFront, t),
            armBack = Mathf.Lerp(a.armBack, b.armBack, t),
            elbowBend = Mathf.Lerp(a.elbowBend, b.elbowBend, t),
            lean = Mathf.Lerp(a.lean, b.lean, t),
            chestPitch = Mathf.Lerp(a.chestPitch, b.chestPitch, t),
        };
    }

    // The weighted sum of three poses whose weights add up to 1.
    public static AirPose Blend3((AirPose pose, float weight) first, (AirPose pose, float weight) second, (AirPose pose, float weight) third) {
        return new AirPose {
            leadFoot = first.pose.leadFoot * first.weight + second.pose.leadFoot * second.weight + third.pose.leadFoot * third.weight,
            trailFoot = first.pose.trailFoot * first.weight + second.pose.trailFoot * second.weight + third.pose.trailFoot * third.weight,
            armFront = first.pose.armFront * first.weight + second.pose.armFront * second.weight + third.pose.armFront * third.weight,
            armBack = first.pose.armBack * first.weight + second.pose.armBack * second.weight + third.pose.armBack * third.weight,
            elbowBend = first.pose.elbowBend * first.weight + second.pose.elbowBend * second.weight + third.pose.elbowBend * third.weight,
            lean = first.pose.lean * first.weight + second.pose.lean * second.weight + third.pose.lean * third.weight,
            chestPitch = first.pose.chestPitch * first.weight + second.pose.chestPitch * second.weight + third.pose.chestPitch * third.weight,
        };
    }
}

// What the upper-body systems read while the character is in the air: the pose for this frame. Zero (not active) on the
// ground, where they follow the gait instead.
public readonly struct AirPoseFrame {
    public static readonly AirPoseFrame None = default;

    // elbows: how far each arm's elbow is bent (the pose's elbow bend, plus what the fall's secondary motion adds).
    public AirPoseFrame(AirPose pose, (float front, float back) elbows) {
        Pose = pose;
        ElbowFront = Mathf.Clamp01(elbows.front);
        ElbowBack = Mathf.Clamp01(elbows.back);
        IsActive = true;
    }

    public AirPose Pose { get; }
    public float ElbowFront { get; }
    public float ElbowBack { get; }
    public bool IsActive { get; }
}
