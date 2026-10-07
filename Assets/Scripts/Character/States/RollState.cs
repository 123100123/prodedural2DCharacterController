using UnityEngine;

// Dive roll: dive forward with both arms reaching ahead, plant the hands on the ground, roll forward over the shoulders,
// then bring the feet back to the ground and hand over to walking. Each phase eases from where the previous one left off.
// With the run intent along the roll, the recovery keeps the roll's travel speed and hands over to the run part way
// through (RollRunExitSettings), so the run starts from the roll's speed rather than from the slow recovery. On a slope
// the roll travels along the surface and keeps the body clear of the slope line, not just of the ground under the pivot.
public sealed class RollState : ICharacterState {
    private enum RollPhase { Dive, HandContact, Roll, Recover }

    private const float FullTurn = 360f;
    private const float GroundProbeHeadroom = 0.5f;
    private const float FootholdSearchStep = 0.05f;

    private readonly StateContext context;
    private readonly RollSettings settings;
    private readonly RollRunExitSettings runExit;

    private RollPhase phase;
    private float phaseElapsed;
    private float readyTime;
    private int direction;
    private float speed;
    private float groundY;
    // The slope of the ground under the body (rise per unit of x; 0 on flat ground).
    private float gradient;
    private float pitch;
    private float chestCurl;
    private (float pitch, float height) phaseStart;
    private Vector2 handContactPoint;
    private (Quaternion body, Quaternion chest) uprightPose;
    private (Vector2 leadExtended, Vector2 trailExtended, Vector2 leadTucked, Vector2 trailTucked, Vector2 hands) bodySpacePose;
    private (LegStepper lead, LegStepper trail) legs;
    private (Vector2 lead, Vector2 trail) recoveryFootholds;
    // The run intent points along the roll this frame (only acted on in the recovery).
    private bool runsAlong;

    public RollState(StateContext context, (RollSettings roll, RollRunExitSettings runExit) settings) {
        this.context = context;
        this.settings = settings.roll;
        runExit = settings.runExit;
    }

    public string Name => "Roll";
    public bool AllowsUpperBodyActions => false;
    public bool AllowsCrouch => false;

    // The dive, the hand contact and the roll over are committed; the recovery is where the run intent can take over.
    public RunReadiness RunReadinessFor(PlayerCommands commands) {
        return RunReadiness.BlockedBy(phase == RollPhase.Recover ? RunBlock.RollRecovering : RunBlock.RollCommitted);
    }

    public bool CanStart => settings.enabled && Time.time >= readyTime;

    public void Begin() => context.Machine.ChangeTo(this);

    public void Enter() {
        direction = context.Rig.Facing.Sign;
        uprightPose = (context.Rig.Body.localRotation, context.Rig.Chest.localRotation);
        bodySpacePose = LimbPoseInBodySpace();
        legs = context.Rig.Stride.LegsOrderedByFacing(direction);
        groundY = context.Rig.Stride.GroundUnderBody;
        gradient = context.Rig.Stride.SlopeGradient;
        (pitch, chestCurl, speed) = (0f, 0f, settings.diveSpeed);
        context.Rig.Motor.Velocity = Vector2.zero;
        StartPhase(RollPhase.Dive);
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        phaseElapsed += deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / PhaseDuration());
        runsAlong = RunIntent.Along((commands.RunIntent, commands.MoveAxis), direction);

        TravelForward(deltaTime);
        if (!TryFindGroundUnderBody()) {
            context.States.Airborne.Fall(context.Rig.Motor.Velocity);
            return;
        }

        PitchAndLowerBody((progress, deltaTime));
        CurlChest(deltaTime);
        PoseLimbs((progress, deltaTime));
        context.Rig.Stride.PlaceStrideTargetUnderBody();

