// Reads and writes every tunable movement setting through its owners' settings properties, as one MovementPreset.
// In the Editor the changes live on the scene's components for the play session only; nothing is saved to assets.
public sealed class MovementTuning {
    private readonly StrideController stride;
    private readonly LocomotionPosture posture;
    private readonly TorsoMotion torso;
    private readonly HeadLook head;
    private readonly ArmSwing arms;
    private readonly CrouchController crouch;
    private readonly JumpSettings jump;
    private readonly AirborneSettings air;
    private readonly AirPoseSettings airPose;
    private readonly FallSecondarySettings fallSecondary;
    private readonly LandingSettings landing;

    public MovementTuning(CharacterParts parts) {
        stride = parts.Stride;
        posture = parts.Posture;
        torso = parts.Torso;
        head = parts.Head;
        arms = parts.Arms;
        crouch = parts.Crouch;
        jump = parts.Jump;
        air = parts.Airborne;
        airPose = parts.AirPose;
        fallSecondary = parts.FallSecondary;
        landing = parts.Landing;
    }

    public MovementPreset Capture() {
        return new MovementPreset {
            walk = stride.Walk,
            run = stride.Run,
            crouchGait = stride.CrouchGait,
            crouch = crouch.Settings,
            jump = jump.Clone(),
            air = air.Clone(),
            airPose = airPose.Clone(),
            fallSecondary = fallSecondary.Clone(),
            landing = landing.Clone(),
            transitions = stride.Transitions,
            balance = stride.Balance,
            idle = stride.IdleStance,
            bodyHeight = stride.BodyHeight,
            body = stride.BodyMass,
            posture = posture.Settings,
            chestSpring = torso.Spring,
            breathing = torso.Breath,
            head = head.Motion,
            arms = arms.Settings,
        };
    }

    public void Apply(MovementPreset preset) {
        stride.Walk = preset.walk;
        stride.Run = preset.run;
        stride.CrouchGait = preset.crouchGait;
        crouch.Settings = preset.crouch;
        jump.CopyFrom(preset.jump);
        air.CopyFrom(preset.air);
        airPose.CopyFrom(preset.airPose);
        fallSecondary.CopyFrom(preset.fallSecondary);
        landing.CopyFrom(preset.landing);
        stride.Transitions = preset.transitions;
        stride.Balance = preset.balance;
        stride.IdleStance = preset.idle;
        stride.BodyHeight = preset.bodyHeight;
        stride.BodyMass = preset.body;
        posture.Settings = preset.posture;
        torso.Spring = preset.chestSpring;
        torso.Breath = preset.breathing;
        head.Motion = preset.head;
        arms.Settings = preset.arms;
    }
}
