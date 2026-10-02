// Reads and writes every tunable locomotion setting through its owners' settings properties, as one LocomotionPreset.
// In the Editor the changes live on the scene's components for the play session only; nothing is saved to assets.
public sealed class LocomotionTuning {
    private readonly StrideController stride;
    private readonly LocomotionPosture posture;
    private readonly TorsoMotion torso;
    private readonly HeadLook head;
    private readonly ArmSwing arms;

    public LocomotionTuning((StrideController stride, LocomotionPosture posture, TorsoMotion torso, HeadLook head, ArmSwing arms) owners) {
        stride = owners.stride;
        posture = owners.posture;
        torso = owners.torso;
        head = owners.head;
        arms = owners.arms;
    }

    public LocomotionPreset Capture() {
        return new LocomotionPreset {
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

    public void Apply(LocomotionPreset preset) {
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