        if (phase == RollPhase.Recover && runExit.HandsOver((runsAlong, progress)) && TryRunOut()) {
            return;
        }
        if (IsPhaseComplete(progress)) {
            AdvancePhase();
        }
    }

    public void Exit() {
        context.Rig.Body.localRotation = uprightPose.body;
        context.Rig.Chest.localRotation = uprightPose.chest;
        readyTime = Time.time + settings.cooldown;
    }

    // ---- Phase flow -------------------------------------------------------------------------------------------

    private void StartPhase(RollPhase next) {
        phase = next;
        phaseElapsed = 0f;
        phaseStart = (pitch, context.Rig.Body.position.y - groundY);
    }

    private void AdvancePhase() {
        switch (phase) {
            case RollPhase.Dive:
                context.Rig.Events.RaiseRolled((new Vector2(context.Rig.FrontArm.HandPosition.x, groundY), direction));
                StartPhase(RollPhase.HandContact);
                break;
            case RollPhase.HandContact:
                StartPhase(RollPhase.Roll);
                break;
            case RollPhase.Roll:
                recoveryFootholds = FindRecoveryFootholds();
                StartPhase(RollPhase.Recover);
                break;
            default:
                StandUp();
                break;
        }
    }

    private bool IsPhaseComplete(float progress) {
        return phase == RollPhase.HandContact ? progress >= 1f || IsFrontHandOnGround() : progress >= 1f;
    }

    private float PhaseDuration() {
        switch (phase) {
            case RollPhase.Dive: return settings.diveDuration;
            case RollPhase.HandContact: return settings.maxContactTime;
            case RollPhase.Roll: return settings.rollDuration;
            default: return settings.recoverDuration;
        }
    }

    private float PhaseSpeed() {
        switch (phase) {
            case RollPhase.Dive: return settings.diveSpeed;
            case RollPhase.HandContact: return settings.contactSpeed;
            case RollPhase.Roll: return settings.rollSpeed;
            default: return runExit.KeepsTravelSpeed(runsAlong) ? settings.rollSpeed : settings.recoverSpeed;
        }
    }

    private float PhasePitch() {
        switch (phase) {
            case RollPhase.Dive: return settings.diveLean;
            case RollPhase.HandContact: return settings.contactLean;
            default: return FullTurn;
        }
    }

    // The roll phase has no height of its own: the body keeps its lowest point on the ground instead.
    private float PhaseBodyHeight() {
        switch (phase) {
            case RollPhase.Dive: return settings.diveBodyHeight;
            case RollPhase.HandContact: return settings.contactBodyHeight;
            default: return context.Rig.Stride.BodyHeight - settings.recoverCrouch;
        }
    }

    // ---- Body motion -----------------------------------------------------------------------------------------

    // Momentum carries over between phases; only the target speed changes.
    private void TravelForward(float deltaTime) {
        KinematicBodyMotor motor = context.Rig.Motor;
        speed = Mathf.Lerp(speed, PhaseSpeed(), FrameRateEase.FractionForFrame(settings.speedEase, deltaTime));
        motor.Velocity = speed * SlopeMath.AlongSurface(gradient, direction);
        motor.MoveWithCollisions(deltaTime, context.Rig.Stride.Slope.maxSlopeAngle);
    }

    // No ground within reach under the body means the roll left a ledge.
    private bool TryFindGroundUnderBody() {
        Transform body = context.Rig.Body;
        Vector2 origin = (Vector2)body.position + Vector2.up * GroundProbeHeadroom;
        float depth = GroundProbeHeadroom + (body.position.y - groundY) + settings.maxGroundDrop;

        if (!FootholdProbe.TryFindGroundBelow((origin, depth), context.Rig.Stride.GroundAt(groundY), out Vector2 ground, out Vector2 normal)) {
            return false;
        }
        groundY = ground.y;
        gradient = SlopeMath.IsSloped(normal, context.Rig.Stride.Slope.maxSlopeAngle) ? SlopeMath.Gradient(normal) : 0f;
        return true;
    }

    // The ground's height at x, along the slope under the body.
    private float GroundLineAt(float x) => groundY + gradient * (x - context.Rig.Body.position.x);

    // Pitch and height move from where the phase started toward the phase's targets, so phases blend without snapping.
    // Rotations are in the rig's authored space; mirroring the root makes them a forward roll for either facing.
    private void PitchAndLowerBody((float progress, float deltaTime) frame) {
        float blend = Mathf.SmoothStep(0f, 1f, frame.progress);
        pitch = Mathf.Lerp(phaseStart.pitch, PhasePitch(), blend);
        float height = Mathf.Lerp(phaseStart.height, PhaseBodyHeight(), blend);

        Transform body = context.Rig.Body;
        body.localRotation = Quaternion.Euler(0f, 0f, pitch * ForwardTurnSign) * uprightPose.body;

        float contactY = LowestClearBodyY();
        bool isRolling = phase == RollPhase.Roll;
        float targetY = isRolling ? contactY : groundY + height;
        float ease = FrameRateEase.FractionForFrame(isRolling ? settings.rollContactEase : settings.groundFollowEase, frame.deltaTime);
        float easedY = Mathf.Lerp(body.position.y, targetY, ease);
        body.position = new Vector3(body.position.x, Mathf.Max(easedY, contactY), body.position.z);
    }

    // The body rolls over its head, shoulders, back and hips: the pivot rises as far as needed to keep them on top of the
    // ground (on a slope, of the ground under each of them).
    private float LowestClearBodyY() {
        float deepestBelowPivot = Mathf.Max(
            Mathf.Max(DepthBelowPivot(context.Rig.Head.CrownPosition), DepthBelowPivot(context.Rig.Chest.position)),
            Mathf.Max(DepthBelowPivot(context.Rig.FrontArm.ShoulderPosition), DepthBelowPivot(context.Rig.BackArm.ShoulderPosition)));
        float hipsClearance = settings.hipRadius - settings.groundClearance;
        return groundY + Mathf.Max(deepestBelowPivot, hipsClearance) + settings.groundClearance;
    }

    // How far below the pivot a body point is, measured against the slope line: a point over higher ground counts as deeper.
    private float DepthBelowPivot(Vector2 point) {
        Vector2 pivot = context.Rig.Body.position;
        return pivot.y - point.y + gradient * (point.x - pivot.x);
    }

    private void CurlChest(float deltaTime) {
        float ease = FrameRateEase.FractionForFrame(settings.limbPoseEase, deltaTime);
        chestCurl = Mathf.Lerp(chestCurl, ChestCurlTarget(), ease);
        float angle = settings.chestCurlAngle * chestCurl * ForwardTurnSign;
        context.Rig.Chest.localRotation = Quaternion.Euler(0f, 0f, angle) * uprightPose.chest;
    }

    // The chest curls while the body is inverted and uncurls before standing.
    private float ChestCurlTarget() => IsInverted ? 1f : 0f;

    private bool IsInverted => pitch >= settings.invertedPitchWindow.x && pitch < settings.invertedPitchWindow.y;

    private float ForwardTurnSign => -context.Rig.Facing.AuthoredSign;

    // ---- Limbs -----------------------------------------------------------------------------------------------

    private void PoseLimbs((float progress, float deltaTime) frame) {
        float ease = FrameRateEase.FractionForFrame(settings.limbPoseEase, frame.deltaTime);
        switch (phase) {
            case RollPhase.Dive:
                ReachArmsAhead();
                PushOffAndExtendLegs((frame.progress, ease));
                break;
            case RollPhase.HandContact:
                ReachHandsToGround();
                SwingLegsInBodySpace((bodySpacePose.leadExtended, bodySpacePose.trailExtended), ease);
                break;
            case RollPhase.Roll:
                HoldOrTuckHands(frame.progress);
                SwingLegsInBodySpace((bodySpacePose.leadTucked, bodySpacePose.trailTucked), ease);
                TuckChinWhileInverted();
                break;
            default:
                legs.lead.SwingFreelyToward(recoveryFootholds.lead, ease);
                legs.trail.SwingFreelyToward(recoveryFootholds.trail, ease);
                break;
        }
    }

    // Open-armed dive: both arms stretch ahead of the shoulders, slightly toward the ground.
    private void ReachArmsAhead() {
        float angle = settings.diveArmAngle * Mathf.Deg2Rad;
        Vector2 ahead = new Vector2(Mathf.Cos(angle) * direction, -Mathf.Sin(angle));
        ArmLimb front = context.Rig.FrontArm;
        ArmLimb back = context.Rig.BackArm;
        front.ReachToward(front.ShoulderPosition + ahead * (front.Reach * settings.diveArmExtension));
        back.ReachToward(back.ShoulderPosition + ahead * (back.Reach * settings.diveArmExtension));
    }

    // The lead foot keeps pushing against the ground for the first part of the dive; the trail leg trails behind.
    private void PushOffAndExtendLegs((float progress, float ease) push) {
        Transform body = context.Rig.Body;
        if (push.progress >= settings.pushOffShare) {
            legs.lead.SwingFreelyToward(body.TransformPoint(bodySpacePose.leadExtended), push.ease);
        }
        legs.trail.SwingFreelyToward(body.TransformPoint(bodySpacePose.trailExtended), push.ease);
    }

    private void ReachHandsToGround() {
        handContactPoint = HandGroundTarget();
        context.Rig.FrontArm.ReachToward(handContactPoint);
        context.Rig.BackArm.ReachToward(BackHandPoint());
    }

    // Ground under a point ahead of the shoulders, found procedurally rather than assumed.
    private Vector2 HandGroundTarget() {
        Vector2 shoulder = context.Rig.FrontArm.ShoulderPosition;
        Vector2 origin = new Vector2(shoulder.x + direction * settings.handReachAhead, shoulder.y);
        float depth = shoulder.y - groundY + settings.maxGroundDrop;

        bool hasGround = FootholdProbe.TryFindGroundBelow((origin, depth), context.Rig.Stride.GroundAt(groundY), out Vector2 ground);
        return hasGround ? ground : new Vector2(origin.x, groundY);
    }

    private bool IsFrontHandOnGround() {
        return context.Rig.FrontArm.HandPosition.y - handContactPoint.y <= settings.contactTolerance;
    }

    // Hands stay planted where they touched while the body rolls over them, then hug the tucked legs.
    private void HoldOrTuckHands(float progress) {
        if (progress < settings.handPinShare) {
            context.Rig.FrontArm.ReachToward(handContactPoint);
            context.Rig.BackArm.ReachToward(BackHandPoint());
            return;
        }

        Vector2 hands = context.Rig.Body.TransformPoint(bodySpacePose.hands);
        context.Rig.FrontArm.ReachToward(hands);
        context.Rig.BackArm.ReachToward(hands);
    }

    private void TuckChinWhileInverted() {
        if (IsInverted) {
            context.Rig.Head.TuckChin(settings.chinTuckAngle);
        }
    }

    private Vector2 BackHandPoint() => handContactPoint + Vector2.right * (direction * settings.backHandSpacing);

    private void SwingLegsInBodySpace((Vector2 lead, Vector2 trail) pose, float ease) {
        Transform body = context.Rig.Body;
        legs.lead.SwingFreelyToward(KeepAboveGround(body.TransformPoint(pose.lead)), ease);
        legs.trail.SwingFreelyToward(KeepAboveGround(body.TransformPoint(pose.trail)), ease);
    }

    private Vector2 KeepAboveGround(Vector2 point) => new Vector2(point.x, Mathf.Max(point.y, GroundLineAt(point.x)));

    // Limb offsets are authored relative to the upright body, then carried in body space so they turn with it.
    private (Vector2 leadExtended, Vector2 trailExtended, Vector2 leadTucked, Vector2 trailTucked, Vector2 hands) LimbPoseInBodySpace() {
        return (
            BodySpacePoint(settings.leadFootExtended),
            BodySpacePoint(settings.trailFootExtended),
            BodySpacePoint(settings.leadFootTucked),
            BodySpacePoint(settings.trailFootTucked),
            BodySpacePoint(settings.handsTucked));
    }

    private Vector2 BodySpacePoint(Vector2 forwardOffset) {
        Transform body = context.Rig.Body;
        Vector3 world = body.position + new Vector3(forwardOffset.x * direction, forwardOffset.y, 0f);
        return body.InverseTransformPoint(world);
    }

    // ---- Recovery --------------------------------------------------------------------------------------------

    private (Vector2 lead, Vector2 trail) FindRecoveryFootholds() {
        float bodyX = context.Rig.Body.position.x;
        float halfWidth = context.Rig.Stride.StanceHalfWidth;
        return (SupportedFootholdToward((bodyX + direction * halfWidth, bodyX)), SupportedFootholdToward((bodyX - direction * halfWidth, bodyX)));
    }

    // Ground within a step of the roll's ground, sliding from the preferred spot toward the body when it is over a gap or edge.
    private Vector2 SupportedFootholdToward((float preferredX, float bodyX) search) {
        StrideController stride = context.Rig.Stride;
        float shiftSign = Mathf.Sign(search.bodyX - search.preferredX);
        int sampleCount = Mathf.CeilToInt(Mathf.Abs(search.bodyX - search.preferredX) / FootholdSearchStep);

        for (int sample = 0; sample <= sampleCount; sample++) {
            Vector2 origin = new Vector2(search.preferredX + shiftSign * sample * FootholdSearchStep, groundY + stride.MaxStepUp);
            if (FootholdProbe.TryFindGroundBelow((origin, stride.MaxStepUp + stride.MaxStepDown), stride.GroundAt(groundY), out Vector2 ground)) {
                return ground;
            }
        }
        return new Vector2(search.bodyX, groundY);
    }

    // Part way through the recovery, toward a run: the feet settle where the body is and the walk takes the roll's
    // velocity from the motor (written by the travel this frame), so the run ramps up from the roll's speed. False if the
    // feet find no ground: the recovery carries on and stands up (or falls) at its end as usual.
    private bool TryRunOut() {
        if (!context.Rig.Stride.TrySettleStance((context.Rig.Body.position.x, direction, groundY))) {
            return false;
        }
        context.States.Walk.Begin();
        return true;
    }

    // The body is left in a slight crouch; the walking gait rises out of it.
    private void StandUp() {
        if (context.Rig.Stride.TrySettleStance((context.Rig.Body.position.x, direction, groundY))) {
            context.States.Walk.Begin();
            return;
        }
        context.States.Airborne.Fall(context.Rig.Motor.Velocity);
    }
}
