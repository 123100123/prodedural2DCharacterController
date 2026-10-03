using UnityEngine;

// The character's movable parts as seen by the full-body states and the upper-body layer.
public sealed class CharacterRig {
    public CharacterRig(
        (StrideController stride, CharacterFacing facing, KinematicBodyMotor motor, CharacterEvents events) core,
        (ArmLimb frontArm, ArmLimb backArm, HeadLook head, Transform chest, Transform postureRoot) upperBody) {
        Stride = core.stride;
        Facing = core.facing;
        Motor = core.motor;
        Events = core.events;
        FrontArm = upperBody.frontArm;
        BackArm = upperBody.backArm;
        Head = upperBody.head;
        Chest = upperBody.chest;
        PostureRoot = upperBody.postureRoot;
    }

    public StrideController Stride { get; }
    public CharacterFacing Facing { get; }
    public KinematicBodyMotor Motor { get; }
    public CharacterEvents Events { get; }
    public ArmLimb FrontArm { get; }
    public ArmLimb BackArm { get; }
    public HeadLook Head { get; }
    public Transform Chest { get; }
    // The pelvis bone, which carries the sustained locomotion lean; the chest's own motion rides on top of it.
    public Transform PostureRoot { get; }
    public Transform Body => Stride.transform;

    // Applies this frame's arm and head requests; parts nobody posed relax toward rest.
    public void ApplyUpperBodyPose(float deltaTime) {
        FrontArm.ApplyPose(deltaTime);
        BackArm.ApplyPose(deltaTime);
        Head.ApplyPose(deltaTime);
    }
}
