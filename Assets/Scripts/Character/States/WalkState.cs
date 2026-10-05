using UnityEngine;

// Grounded locomotion: the stride gait walks the character between walls, turning toward the movement direction. Down +
// jump on a one-way platform drops through it instead of jumping.
public sealed class WalkState : ICharacterState {
    private readonly StateContext context;

    public WalkState(StateContext context) {
        this.context = context;
    }

    public bool AllowsUpperBodyActions => true;
    public bool AllowsCrouch => true;

    public void Begin() => context.Machine.ChangeTo(this);

    // Grounded states hand momentum over through the motor velocity: read on entry, written on exit. Walking always
    // begins on feet just placed in a stance (spawning, landing, after a roll or a climb), so the gait starts from it.
    public void Enter() {
        context.Rig.Stride.BeginGroundedMotion(context.Rig.Motor.Velocity);
        context.Rig.Stride.RestartGaitFromStance();
        context.Rig.Motor.Velocity = Vector2.zero;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        if (!commands.IsAiming) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        if (commands.JumpPressed) {
            if (commands.DownHeld && TryDropThroughPlatform()) {
                return;
            }
            if (!context.States.Climb.TryBeginFromStance(commands.MoveAxis)) {
                context.States.JumpWindup.Begin(true);
            }
            return;
        }
        if (commands.RollPressed && context.States.Roll.CanStart) {
            context.States.Roll.Begin();
            return;
        }
        if (context.States.Slide.TryBeginFromWalk(commands)) {
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

    // Only when everything under the feet is one-way: the feet let go of the platforms, which the ground queries ignore for
    // a moment, and the body falls through them with the speed it walked at (no jump, no coyote jump).
    private bool TryDropThroughPlatform() {
        StrideController stride = context.Rig.Stride;
        bool frontHasGround = stride.TryFindGroundUnderFoot(stride.FrontLeg, out Collider2D frontGround);
        bool backHasGround = stride.TryFindGroundUnderFoot(stride.BackLeg, out Collider2D backGround);
        if (!OneWayPassage.CanDropThrough((frontHasGround, OneWayPlatform.IsOneWay(frontGround)), (backHasGround, OneWayPlatform.IsOneWay(backGround)))) {
            return false;
        }

        stride.OneWay.DropThrough((frontGround, backGround), Time.time);
        context.Rig.Events.RaiseDroppedThroughPlatform(new Vector2(context.Rig.Body.position.x, stride.StanceHeight));
        float runSpeed = stride.Run.speed;
        context.States.Airborne.Drop(new Vector2(Mathf.Clamp(stride.BodyVelocity.x, -runSpeed, runSpeed), 0f));
        return true;
    }

    // Running needs the run input and no aiming: aiming slows the character to a walk.
    private (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) GaitIntent(PlayerCommands commands) {
        ((float min, float max) feet, (float min, float max) body) spans = WallSpans.Within(context.Rig);
        return (commands.MoveAxis, commands.RunHeld && !commands.IsAiming, spans.feet, spans.body);
    }
}
