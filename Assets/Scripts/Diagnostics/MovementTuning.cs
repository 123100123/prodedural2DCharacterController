// Reads and writes every tunable movement setting through its owners' settings properties, as one MovementPreset.
// In the Editor the changes live on the scene's components for the play session only; nothing is saved to assets.
public sealed class MovementTuning {
    private readonly StrideController stride;
    private readonly LocomotionPosture posture;
    private readonly TorsoMotion torso;
    private readonly HeadLook head;
    private readonly ArmSwing arms;

    public MovementTuning(CharacterParts parts) {
        stride = parts.Stride;
        posture = parts.Posture;
        torso = parts.Torso;
        head = parts.Head;
        arms = parts.Arms;
    }

    public MovementPreset Capture() {
        return new MovementPreset {
            walk = stride.Walk,
            run = stride.Run,
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
