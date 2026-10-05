using UnityEngine;

// Running slide: crouch pressed at speed drops the body low and shoots it forward on the ground, the lead leg stretched
// ahead along the ground, the trail leg folded under, the torso leaning back with the back hand trailing near the ground
// and the front hand held forward. Friction slows it; it ends into the crouch walk (or the stand, crouch released where
// there is room), stops against a wall, leaves a ledge into a fall, and a jump launches out of it with its momentum.
// The body moves through the motor with the crouch hull (the crouch is held for the whole slide), like the roll.
public sealed class SlideState : ICharacterState {
    // The ground ray starts this far above the body pivot.
    private const float GroundProbeHeadroom = 0.5f;
    // Feet and hands reach at most this share of the limb's length, so the joints never lock.
    private const float MaxLimbExtension = 0.985f;
    // Ending faster than this share of the walk speed, the feet take a moving gait's positions and the gait carries on
    // (as after a running landing); slower, they take the standing stance.
    private const float MovingStanceShare = 0.6f;

    private readonly StateContext context;
    private readonly (SlideSettings slide, JumpSettings jump) settings;

    private int direction;
    private float speed;
    private float groundY;
    private float readyAt = float.NegativeInfinity;
    private bool isAiming;
    private (LegStepper lead, LegStepper trail) legs;

    public SlideState(StateContext context, (SlideSettings slide, JumpSettings jump) settings) {
        this.context = context;
        this.settings = settings;
    }

    // Aiming and shooting run on top of the slide: the shooting stance takes the arms and head, the slide keeps the legs,
    // the low body, the lean and the facing (a pointer behind is held at the aim's steepest angle in front rather than
    // turning the character, whose legs would then fold the wrong way). Not aiming, the slide poses the arms itself.
    public bool AllowsUpperBodyActions => isAiming;
    // The slide holds the crouch itself (the crouch hull and the low crouch share it hands back to the walk).
    public bool AllowsCrouch => true;

    public bool IsSliding => context.Machine.Current == this;
    // The crouch is held while sliding, whatever the input.
    public bool HoldsCrouch => IsSliding;
    // +1 sliding right, -1 left; 0 when not sliding.
    public int Direction => IsSliding ? direction : 0;
    // Unsigned slide speed; 0 when not sliding.
    public float Speed => IsSliding ? speed : 0f;

    // The walk asks every frame before it steps: a crouch press at speed on the ground starts a slide.
    public bool TryBeginFromWalk(PlayerCommands commands) {
        if (!commands.CrouchPressed || !settings.slide.enabled) {
            return false;
        }
        StrideController stride = context.Rig.Stride;
        bool runIntent = SlidePolicy.IsRunIntent((commands.RunHeld, commands.MoveAxis, commands.IsAiming));
        bool grounded = !stride.HasLostGround && !stride.IsPastSupportEdge;
        SlidePolicy.Entry entry = new SlidePolicy.Entry((true, runIntent), (stride.GroundSpeed, stride.Run.speed), (grounded, SlidePolicy.IsReady(Time.time, readyAt)));
        if (!SlidePolicy.CanStart(entry, settings.slide)) {
            return false;
        }

        direction = stride.GroundSpeed >= 0f ? 1 : -1;
        speed = SlidePolicy.StartSpeed(stride.GroundSpeed, settings.slide);
        isAiming = commands.IsAiming;
        context.Machine.ChangeTo(this);
        return true;
    }

    public void Enter() {
        StrideController stride = context.Rig.Stride;
        groundY = stride.StanceHeight;
        legs = stride.LegsOrderedByFacing(direction);
        context.Rig.Motor.Velocity = new Vector2(speed * direction, 0f);
        context.Rig.Facing.FaceToward(direction);
        context.Rig.HoldsFacing = true;
        context.Rig.Events.RaiseSlideStarted((new Vector2(context.Rig.Body.position.x, groundY), direction));
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        isAiming = commands.IsAiming;

        speed = SlidePolicy.NextSpeed(speed, deltaTime, settings.slide);
        bool blocked = !commands.JumpPressed && TravelIsBlocked(deltaTime);
        bool groundLost = !TryFollowGround();
        bool roomToStand = !commands.CrouchRequested && HasRoomToStand();
        SlidePolicy.Frame frame = new SlidePolicy.Frame((commands.JumpPressed, commands.CrouchRequested), (groundLost, blocked, speed), roomToStand);

        switch (SlidePolicy.Continue(frame, settings.slide)) {
            case SlidePolicy.Outcome.Jump:
                JumpOut();
                return;
            case SlidePolicy.Outcome.GroundLost:
                context.States.Airborne.Fall(new Vector2(speed * direction, 0f));
                return;
            case SlidePolicy.Outcome.HitWall:
            case SlidePolicy.Outcome.Stopped:
                if (!TryGetUp()) {
                    context.States.Airborne.Fall(new Vector2(speed * direction, 0f));
                }
                return;
            case SlidePolicy.Outcome.Released:
                if (TryGetUp()) {
                    return;
                }
                break;
        }

        FollowGroundHeight(deltaTime);
        Pose(deltaTime);
        context.Rig.Stride.PlaceStrideTargetUnderBody();
    }

    // The walk reads the slide's speed on entry; the flight is handed its own velocity after the change.
    public void Exit() {
        context.Rig.Motor.Velocity = new Vector2(speed * direction, 0f);
        context.Rig.AirPose = AirPoseFrame.None;
        context.Rig.HoldsFacing = false;
        readyAt = Time.time + settings.slide.cooldown;
        context.Rig.Events.RaiseSlideStopped();
    }

