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

        context.Rig.Stride.TickWalk(deltaTime, StrideWithinWalls(commands.MoveAxis));

        if (context.Rig.Stride.IsPastSupportEdge || context.Rig.Stride.HasLostGround) {
            context.States.Airborne.Fall(context.Rig.Stride.BodyVelocity);
        }
    }

    public void Exit() {
        context.Rig.Motor.Velocity = context.Rig.Stride.BodyVelocity;
    }

    // The body may travel as far as its hull is free; feet may reach the wall face itself, a hull half-width further.
    private (float direction, (float min, float max) feetSpan, (float min, float max) bodySpan) StrideWithinWalls(float moveAxis) {
        float bodyX = context.Rig.Body.position.x;
        float maxOffset = context.Rig.Stride.MaxStrideOffset;
        KinematicBodyMotor motor = context.Rig.Motor;

        float freeBehind = motor.FreeDistance(-1f, maxOffset);
        float freeAhead = motor.FreeDistance(1f, maxOffset);
        (float min, float max) bodySpan = (bodyX - freeBehind, bodyX + freeAhead);
        (float min, float max) feetSpan = (bodySpan.min - motor.HullHalfWidth, bodySpan.max + motor.HullHalfWidth);
        return (moveAxis, feetSpan, bodySpan);
    }
}
