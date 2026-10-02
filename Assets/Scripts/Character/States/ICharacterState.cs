// One exclusive full-body behaviour (walking, airborne, rolling, climbing).
public interface ICharacterState {
    // Whether upper-body actions such as aiming and shooting may run on top of this state.
    bool AllowsUpperBodyActions { get; }

    void Enter();

    void Tick(float deltaTime, PlayerCommands commands);

    void Exit();
}
