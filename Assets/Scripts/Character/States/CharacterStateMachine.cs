// Holds the active full-body state and switches between states.
public sealed class CharacterStateMachine {
    public ICharacterState Current { get; private set; }

    public void ChangeTo(ICharacterState next) {
        Current?.Exit();
        Current = next;
        Current.Enter();
    }

    public void Tick(float deltaTime, PlayerCommands commands) => Current.Tick(deltaTime, commands);
}
