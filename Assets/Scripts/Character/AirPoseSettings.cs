using System;
using UnityEngine;

// The airborne poses and how they are blended: pushing off and rising, the hang near the top, and reaching down for the
// landing, each standing and (for the rise and the descent) running, mixed by how fast the character moves forward.
// The torso, arms and legs each follow the blend through their own springs. A class so a setting added later keeps its
// default in scenes saved before it existed.
[Serializable]
public class AirPoseSettings {
    [Header("Blend")]
    [Tooltip("Vertical speed at which the pose is fully rising or fully falling.")]
    public float apexBand = 5f;
    [Tooltip("Shares of the run speed between which the standing pose changes into the running one: x where it starts, y where it is complete.")]
    public Vector2 runningBand = new Vector2(0.25f, 0.85f);

    [Header("Push-off")]
    [Tooltip("The back leg stays extended behind the rising body this long after take-off, so the jump visibly drives off the ground.")]
    public float pushOffDuration = 0.18f;
    [Tooltip("How much further back and down (x back, y down) the trailing foot reaches during the push-off.")]
    public Vector2 pushOffReach = new Vector2(-0.3f, -0.45f);

    [Header("Rising")]
    public AirPose ascentStanding = new AirPose {
        leadFoot = new Vector2(0.7f, -1.35f), trailFoot = new Vector2(-0.55f, -2.3f),
        armFront = 115f, armBack = 115f, elbowBend = 0.2f, lean = -2f, chestPitch = -6f,
    };
    public AirPose ascentRunning = new AirPose {
        leadFoot = new Vector2(1f, -1.2f), trailFoot = new Vector2(-0.9f, -2.1f),
        armFront = 110f, armBack = 40f, elbowBend = 0.4f, lean = 8f, chestPitch = -3f,
    };

    [Header("Hang")]
    public AirPose apex = new AirPose {
        leadFoot = new Vector2(0.45f, -1.45f), trailFoot = new Vector2(0.05f, -1.5f),
        armFront = 70f, armBack = 70f, elbowBend = 0.35f, lean = 0f, chestPitch = 4f,
    };

    [Header("Falling")]
    public AirPose descentStanding = new AirPose {
        leadFoot = new Vector2(0.3f, -2.05f), trailFoot = new Vector2(-0.2f, -2.15f),
        armFront = 45f, armBack = 45f, elbowBend = 0.3f, lean = 0f, chestPitch = 2f,
    };
    public AirPose descentRunning = new AirPose {
        leadFoot = new Vector2(0.6f, -2f), trailFoot = new Vector2(-0.5f, -2.1f),
        armFront = 55f, armBack = 25f, elbowBend = 0.35f, lean = 10f, chestPitch = 6f,
    };

    public AirPoseSettings Clone() => (AirPoseSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the airborne state holds by reference.
    public void CopyFrom(AirPoseSettings source) {
        apexBand = source.apexBand;
        runningBand = source.runningBand;
        pushOffDuration = source.pushOffDuration;
        pushOffReach = source.pushOffReach;
        ascentStanding = source.ascentStanding;
        ascentRunning = source.ascentRunning;
        apex = source.apex;
        descentStanding = source.descentStanding;
        descentRunning = source.descentRunning;
    }
}
