using UnityEngine;

// Ballistic flight after a jump or a fall: air steering that keeps the momentum the flight started with, a jump cut
// short by releasing the button, flight leg pose, coyote jump, buffered jump, ledge grabs and landing.
public sealed class AirborneState : ICharacterState {
    private readonly StateContext context;
    private readonly (AirborneSettings air, JumpSettings jump, AirPoseSettings pose) settings;

    // Feet reach at most this share of the leg's length, so the knee never locks.
    private const float MaxLegExtension = 0.985f;

    private float timeInAir;
    private float timeFalling;
    private float timeSinceLaunch;
    private float jumpBufferLeft;
    private bool canCoyoteJump;
    private (LegStepper lead, LegStepper trail) legs;
    // The variable jump height: the full height of the jump (0 when the flight is a fall), how long the button was held,
    // whether it still is, and whether the rise has been settled (cut short, or held long enough to rise in full).
    private (float fullHeight, float heldFor, bool isHeld, bool isSettled) jump = (0f, 0f, false, true);
    private AirSnapshot snapshot;

    public AirborneState(StateContext context, (AirborneSettings air, JumpSettings jump, AirPoseSettings pose) settings) {
        this.context = context;
        this.settings = settings;
    }

    public bool AllowsUpperBodyActions => true;
    public bool AllowsCrouch => true;

    // Read-only view of the flight for debug tools.
    public AirSnapshot Snapshot => snapshot;

    // A jump: launched with its velocity, and how the jump button was held (releasing it early cuts the rise short).
    public void Launch(Vector2 velocity, (float fullHeight, float heldFor, bool isHeld) hold) {
        jump = (hold.fullHeight, hold.heldFor, hold.isHeld, false);
        BeginFlight((velocity, false, true));
    }

    // The whole ground speed carries over (up to the run speed); vertical motion of the gait is not a launch.
    public void Fall(Vector2 walkVelocity) {
        float runSpeed = context.Rig.Stride.Run.speed;
        float carriedSpeedX = Mathf.Clamp(walkVelocity.x, -runSpeed, runSpeed);
        jump = (0f, 0f, false, true);
        BeginFlight((new Vector2(carriedSpeedX, 0f), true, false));
    }

    // The velocity is set after the change: the state being left may write the motor velocity in its Exit.
    private void BeginFlight((Vector2 velocity, bool allowsCoyoteJump, bool isJump) entry) {
        canCoyoteJump = entry.allowsCoyoteJump;
        context.Machine.ChangeTo(this);
        context.Rig.Motor.Velocity = entry.velocity;
        snapshot.LaunchVelocity = entry.velocity;
        snapshot.LaunchedByJump = entry.isJump;
    }

    public void Enter() {
        timeInAir = 0f;
        timeFalling = 0f;
        timeSinceLaunch = 0f;
        jumpBufferLeft = 0f;
        legs = context.Rig.Stride.LegsOrderedByFacing(context.Rig.Facing.Sign);
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        timeInAir += deltaTime;
        timeSinceLaunch += deltaTime;
        timeFalling += context.Rig.Motor.Velocity.y < 0f ? deltaTime : 0f;
        jumpBufferLeft -= deltaTime;

        if (!commands.IsAiming) {
            context.Rig.Facing.FaceToward(commands.MoveAxis);
        }
        if (commands.JumpPressed) {
            JumpOrBufferJump();
        }

        CutJumpShortWhenReleased((commands.JumpHeld, deltaTime));
        SteerHorizontally((commands.MoveAxis, deltaTime));
        context.Rig.Motor.ApplyGravity(deltaTime);
        if (TryTouchDown(deltaTime)) {
            return;
        }

        context.Rig.Motor.MoveWithCollisions(deltaTime);
        if (context.States.Climb.TryBeginFromAir()) {
            return;
        }
        SolvePose();
        PoseLegsForFlight(deltaTime);
        context.Rig.Stride.PlaceStrideTargetUnderBody();
        UpdateSnapshot();
    }

    // The pose of this frame of the flight, read by the legs here and by the arms, posture and chest.
    private void SolvePose() {
        Vector2 velocity = context.Rig.Motor.Velocity;
        AirbornePoseSolver.Motion motion = new AirbornePoseSolver.Motion((velocity.y, velocity.x), context.Rig.Stride.Run.speed, timeSinceLaunch);
        context.Rig.AirPose = new AirPoseFrame(AirbornePoseSolver.Solve(motion, settings.pose));

        AirPhase phase = AirPhase.From(velocity.y, settings.pose.apexBand);
        snapshot.Ascent = phase.Ascent;
        snapshot.Apex = phase.Apex;
        snapshot.Descent = phase.Descent;
        snapshot.RunningPoseShare = AirbornePoseSolver.RunningShare(motion, settings.pose);
    }

    private void UpdateSnapshot() {
        snapshot.IsAirborne = true;
        snapshot.Velocity = context.Rig.Motor.Velocity;
        snapshot.TimeAirborne = timeInAir;
        snapshot.TimeFalling = timeFalling;
        snapshot.TimeSinceLaunch = timeSinceLaunch;
    }

