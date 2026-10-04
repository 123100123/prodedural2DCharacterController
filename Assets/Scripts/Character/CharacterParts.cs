using UnityEngine;

// The collaborators a composed character hands to tools that tune and inspect it. Plain references, so the
// character knows nothing about who uses them: the tools depend on the character, never the other way round.
public readonly struct CharacterParts {
    public CharacterParts((StrideController stride, LocomotionPosture posture, TorsoMotion torso, HeadLook head, ArmSwing arms) movement, (CrouchController crouch, Transform postureRoot) body, (JumpSettings jump, AirborneSettings airborne, AirborneState airborneState, (AirPoseSettings pose, FallSecondarySettings fall) air) moves) {
        Stride = movement.stride;
        Posture = movement.posture;
        Torso = movement.torso;
        Head = movement.head;
        Arms = movement.arms;
        Crouch = body.crouch;
        PostureRoot = body.postureRoot;
        Jump = moves.jump;
        Airborne = moves.airborne;
        AirborneState = moves.airborneState;
        AirPose = moves.air.pose;
        FallSecondary = moves.air.fall;
    }

    public StrideController Stride { get; }
    public LocomotionPosture Posture { get; }
    public TorsoMotion Torso { get; }
    public HeadLook Head { get; }
    public ArmSwing Arms { get; }
    public CrouchController Crouch { get; }
    public Transform PostureRoot { get; }
    public JumpSettings Jump { get; }
    public AirborneSettings Airborne { get; }
    // The flight state, for its read-only snapshot.
    public AirborneState AirborneState { get; }
    public AirPoseSettings AirPose { get; }
    public FallSecondarySettings FallSecondary { get; }
}
