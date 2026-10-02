// What every full-body state shares: the machine to switch with, the rig to move and the sibling states to switch to.
public readonly struct StateContext {
    public StateContext((CharacterStateMachine machine, CharacterRig rig, CharacterStates states) parts) {
        Machine = parts.machine;
        Rig = parts.rig;
        States = parts.states;
    }

    public CharacterStateMachine Machine { get; }
    public CharacterRig Rig { get; }
    public CharacterStates States { get; }
}
