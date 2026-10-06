using UnityEngine;

// Sliding down a wall: a falling character pressing into a tall wall catches it, turns its back to it and slides down at
// a capped speed, one hand braced on the wall above the shoulder, the feet pressed against it with the knees bent and the
// torso leaning out. A jump kicks off away from the wall (also just after dropping off it); a ledge in reach is climbed;
// letting go, the wall ending or the ground arriving hands the body back to the flight.
public sealed class WallSlideState : ICharacterState {
    // Horizontal speed holding the body against the wall; the motor stops it at the face every frame.
    private const float PressSpeed = 2f;
    // Feet reach at most this share of the leg's length, so the knee never locks.
    private const float MaxLegExtension = 0.985f;
    // The braced hand sits this far off the wall face (the palm on the surface, not in it).
    private const float HandWallGap = 0.03f;

    private readonly StateContext context;
    private readonly WallSettings settings;

    private int wallSide;
    private float wallFaceX;
    private float releasedFor;
    private float timeSliding;
    private bool jumpOnEntry;
    private (LegStepper lead, LegStepper trail) legs;
    // The wall last dropped off without a jump, and when: a jump soon after still kicks off it.
    private (int side, float faceX, float at) leftWall;

    public WallSlideState(StateContext context, WallSettings settings) {
        this.context = context;
        this.settings = settings;
        leftWall = (0, 0f, float.NegativeInfinity);
    }

    // Aiming and shooting need the arms, which brace on the wall.
    public bool AllowsUpperBodyActions => false;
    public bool AllowsCrouch => false;

    public bool IsSliding => context.Machine.Current == this;
    // +1 when the wall is to the right of the body, -1 to the left; 0 when not sliding.
    public int WallSide => IsSliding ? wallSide : 0;
    public float TimeSliding => IsSliding ? timeSliding : 0f;

    // The flight asks every frame after it moved. A ledge in reach toward the wall is grabbed rather than slid past.
    // jumpBuffered: a jump pressed just before catching the wall kicks off it at once.
    public bool TryBeginFromAir((float moveAxis, bool jumpBuffered) intent) {
        int side = WallSlidePolicy.PressedSide(intent.moveAxis);
        if (!settings.enabled || side == 0 || context.Rig.Motor.Velocity.y > settings.catchRiseSpeed) {
            return false;
        }

        WallProbe.Contact contact = Probe(side);
        if (!contact.Chest || !contact.Knee) {
            return false;
        }
        bool ledgeInReach = context.States.Climb.TryFindLedgeInReach(side, out Ledge ledge);
        float groundClearance = GroundClearance(settings.minGroundClearance);
        WallSlidePolicy.Catch wall = new WallSlidePolicy.Catch((context.Rig.Motor.Velocity.y, true), (contact.Chest, contact.Knee), (groundClearance, ledgeInReach));
        if (ledgeInReach) {
            context.States.Climb.Grab(ledge, intent.moveAxis);
            return true;
        }
        if (!WallSlidePolicy.CanCatch(wall, settings)) {
            return false;
        }

        wallSide = side;
        wallFaceX = contact.FaceX;
        jumpOnEntry = intent.jumpBuffered;
        context.Machine.ChangeTo(this);
        return true;
    }

    // A jump pressed in the air just after dropping off a wall: kicks off that wall as if still on it.
    public bool TryCoyoteWallJump() {
        if (!WallSlidePolicy.IsWithinCoyote((Time.time, leftWall.at, leftWall.side), settings)) {
            return false;
        }
        (wallSide, wallFaceX) = (leftWall.side, leftWall.faceX);
        KickOff();
        return true;
    }

    public void Enter() {
        releasedFor = 0f;
        timeSliding = 0f;
        leftWall = (0, 0f, float.NegativeInfinity);
        context.Rig.Facing.FaceToward(-wallSide);
        legs = context.Rig.Stride.LegsOrderedByFacing(context.Rig.Facing.Sign);
        context.Rig.Events.RaiseWallSlideStarted((new Vector2(wallFaceX, context.Rig.Body.position.y), wallSide));
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        timeSliding += deltaTime;
        if (commands.JumpPressed || jumpOnEntry) {
            KickOff();
            return;
        }

        bool pressingIntoWall = WallSlidePolicy.PressedSide(commands.MoveAxis) == wallSide;
        releasedFor = WallSlidePolicy.ReleasedFor((releasedFor, pressingIntoWall, deltaTime));
        SlideDown(deltaTime);

        WallProbe.Contact contact = Probe(wallSide);
        if (contact.IsTouching) {
            wallFaceX = contact.FaceX;
        }
        bool ledgeInReach = context.States.Climb.TryFindLedgeInReach(wallSide, out Ledge ledge);
        switch (WallSlidePolicy.Continue((ledgeInReach, LandsNextFrame(deltaTime), contact.Knee, releasedFor), settings)) {
            case WallSlidePolicy.Outcome.Ledge:
                context.States.Climb.Grab(ledge, commands.MoveAxis);
                return;
            case WallSlidePolicy.Outcome.Ground:
                DropOff(false);
                return;
            case WallSlidePolicy.Outcome.WallEnded:
            case WallSlidePolicy.Outcome.Released:
                DropOff(true);
                return;
        }

        context.Rig.Facing.FaceToward(-wallSide);
        PoseAgainstWall(deltaTime);
        context.Rig.Stride.PlaceStrideTargetUnderBody();
    }

