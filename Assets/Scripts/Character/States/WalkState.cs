using UnityEngine;

// Grounded locomotion: the stride gait walks the character between walls, turning toward the movement direction.
public sealed class WalkState : ICharacterState {
    private readonly StateContext context;
    private Vector2 lastBodyPosition;
    private Vector2 bodyVelocity;

    public WalkState(StateContext context) {
        this.context = context;
    }

    public bool AllowsUpperBodyActions => true;

    public void Begin() => context.Machine.ChangeTo(this);

    public void Enter() {
        lastBodyPosition = context.Rig.Body.position;
        bodyVelocity = Vector2.zero;
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
        if (commands.RollPressed && context.States.Roll.IsReady) {
            context.States.Roll.Begin();
            return;
        }

        context.Rig.Stride.TickWalk(deltaTime, StrideWithinWalls(commands.MoveAxis));
        TrackBodyVelocity(deltaTime);

        if (context.Rig.Stride.IsPastSupportEdge || context.Rig.Stride.HasLostGround) {
            context.States.Airborne.Fall(bodyVelocity);
        }
    }

    public void Exit() { }

    // Feet may reach the wall face itself, which lies a hull half-width beyond the hull's free travel.
    private (float direction, float minX, float maxX) StrideWithinWalls(float moveAxis) {
        float bodyX = context.Rig.Body.position.x;
        float maxOffset = context.Rig.Stride.MaxStrideOffset;
        KinematicBodyMotor motor = context.Rig.Motor;

        float reachBehind = motor.FreeDistance(-1f, maxOffset) + motor.HullHalfWidth;
        float reachAhead = motor.FreeDistance(1f, maxOffset) + motor.HullHalfWidth;
        return (moveAxis, bodyX - reachBehind, bodyX + reachAhead);
    }

    private void TrackBodyVelocity(float deltaTime) {
        Vector2 bodyPosition = context.Rig.Body.position;
        if (deltaTime > 0f) {
            bodyVelocity = (bodyPosition - lastBodyPosition) / deltaTime;
        }
        lastBodyPosition = bodyPosition;
    }
}
