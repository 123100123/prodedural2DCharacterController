using UnityEngine;

// The character's movable parts as seen by the full-body states and the upper-body layer.
public sealed class CharacterRig {
    public CharacterRig(
        (StrideController stride, CharacterFacing facing, KinematicBodyMotor motor) lowerBody,
        (ArmLimb frontArm, ArmLimb backArm, HeadLook head, Transform chest) upperBody) {
        Stride = lowerBody.stride;
        Facing = lowerBody.facing;
        Motor = lowerBody.motor;
        FrontArm = upperBody.frontArm;
        BackArm = upperBody.backArm;
        Head = upperBody.head;
        Chest = upperBody.chest;
    }

    public StrideController Stride { get; }
    public CharacterFacing Facing { get; }
    public KinematicBodyMotor Motor { get; }
    public ArmLimb FrontArm { get; }
    public ArmLimb BackArm { get; }
    public HeadLook Head { get; }
    public Transform Chest { get; }
    public Transform Body => Stride.transform;

    // Applies this frame's arm and head requests; parts nobody posed relax toward rest.
    public void ApplyUpperBodyPose(float deltaTime) {
        FrontArm.ApplyPose(deltaTime);
        BackArm.ApplyPose(deltaTime);
        Head.ApplyPose(deltaTime);
    }
}
