using UnityEngine;

// Loads a jump on planted feet, then launches the body into the air. The windup shrinks as the character moves faster
// (a running jump leaves the ground off its stride) and the gait keeps carrying the body at the speed it had, so the
// launch inherits it (JumpMomentum). Holding the jump button through the windup and the rise gives the full height.
public sealed class JumpWindupState : ICharacterState {
    private readonly StateContext context;
    private readonly (JumpSettings jump, AirborneSettings air) settings;
    private float elapsed;
    private float duration;
    private bool continuesGait;
    private (float heldFor, bool isHeld) hold;

    public JumpWindupState(StateContext context, (JumpSettings jump, AirborneSettings air) settings) {
        this.context = context;
        this.settings = settings;
    }

    public bool AllowsUpperBodyActions => true;
    public bool AllowsCrouch => true;

    // From the grounded gait, which carries on through the windup; or after a landing (a jump pressed just before it),
    // when the gait is handed the landing momentum first.
    public void Begin(bool fromGait) {
        continuesGait = fromGait;
        context.Machine.ChangeTo(this);
    }

    public void Enter() {
        StrideController stride = context.Rig.Stride;
        elapsed = 0f;
        hold = (0f, true);
        if (!continuesGait) {
            stride.BeginGroundedMotion(context.Rig.Motor.Velocity);
        }

        (float speedShare, float crouchShare) motion = (JumpMomentum.SpeedShare(stride.GroundSpeed, stride.Run.speed), stride.CrouchShare);
        duration = JumpMomentum.WindupDuration(motion, settings.jump);
        stride.WindupCrouch = JumpMomentum.WindupDepth(motion, settings.jump);
        stride.HoldGroundSpeed = true;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        elapsed += deltaTime;
        if (hold.isHeld) {
            hold = commands.JumpHeld ? (hold.heldFor + deltaTime, true) : (hold.heldFor, false);
        }
        if (!commands.IsAiming) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        ((float min, float max) feet, (float min, float max) body) spans = WallSpans.Within(context.Rig);
        context.Rig.Stride.TickWalk(deltaTime, (0f, false, spans.feet, spans.body));

        if (elapsed >= duration) {
            Launch();
        }
    }

    public void Exit() {
        context.Rig.Stride.WindupCrouch = 0f;
        context.Rig.Stride.HoldGroundSpeed = false;
    }

    private void Launch() {
        StrideController stride = context.Rig.Stride;
        float fullHeight = JumpMomentum.FullHeight(settings.jump, stride.CrouchShare);
        Vector2 launch = new Vector2(
            JumpMomentum.LaunchSpeedX((stride.GroundSpeed, stride.Run.speed), settings.jump),
            JumpMath.LaunchSpeedForHeight(context.Rig.Motor.Gravity, fullHeight));
        context.Rig.Events.RaiseJumped(launch);
        context.States.Airborne.Launch(launch, (fullHeight, hold.heldFor, hold.isHeld));
    }
}
