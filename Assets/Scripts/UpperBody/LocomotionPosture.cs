using System;
using UnityEngine;

// Leans the torso forward from the hips as the character moves, by turning the posture root (the pelvis bone, so the
// whole torso tilts as one line rather than bending at the waist): a modest base lean that grows with the pace and a
// lean into acceleration, through a slow, critically damped spring so the posture carries the body's inertia instead
// of following individual steps. Most of the forward body line comes from the pelvis travelling over its support foot
// (StrideController); this lean only adds to it. When the gait is not driving it follows the flight pose in the air and
// relaxes upright rolling or climbing. The chest's per-step flex rides on top of it (TorsoMotion).
[Serializable]
public class LocomotionPosture {
    [SerializeField] private PostureSettings settings = PostureSettings.Default;

    private CharacterRig rig;
    private Quaternion restRotation;
    private SecondOrderDynamics lean;
    private float previousGroundSpeed;

    public PostureSettings Settings { get => settings; set => settings = value; }
    // Degrees of forward lean the posture is heading for, the part of it from acceleration, and the lean it has now.
    public float TargetLean { get; private set; }
    public float AccelerationLean { get; private set; }
    public float CurrentLean => lean?.Value ?? 0f;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restRotation = rig.PostureRoot.localRotation;
        lean = new SecondOrderDynamics(0f);
        previousGroundSpeed = rig.Stride.GroundSpeed;
    }

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }
        AccelerationLean = MeasureAccelerationLean(deltaTime);
        TargetLean = rig.Stride.IsDrivingGait ? PostureLean.Total((BaseLean(), AccelerationLean), settings) : AirLean();
        float forwardLean = lean.Update((TargetLean, deltaTime), settings.spring);
        // Turning about +z leans an authored right-facing rig back, so forward is the opposite of the authored facing. A
        // push's lean is added outside the slow spring: the push pose blends it in and out on its own, quicker spring.
        rig.PostureRoot.localRotation = Quaternion.Euler(0f, 0f, -(forwardLean + rig.PushLean) * rig.Facing.AuthoredSign) * restRotation;
    }

    // In the air the posture takes the lean of the flight pose; with no flight (rolling, climbing) it relaxes upright.
    private float AirLean() => rig.AirPose.IsActive ? rig.AirPose.Pose.lean : 0f;

    private float BaseLean() {
        StrideController stride = rig.Stride;
        float idleLean = settings.idleLean + stride.CrouchSettings.idleLean * stride.CrouchShare;
        float slopeLean = PostureLean.SlopeLean((stride.SlopeGradient, rig.Facing.Sign), stride.Slope.slopeLean);
        return PostureLean.BaseLean((idleLean, stride.CurrentGait.lean, stride.Pace.Moving)) + slopeLean;
    }

    // Uses the gait's ground speed rather than the body position, so the step's own surge and bob do not read as starts.
    private float MeasureAccelerationLean(float deltaTime) {
        float groundSpeed = rig.Stride.GroundSpeed;
        float forwardAcceleration = (groundSpeed - previousGroundSpeed) / deltaTime * rig.Facing.Sign;
        previousGroundSpeed = groundSpeed;
        return rig.Stride.IsDrivingGait ? PostureLean.AccelerationLean(forwardAcceleration, settings) : 0f;
    }
}