    public void Exit() {
        snapshot = default;
        context.Rig.AirPose = AirPoseFrame.None;
    }

    private void JumpOrBufferJump() {
        KinematicBodyMotor motor = context.Rig.Motor;
        if (canCoyoteJump && timeInAir <= settings.air.coyoteTime) {
            float fullHeight = JumpMomentum.FullHeight(settings.jump, context.Rig.Stride.CrouchShare);
            motor.Velocity = new Vector2(motor.Velocity.x, JumpMath.LaunchSpeedForHeight(motor.Gravity, fullHeight));
            jump = (fullHeight, 0f, true, false);
            timeSinceLaunch = 0f;
            snapshot.LaunchedByJump = true;
            snapshot.LaunchVelocity = motor.Velocity;
            canCoyoteJump = false;
            return;
        }
        jumpBufferLeft = settings.air.jumpBufferTime;
    }

    private void SteerHorizontally((float moveAxis, float deltaTime) steering) {
        KinematicBodyMotor motor = context.Rig.Motor;
        float speedX = AirSteering.Step((motor.Velocity.x, steering.moveAxis, steering.deltaTime), settings.air);
        motor.Velocity = new Vector2(speedX, motor.Velocity.y);
    }

    // Releasing the jump button while still rising cuts the rise to the height the button was held for: a tap is a hop,
    // a hold of the full time (or past the apex) is the whole jump.
    private void CutJumpShortWhenReleased((bool isJumpHeld, float deltaTime) frame) {
        KinematicBodyMotor motor = context.Rig.Motor;
        if (jump.isSettled) {
            return;
        }
        if (motor.Velocity.y <= 0f) {
            jump.isSettled = true;
            return;
        }
        if (frame.isJumpHeld && jump.isHeld) {
            jump.heldFor += frame.deltaTime;
            jump.isSettled = jump.heldFor >= settings.jump.fullHoldTime;
            return;
        }

        float heldHeight = JumpMath.HeldJumpHeight(jump.heldFor, (settings.jump.minHopHeight, jump.fullHeight, settings.jump.fullHoldTime));
        motor.Velocity = new Vector2(motor.Velocity.x, JumpMath.CutRise(motor.Velocity.y, motor.Gravity, heldHeight));
        jump.isHeld = false;
        jump.isSettled = true;
    }

    // The legs follow the flight pose: the push-off, the tuck at the top and the reach down for the ground.
    private void PoseLegsForFlight(float deltaTime) {
        AirPose pose = context.Rig.AirPose.Pose;
        float ease = FrameRateEase.FractionForFrame(settings.air.legPoseEase, deltaTime);
        legs.lead.SwingFreelyToward(FootTargetAboveGround((pose.leadFoot, legs.lead)), ease);
        legs.trail.SwingFreelyToward(FootTargetAboveGround((pose.trailFoot, legs.trail)), ease);
    }

    // Feet never aim below the ground they are reaching for, nor further from the hip than the leg is long.
    private Vector2 FootTargetAboveGround((Vector2 offset, LegStepper leg) foot) {
        Vector2 body = context.Rig.Body.position;
        Vector2 offset = foot.offset;
        Vector2 hip = foot.leg.HipPosition;
        Vector2 requested = body + new Vector2(offset.x * context.Rig.Facing.Sign, offset.y);
        Vector2 poseTarget = hip + Vector2.ClampMagnitude(requested - hip, foot.leg.LegLength * MaxLegExtension);
        Vector2 rayOrigin = new Vector2(poseTarget.x, body.y);

        bool hitsGround = FootholdProbe.TryFindGroundBelow((rayOrigin, body.y - poseTarget.y), context.Rig.Stride.GroundMask, out Vector2 ground);
        return hitsGround ? ground : poseTarget;
    }

    // Swept landing: checks the whole fall of this frame so a long frame cannot carry the body through the ground.
    // Part of the landing speed is handed to the grounded state, whose body spring turns it into the squash.
    private bool TryTouchDown(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        StrideController stride = context.Rig.Stride;
        float fallThisFrame = -motor.Velocity.y * deltaTime;
        if (fallThisFrame < 0f || !stride.TryFindGroundBelowBody(fallThisFrame, out Vector2 ground)) {
            return false;
        }
        if (!stride.TrySettleStance((context.Rig.Body.position.x, context.Rig.Facing.Sign, ground.y))) {
            return false;
        }

        float impactSpeed = -motor.Velocity.y;
        PlaceBodyAtTouchdown((ground.y + stride.BodyHeight - settings.air.touchdownSink, fallThisFrame));
        float carriedFallSpeed = Mathf.Min(settings.air.maxCarriedLandingSpeed, impactSpeed * settings.air.landingSpeedCarry);
        motor.Velocity = new Vector2(motor.Velocity.x, -carriedFallSpeed);
        context.Rig.Events.RaiseLanded((ground, impactSpeed));
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
            context.States.JumpWindup.Begin(false);
            return;
        }
        context.States.Walk.Begin();
    }
}
