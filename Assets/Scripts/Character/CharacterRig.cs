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
    // The pose the upper body takes while the character is in the air; none on the ground.
    public AirPoseFrame AirPose { get; set; }
    // Set by a state whose pose needs the body to keep the way it faces (a running slide faces along the slide): aiming
    // then points the arms without turning the character.
    public bool HoldsFacing { get; set; }
    // Degrees the torso leans forward into a push on top of the locomotion posture, already blended in and out (0 when
    // not pushing); set by the push pose every frame.
    public float PushLean { get; set; }

    // Applies this frame's arm and head requests; parts nobody posed relax toward rest.
    public void ApplyUpperBodyPose(float deltaTime) {
        FrontArm.ApplyPose(deltaTime);
        BackArm.ApplyPose(deltaTime);
        Head.ApplyPose(deltaTime);
    }
}