    // ---- Motion ----------------------------------------------------------------------------------------------

    // The hull slides along the ground at the slide speed; geometry ahead stops it.
    private bool TravelIsBlocked(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        motor.Velocity = new Vector2(speed * direction, 0f);
        motor.MoveWithCollisions(deltaTime);
        if (speed > 0f && Mathf.Approximately(motor.Velocity.x, 0f)) {
            speed = 0f;
            return true;
        }
        return false;
    }

    // Ground under the body, at most a step below the ground slid on so far: deeper (a ledge) is a fall.
    private bool TryFollowGround() {
        Transform body = context.Rig.Body;
        Vector2 origin = (Vector2)body.position + Vector2.up * GroundProbeHeadroom;
        float depth = GroundProbeHeadroom + Mathf.Max(0f, body.position.y - groundY) + context.Rig.Stride.MaxStepDown;
        if (!FootholdProbe.TryFindGroundBelow((origin, depth), context.Rig.Stride.GroundAt(groundY), out Vector2 ground)) {
            return false;
        }
        groundY = ground.y;
        return true;
    }

    // The pelvis eases down into the slide and then rides the ground at the slide height.
    private void FollowGroundHeight(float deltaTime) {
        Transform body = context.Rig.Body;
        float targetY = groundY + settings.slide.pelvisHeight;
        float ease = FrameRateEase.FractionForFrame(settings.slide.groundFollowEase, deltaTime);
        body.position = new Vector3(body.position.x, Mathf.Lerp(body.position.y, targetY, ease), body.position.z);
    }

    // The standing hull on the ground under the body: what the crouch would stand up into.
    private bool HasRoomToStand() {
        Vector2 standingPivot = new Vector2(context.Rig.Body.position.x, groundY + context.Rig.Stride.BodyHeight);
        return context.Rig.Motor.HasHeadroom(standingPivot);
    }

    // The feet are put down where a gait at this speed has them, and the walk carries on from them (crouched while crouch
    // is held or a ceiling is too low to stand under). False if either foot finds no ground.
    private bool TryGetUp() {
        StrideController stride = context.Rig.Stride;
        bool leadIsFront = legs.lead == stride.FrontLeg;
        if (!stride.TrySettleLanding((context.Rig.Body.position.x, context.Rig.Facing.Sign, groundY), (speed * direction, leadIsFront, MovingStanceShare))) {
            return false;
        }
        context.States.Walk.Begin();
        return true;
    }

    // Straight out of the slide into the air, with no windup (the body is already low): the slide speed carries into the
    // launch as a running jump's ground speed does, and the rise makes up the height the slide is below a crouch, so the
    // jump peaks as high as one from a crouch.
    private void JumpOut() {
        StrideController stride = context.Rig.Stride;
        KinematicBodyMotor motor = context.Rig.Motor;
        float crouchPivotY = groundY + stride.BodyHeight - stride.CrouchDrop;
        float fullHeight = JumpMomentum.FullHeight(settings.jump, stride.CrouchShare) + Mathf.Max(0f, crouchPivotY - context.Rig.Body.position.y);
        Vector2 launch = new Vector2(
            JumpMomentum.LaunchSpeedX((speed * direction, stride.Run.speed), settings.jump),
            JumpMath.LaunchSpeedForHeight(motor.Gravity, fullHeight));
        context.Rig.Events.RaiseJumped(launch);
        context.States.Airborne.Launch(launch, (fullHeight, 0f, true));
    }

    // ---- Pose -------------------------------------------------------------------------------------------------

    private void Pose(float deltaTime) {
        SlideSettings slide = settings.slide;
        context.Rig.AirPose = new AirPoseFrame(new AirPose { lean = -slide.leanBack }, (0f, 0f));

        float ease = FrameRateEase.FractionForFrame(slide.limbPoseEase, deltaTime);
        legs.lead.SwingFreelyToward(FootOnGround((legs.lead, slide.leadFoot)), ease);
        legs.trail.SwingFreelyToward(FootOnGround((legs.trail, slide.trailFoot)), ease);
        if (!isAiming) {
            PoseArms();
            context.Rig.Head.HoldLevel();
        }
    }

    // A foot at its offset from the pelvis along the slide, never below the ground nor further than the leg reaches.
    private Vector2 FootOnGround((LegStepper leg, Vector2 offset) foot) {
        Vector2 wanted = new Vector2(context.Rig.Body.position.x + foot.offset.x * direction, groundY + foot.offset.y);
        Vector2 hip = foot.leg.HipPosition;
        Vector2 reached = hip + Vector2.ClampMagnitude(wanted - hip, foot.leg.LegLength * MaxLimbExtension);
        return new Vector2(reached.x, Mathf.Max(reached.y, groundY));
    }

    // The back hand trails behind near the ground; the front hand is held forward for balance.
    private void PoseArms() {
        SlideSettings slide = settings.slide;
        ArmLimb back = context.Rig.BackArm;
        Vector2 trailing = new Vector2(back.ShoulderPosition.x + slide.trailingHand.x * direction, groundY + slide.trailingHand.y);
        back.ReachToward(back.ShoulderPosition + Vector2.ClampMagnitude(trailing - back.ShoulderPosition, back.Reach * MaxLimbExtension));

        ArmLimb front = context.Rig.FrontArm;
        Vector2 forward = new Vector2(slide.balanceHand.x * direction, slide.balanceHand.y);
        front.ReachToward(front.ShoulderPosition + Vector2.ClampMagnitude(forward, front.Reach * MaxLimbExtension));
    }
}
