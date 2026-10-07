using UnityEngine;

// Holds the active full-body state and switches between states, keeping a record of the last switch for debug tools.
public sealed class CharacterStateMachine {
    public ICharacterState Current { get; private set; }
    // The state left at the last switch; null before the first.
    public ICharacterState Previous { get; private set; }
    // Time.time of the last switch.
    public float LastChangeTime { get; private set; }
    // How many switches there have been.
    public int ChangeCount { get; private set; }

    public void ChangeTo(ICharacterState next) {
        Current?.Exit();
        Previous = Current;
        Current = next;
        LastChangeTime = Time.time;
        ChangeCount++;
        Current.Enter();
    }

    public void Tick(float deltaTime, PlayerCommands commands) => Current.Tick(deltaTime, commands);
}
