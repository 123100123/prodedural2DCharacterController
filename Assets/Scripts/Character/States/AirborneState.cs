using UnityEngine;

// Ballistic flight after a jump or a fall: air steering, flight leg pose, coyote jump, buffered jump and landing.
public sealed class AirborneState : ICharacterState {
    private readonly StateContext context;
    private readonly (AirborneSettings air, JumpSettings jump) settings;

    private float timeInAir;
    private float jumpBufferLeft;
    private bool canCoyoteJump;
    private (LegStepper lead, LegStepper trail) legs;

    public AirborneState(StateContext context, (AirborneSettings air, JumpSettings jump) settings) {
        this.context = context;
        this.settings = settings;
    }

    public bool AllowsUpperBodyActions => true;

    public void Launch(Vector2 velocity) => BeginFlight((velocity, false));

    // Only the walking speed carries over; vertical motion of the gait is not a launch.
    public void Fall(Vector2 walkVelocity) {
        float carriedSpeedX = Mathf.Clamp(walkVelocity.x, -settings.air.airSpeed, settings.air.airSpeed);
        BeginFlight((new Vector2(carriedSpeedX, 0f), true));
    }

    private void BeginFlight((Vector2 velocity, bool allowsCoyoteJump) entry) {
        context.Rig.Motor.Velocity = entry.velocity;
        canCoyoteJump = entry.allowsCoyoteJump;
        context.Machine.ChangeTo(this);
    }

    public void Enter() {
        timeInAir = 0f;
        jumpBufferLeft = 0f;
        legs = context.Rig.Stride.LegsOrderedByFacing(context.Rig.Facing.Sign);
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        timeInAir += deltaTime;
        jumpBufferLeft -= deltaTime;

        if (!commands.AimHeld) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        if (commands.JumpPressed) {
            JumpOrBufferJump();
        }

        SteerHorizontally((commands.MoveAxis, deltaTime));
        context.Rig.Motor.ApplyGravity(deltaTime);
        if (TryTouchDown(deltaTime)) {
            return;
        }

        context.Rig.Motor.MoveWithCollisions(deltaTime);
        PoseLegsForFlight(deltaTime);
        context.Rig.Stride.PlaceStrideTargetUnderBody();
    }

    public void Exit() { }

    private void JumpOrBufferJump() {
        KinematicBodyMotor motor = context.Rig.Motor;
        if (canCoyoteJump && timeInAir <= settings.air.coyoteTime) {
            motor.Velocity = new Vector2(motor.Velocity.x, settings.jump.LaunchSpeed(motor.Gravity));
            canCoyoteJump = false;
            return;
        }
        jumpBufferLeft = settings.air.jumpBufferTime;
    }

    private void SteerHorizontally((float moveAxis, float deltaTime) steering) {
        KinematicBodyMotor motor = context.Rig.Motor;
        float targetSpeed = steering.moveAxis * settings.air.airSpeed;
        float speedX = Mathf.MoveTowards(motor.Velocity.x, targetSpeed, settings.air.airAcceleration * steering.deltaTime);
        motor.Velocity = new Vector2(speedX, motor.Velocity.y);
    }

    // Legs tuck while rising and reach for the ground while falling.
    private void PoseLegsForFlight(float deltaTime) {
        float reaching = Mathf.InverseLerp(settings.air.poseBlendSpeed, -settings.air.poseBlendSpeed, context.Rig.Motor.Velocity.y);
        float ease = FrameRateEase.FractionForFrame(settings.air.legPoseEase, deltaTime);

        Vector2 leadOffset = Vector2.Lerp(settings.air.leadFootTucked, settings.air.leadFootReaching, reaching);
        Vector2 trailOffset = Vector2.Lerp(settings.air.trailFootTucked, settings.air.trailFootReaching, reaching);
        legs.lead.SwingFreelyToward(BodyPointFacingForward(leadOffset), ease);
        legs.trail.SwingFreelyToward(BodyPointFacingForward(trailOffset), ease);
    }

    // Feet never aim below the ground they are reaching for.
    private Vector2 BodyPointFacingForward(Vector2 offset) {
        Vector2 body = context.Rig.Body.position;
        Vector2 poseTarget = body + new Vector2(offset.x * context.Rig.Facing.Sign, offset.y);
        Vector2 rayOrigin = new Vector2(poseTarget.x, body.y);

        bool hitsGround = FootholdProbe.TryFindGroundBelow((rayOrigin, body.y - poseTarget.y), context.Rig.Stride.GroundMask, out Vector2 ground);
        return hitsGround ? ground : poseTarget;
    }

    // Swept landing: checks the whole fall of this frame so a long frame cannot carry the body through the ground.
    private bool TryTouchDown(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        StrideController stride = context.Rig.Stride;
        float fallThisFrame = -motor.Velocity.y * deltaTime;
        if (fallThisFrame < 0f || !stride.TryFindGroundBelowBody(fallThisFrame, out Vector2 ground)) {
            return false;
        }
        if (!stride.TrySettleStance((context.Rig.Body.position.x, context.Rig.Facing.Sign))) {
            return false;
        }

        float landingDip = Mathf.Min(settings.air.maxLandingDip, -motor.Velocity.y * settings.air.landingDipPerSpeed);
        PlaceBodyAtTouchdown((ground.y + stride.BodyHeight - landingDip, fallThisFrame));
        stride.AbsorbLanding(landingDip);
        motor.Velocity = Vector2.zero;
        ContinueAfterLanding();
        return true;
    }

    private void PlaceBodyAtTouchdown((float lowestY, float fallThisFrame) touchdown) {
        Transform body = context.Rig.Body;
        float landedY = Mathf.Max(body.position.y - touchdown.fallThisFrame, touchdown.lowestY);
        body.position = new Vector3(body.position.x, Mathf.Min(body.position.y, landedY), body.position.z);
    }

    private void ContinueAfterLanding() {
        if (jumpBufferLeft > 0f) {
            context.States.JumpWindup.Begin();
            return;
        }
        context.States.Walk.Begin();
    }
}
