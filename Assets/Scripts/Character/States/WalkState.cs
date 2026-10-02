// Grounded locomotion: the stride gait walks the character, turning toward the movement direction.
public sealed class WalkState : ICharacterState {
    private readonly CharacterStateMachine machine;
    private readonly StrideController stride;
    private readonly CharacterFacing facing;

    public WalkState(CharacterStateMachine machine, (StrideController stride, CharacterFacing facing) body) {
        this.machine = machine;
        stride = body.stride;
        facing = body.facing;
    }

    public bool AllowsUpperBodyActions => true;

    public void Begin() => machine.ChangeTo(this);

    public void Enter() { }

    public void Tick(float deltaTime, PlayerCommands commands) {
        if (!commands.AimHeld) {
            facing.FaceToward(commands.MoveAxis);
        }
        stride.TickWalk(deltaTime, commands.MoveAxis);
    }

    public void Exit() { }
}
