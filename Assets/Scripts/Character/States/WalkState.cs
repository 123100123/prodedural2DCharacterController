using UnityEngine;

// Grounded locomotion: the stride gait walks the character between walls, turning toward the movement direction.
public sealed class WalkState : ICharacterState {
    private readonly StateContext context;

    public WalkState(StateContext context) {
        this.context = context;
    }

    public bool AllowsUpperBodyActions => true;

    public void Begin() => context.Machine.ChangeTo(this);

    // Grounded states hand momentum over through the motor velocity: read on entry, written on exit.
    public void Enter() {
        context.Rig.Stride.BeginGroundedMotion(context.Rig.Motor.Velocity);
        context.Rig.Motor.Velocity = Vector2.zero;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        if (!commands.IsAiming) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        if (commands.JumpPressed) {
            if (!context.States.Climb.TryBeginFromStance()) {
                context.States.JumpWindup.Begin();
            }
            return;
        }
        if (commands.RollPressed && context.States.Roll.CanStart) {
            context.States.Roll.Begin();
            return;
        }

        context.Rig.Stride.TickWalk(deltaTime, GaitIntent(commands));

        if (context.Rig.Stride.IsPastSupportEdge || context.Rig.Stride.HasLostGround) {
            context.States.Airborne.Fall(context.Rig.Stride.BodyVelocity);
        }
    }

    public void Exit() {
        context.Rig.Motor.Velocity = context.Rig.Stride.BodyVelocity;
    }

    // Running needs the run input and no aiming: aiming slows the character to a walk.
    private (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) GaitIntent(PlayerCommands commands) {
        ((float min, float max) feet, (float min, float max) body) spans = SpansWithinWalls();
        return (commands.MoveAxis, commands.RunHeld && !commands.IsAiming, spans.feet, spans.body);
    }

    // The body may travel as far as its hull is free; feet may reach the wall face itself, a hull half-width further.
    private ((float min, float max) feet, (float min, float max) body) SpansWithinWalls() {
        float bodyX = context.Rig.Body.position.x;
        float lookAhead = context.Rig.Stride.WallProbeDistance;
        KinematicBodyMotor motor = context.Rig.Motor;

        (float min, float max) body = (bodyX - motor.FreeDistance(-1f, lookAhead), bodyX + motor.FreeDistance(1f, lookAhead));
        return ((body.min - motor.HullHalfWidth, body.max + motor.HullHalfWidth), body);
    }
}
