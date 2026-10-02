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
        context.Rig.Stride.BeginGroundedMotion(context.Rig.Motor.Velocity);
        context.Rig.Stride.HeldCrouch = settings.jump.crouchDepth;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        elapsed += deltaTime;
        if (!commands.IsAiming) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        (float min, float max) unbounded = (float.NegativeInfinity, float.PositiveInfinity);
        context.Rig.Stride.TickWalk(deltaTime, (0f, unbounded, unbounded));

        if (elapsed >= settings.jump.windupDuration) {
            Vector2 launch = LaunchVelocity(commands.MoveAxis);
            context.Rig.Events.RaiseJumped(launch);
            context.States.Airborne.Launch(launch);
        }
    }

    public void Exit() {
        context.Rig.Stride.HeldCrouch = 0f;
    }

    private Vector2 LaunchVelocity(float moveAxis) {
        return new Vector2(moveAxis * settings.air.airSpeed, settings.jump.LaunchSpeed(context.Rig.Motor.Gravity));
    }
}
