using System;
using UnityEngine;

// Leans the torso forward from the hips as the character moves, by turning the posture root (the pelvis bone, so the
// whole torso tilts as one line rather than bending at the waist): a modest base lean that grows with the pace and a
// lean into acceleration, through a slow, critically damped spring so the posture carries the body's inertia instead
// of following individual steps. Most of the forward body line comes from the pelvis travelling over its support foot
// (StrideController); this lean only adds to it. When the gait is not driving (airborne, rolling, climbing) it relaxes
// upright. The chest's per-step flex rides on top of it (TorsoMotion).
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
        TargetLean = rig.Stride.IsDrivingGait ? PostureLean.Total((BaseLean(), AccelerationLean), settings) : 0f;
        float forwardLean = lean.Update((TargetLean, deltaTime), settings.spring);
        // Turning about +z leans an authored right-facing rig back, so forward is the opposite of the authored facing.
        rig.PostureRoot.localRotation = Quaternion.Euler(0f, 0f, -forwardLean * rig.Facing.AuthoredSign) * restRotation;
    }

    private float BaseLean() {
        StrideController stride = rig.Stride;
        return PostureLean.BaseLean((settings.idleLean, stride.CurrentGait.lean, stride.Pace.Moving));
    }

    // Uses the gait's ground speed rather than the body position, so the step's own surge and bob do not read as starts.
    private float MeasureAccelerationLean(float deltaTime) {
        float groundSpeed = rig.Stride.GroundSpeed;
        float forwardAcceleration = (groundSpeed - previousGroundSpeed) / deltaTime * rig.Facing.Sign;
        previousGroundSpeed = groundSpeed;
        return rig.Stride.IsDrivingGait ? PostureLean.AccelerationLean(forwardAcceleration, settings) : 0f;
    }
}