    public void Exit() {
        context.Rig.AirPose = AirPoseFrame.None;
        context.Rig.Events.RaiseWallSlideStopped();
    }

    // Held against the wall while the fall speed eases to the slide speed.
    private void SlideDown(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        motor.Velocity = new Vector2(wallSide * PressSpeed, WallSlidePolicy.SlideSpeed((motor.Velocity.y, deltaTime), settings));
        motor.MoveWithCollisions(deltaTime);
    }

    // Away from the wall and up, through the flight's launch so the jump button's hold sets the height and the air
    // steering carries on; steering back toward the wall is ignored for a moment.
    private void KickOff() {
        int side = wallSide;
        Vector2 launch = WallSlidePolicy.WallJumpVelocity(side, settings);
        float fullHeight = WallSlidePolicy.WallJumpHeight(context.Rig.Motor.Gravity, settings);
        StrideController stride = context.Rig.Stride;
        Vector2 kickPoint = new Vector2(wallFaceX, Mathf.Min(stride.FrontLeg.FootTarget.y, stride.BackLeg.FootTarget.y));
        jumpOnEntry = false;
        leftWall = (0, 0f, float.NegativeInfinity);

        context.Rig.Facing.FaceToward(-side);
        context.Rig.Events.RaiseWallJumped((kickPoint, -side));
        context.States.Airborne.Launch(launch, (fullHeight, 0f, true));
        context.States.Airborne.LockSteering(new SteeringLockout(side, settings.steeringLockout));
    }

    // Leaves the wall with the velocity of the slide (no push away: the air steering takes the input from here).
    // allowsCoyote: dropping off the wall (not landing) leaves a moment in which a jump still kicks off it.
    private void DropOff(bool allowsCoyote) {
        int side = wallSide;
        float faceX = wallFaceX;
        context.States.Airborne.Drop(new Vector2(0f, context.Rig.Motor.Velocity.y));
        leftWall = allowsCoyote ? (side, faceX, Time.time) : (0, 0f, float.NegativeInfinity);
    }

    private WallProbe.Contact Probe(int side) {
        KinematicBodyMotor motor = context.Rig.Motor;
        return WallProbe.Touch((context.Rig.Body.position, side, motor.HullHalfWidth), (settings, motor.SolidMask, context.Rig.Stride.Slope.maxSlopeAngle));
    }

    // Gap between where the standing feet would be and the ground below; infinity when the ground is further than
    // lookBelow under them.
    private float GroundClearance(float lookBelow) {
        StrideController stride = context.Rig.Stride;
        if (!stride.TryFindGroundBelowBody(Mathf.Max(0f, lookBelow), out Vector2 ground)) {
            return float.PositiveInfinity;
        }
        return context.Rig.Body.position.y - ground.y - stride.BodyHeight;
    }

    // Whether the flight, starting next frame at the slide speed, touches down in that frame (it adds gravity first).
    private bool LandsNextFrame(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        float fallNextFrame = Mathf.Max(0f, -motor.Velocity.y + motor.CurrentGravity * deltaTime) * deltaTime;
        return GroundClearance(fallNextFrame) <= fallNextFrame;
    }

    // ---- Pose -------------------------------------------------------------------------------------------------

    private void PoseAgainstWall(float deltaTime) {
        context.Rig.AirPose = new AirPoseFrame(new AirPose { lean = settings.leanOut }, (0f, 0f));
        context.Rig.Head.HoldLevel();
        PoseArms();
        float ease = FrameRateEase.FractionForFrame(settings.limbPoseEase, deltaTime);
        legs.lead.SwingFreelyToward(FootOnWall((legs.lead, settings.footReach.x)), ease);
        legs.trail.SwingFreelyToward(FootOnWall((legs.trail, settings.footReach.y)), ease);
    }

    // The front hand braces flat on the wall above the shoulder; the back arm hangs out forward for balance.
    private void PoseArms() {
        ArmLimb front = context.Rig.FrontArm;
        Vector2 brace = new Vector2(wallFaceX - wallSide * HandWallGap, front.ShoulderPosition.y + settings.handAboveShoulder);
        front.ReachToward(front.ShoulderPosition + Vector2.ClampMagnitude(brace - front.ShoulderPosition, front.Reach * MaxLegExtension));

        ArmLimb back = context.Rig.BackArm;
        Vector2 free = new Vector2(settings.freeHand.x * context.Rig.Facing.Sign, settings.freeHand.y);
        back.ReachToward(back.ShoulderPosition + Vector2.ClampMagnitude(free, back.Reach * MaxLegExtension));
    }

    // A foot on the wall face below its hip, the hip-to-foot distance the given share of the leg (the knee bends the
    // rest); a foot whose hip is too far from the wall reaches as far toward it as the leg allows.
    private Vector2 FootOnWall((LegStepper leg, float reachShare) foot) {
        Vector2 hip = foot.leg.HipPosition;
        float reach = foot.leg.LegLength * Mathf.Clamp(foot.reachShare, 0.3f, MaxLegExtension);
        float footX = wallFaceX - wallSide * settings.footWallGap;
        float across = Mathf.Min(Mathf.Abs(footX - hip.x), reach);
        float down = Mathf.Sqrt(Mathf.Max(0f, reach * reach - across * across));
        return new Vector2(hip.x + Mathf.Sign(footX - hip.x) * across, hip.y - down);
    }
}
