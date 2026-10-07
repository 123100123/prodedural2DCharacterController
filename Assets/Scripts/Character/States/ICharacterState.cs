// One exclusive full-body behaviour (walking, airborne, rolling, climbing, sliding down a wall, sliding along the ground).
public interface ICharacterState {
    // A fixed name for debug tools (a constant: reading it allocates nothing).
    string Name { get; }

    // Whether upper-body actions such as aiming and shooting may run on top of this state.
    bool AllowsUpperBodyActions { get; }

    // Whether the character may crouch in this state (crouching is a modifier layered over the states that allow it).
    bool AllowsCrouch { get; }

    // Whether this state could run with this frame's commands, and if not, why. Each state answers for itself; a state
    // that cannot run acts on commands.RunIntent at its own legal exit. A query: it changes nothing.
    RunReadiness RunReadinessFor(PlayerCommands commands);

    void Enter();

    void Tick(float deltaTime, PlayerCommands commands);

    void Exit();
}
