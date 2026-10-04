// One exclusive full-body behaviour (walking, airborne, rolling, climbing).
public interface ICharacterState {
    // Whether upper-body actions such as aiming and shooting may run on top of this state.
    bool AllowsUpperBodyActions { get; }

    // Whether the character may crouch in this state (crouching is a modifier layered over the states that allow it).
    bool AllowsCrouch { get; }

    void Enter();

    void Tick(float deltaTime, PlayerCommands commands);

    void Exit();
}
