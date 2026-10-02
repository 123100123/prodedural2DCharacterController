using UnityEngine;

// Crouches briefly on planted feet, then launches the body into the air.
public sealed class JumpWindupState : ICharacterState {
    private readonly StateContext context;
    private readonly (JumpSettings jump, AirborneSettings air) settings;
    private float elapsed;

    public JumpWindupState(StateContext context, (JumpSettings jump, AirborneSettings air) settings) {
        this.context = context;
        this.settings = settings;
    }

    public bool AllowsUpperBodyActions => true;

    public void Begin() => context.Machine.ChangeTo(this);

    public void Enter() {
        elapsed = 0f;
        context.Rig.Stride.HeldCrouch = settings.jump.crouchDepth;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        elapsed += deltaTime;
        if (!commands.AimHeld) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        context.Rig.Stride.TickWalk(deltaTime, (0f, float.NegativeInfinity, float.PositiveInfinity));

        if (elapsed >= settings.jump.windupDuration) {
            context.States.Airborne.Launch(LaunchVelocity(commands.MoveAxis));
        }
    }

    public void Exit() {
        context.Rig.Stride.HeldCrouch = 0f;
    }

    private Vector2 LaunchVelocity(float moveAxis) {
        return new Vector2(moveAxis * settings.air.airSpeed, settings.jump.LaunchSpeed(context.Rig.Motor.Gravity));
    }
}
