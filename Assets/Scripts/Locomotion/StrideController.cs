using System.Collections.Generic;
using UnityEngine;

// Grounded gait: the body moves at the ground speed and the feet follow it. One gait clock (StepScheduler) times both
// legs half a cycle apart and also drives their swings, so steps alternate evenly at any cadence; each foot lands where
// the hip will be at touchdown plus half a stance ahead. Walk and run are blended profiles, so idle, walking and running
// flow into each other. The body rides the feet as a mass on springs and bobs and surges in step with the cycle.
public class StrideController : MonoBehaviour {
    private const float GroundContactTolerance = 0.15f;
    // Walls are looked for at least this far ahead, and as far as a full-speed running step lands.
    private const float MinWallLookAhead = 2.5f;
    // Long frames (hitches) are split into gait steps no longer than this, so the feet keep up with the body.
    private const float MaxGaitStep = 1f / 30f;
    // Feet settle into a stance once the body has come to rest above them (horizontal speed below this), so the stance
    // spots no longer move and one settling step per foot is enough.
    private const float SettledBodySpeed = 0.2f;
    // A gait swing may outlast its forecast by this share before its time ceiling finishes it (the clock stalled).
    private const float SwingCeilingSlack = 1.15f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [Tooltip("Debug marker showing where the gait is carrying the body.")]
    [SerializeField] private Transform strideTarget;

    // Standing height of the inverseRobo rig: its legs are 2.34 long and its hips 0.27 below the pivot, so the legs stand
    // at about 0.92 of their length, knees slightly bent.
    public const float DefaultBodyHeight = 2.4f;

    [Tooltip("Height of the body pivot above the feet when standing. Keep the standing legs a little short of straight " +
             "(about 0.9 of their length) so the knees never lock.")]
    [SerializeField] private float bodyHeight = DefaultBodyHeight;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("With no movement input, momentum carries the body at most this far past its feet, so stopping near a ledge settles there.")]
    [SerializeField] private float stoppingOverhang = 0.3f;
    [SerializeField] private IdleStanceSettings idle = IdleStanceSettings.Default;

    [Header("Gait")]
    [SerializeField] private GaitProfile walk = GaitProfile.Walk;
    [SerializeField] private GaitProfile run = GaitProfile.Run;
    [SerializeField] private GaitTransitionSettings transitions = GaitTransitionSettings.Default;
    [SerializeField] private BalanceSettings balance = BalanceSettings.Default;
    [SerializeField] private StepArcShape stepArc = new StepArcShape();

    [SerializeField] private BodyMassSettings body = BodyMassSettings.Default;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private readonly StepScheduler scheduler = new StepScheduler();
    private readonly GaitDiagnostics diagnostics = new GaitDiagnostics();
    private readonly SupportLegResolver supportResolver = new SupportLegResolver();
    private float supportCatchUp = 1f;
    private float lastBalanceStepTime = float.NegativeInfinity;
    // The intended speed each leg's step in the air was planned for.
    private (float front, float back) plannedIntent;
    private Vector2 bodyTarget;
    private (float support, float surge) pelvisOffsets;
    private bool lastStepFoundNoFoothold;
    private int lastGaitFrame = -10;
    private BodyCarriage carriage;
    private float groundSpeed;
    private float intendedSpeed;
    private float anchorX;
    private CharacterEvents events;
    private CharacterFacing facing;

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight { get => bodyHeight; set => bodyHeight = value; }
    // Far enough that open ground never shortens a step: a shorter look-ahead would land running feet under the hip.
    public float WallProbeDistance => Mathf.Max(MinWallLookAhead, GaitTiming.LandingReach(run.speed, run));
    public float StanceHalfWidth => idle.stanceHalfWidth;
    public float MaxStepUp => footholdLimits.maxStepUp;
    public float MaxStepDown => footholdLimits.maxStepDown;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;
    public Vector2 BodyVelocity => Carriage.Velocity;

    // The blend of the walk and run profiles currently in use, by Pace.Running.
    public GaitProfile CurrentGait { get; private set; } = GaitProfile.Walk;
    // How far the gait is from standing toward walking and from walking toward running, from the ground speed alone.
    public LocomotionPace Pace { get; private set; } = LocomotionPace.Standing;
    // Signed ground speed the gait is carrying the body at (positive toward world right).
    public float GroundSpeed => groundSpeed;
    // Where the legs are in their shared cycle; the torso and arms move in step with it.
    public float GaitPhase => scheduler.Phase;
    // What the gait did recently (contacts, balance steps, resyncs), for inspection only.
    public GaitDiagnostics Diagnostics => diagnostics;

    // Tuning: each group of settings as a value; a new value takes effect on the next gait tick.
    public GaitProfile Walk { get => walk; set => walk = value; }
    public GaitProfile Run { get => run; set => run = value; }
    public GaitTransitionSettings Transitions { get => transitions; set => transitions = value; }
    public BalanceSettings Balance { get => balance; set => balance = value; }
    public BodyMassSettings BodyMass { get => body; set => body = value; }
    public IdleStanceSettings IdleStance { get => idle; set => idle = value; }
    // Debug override of the target speed (GaitPreview.Off in normal play).
    public GaitPreview Preview { get; set; } = GaitPreview.Off;

    // Inspection: what the gait is doing this frame.
    public float IntendedSpeed => intendedSpeed;
    public bool IsMoving => scheduler.IsMoving;
    public float CyclePeriod => GaitTiming.CyclePeriod(groundSpeed, CurrentGait);
    public float SwingShare => scheduler.SwingShare;
    public float StrideAnchorX => anchorX;
    public Vector2 BodyTarget => bodyTarget;
    // The leg carrying the body and how far through its stance the body is.
    public SupportStance Support => supportResolver.Current;
    // The pelvis target's horizontal shifts from the anchor: toward a stance centred over the support foot, and the
    // body's speed change over its support.
    public (float support, float surge) PelvisOffsets => pelvisOffsets;
    // Ground spots the last planned step chose its foothold from.
    public IReadOnlyList<Vector2> FootholdCandidates => footholdCandidates;
    // True while a grounded state ticks the gait this frame (false in the air, rolling or climbing).
    public bool IsDrivingGait => Time.frameCount - lastGaitFrame <= 1;

    // Held by a state (e.g. a jump wind-up) to lower the body.
    public float HeldCrouch { get; set; }

    // The body leans past both feet toward a spot where no foothold was found, e.g. walking off a ledge. On solid ground
    // balance steps keep the feet under the body, so leaning ahead alone is never a fall.
    public bool IsPastSupportEdge => lastStepFoundNoFoothold && SupportOverhang() > edgeOverhang;

    // Neither foot stands on ground any more (e.g. spawned in the air or the floor moved away).
    public bool HasLostGround => !IsFootOnGround(frontLeg) && !IsFootOnGround(backLeg);

    public void Connect((CharacterEvents events, CharacterFacing facing) character) {
        events = character.events;
        facing = character.facing;
    }

    private BodyCarriage Carriage => carriage ?? (carriage = new BodyCarriage(transform));

    // Hands the body to the gait with its current momentum, e.g. after a landing (a downward velocity becomes the squash).
    public void BeginGroundedMotion(Vector2 velocity) {
        Carriage.Restart(velocity);
        groundSpeed = Mathf.Clamp(velocity.x, -run.speed, run.speed);
        BlendGaitWithSpeed(float.PositiveInfinity);
        anchorX = transform.position.x;
    }

    // The feet were just placed in a stance (spawning, landing, after a roll or a climb): the gait starts afresh from it.
    public void RestartGaitFromStance() {
        scheduler.RestartFromStance();
        supportResolver.Reset();
    }

    // feetSpan bounds where feet may go (up to wall faces); bodySpan bounds the body pivot so its hull stays out of walls.
    public void TickWalk(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        lastGaitFrame = Time.frameCount;
        int gaitSteps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / MaxGaitStep));
        for (int step = 0; step < gaitSteps; step++) {
            TickGaitStep(deltaTime / gaitSteps, intent);
        }
    }

    private void TickGaitStep(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        AccelerateGround((intent.direction, intent.running, deltaTime));
        AdvanceAnchor((deltaTime, AnchorSpan(intent)));
        BlendGaitWithSpeed(deltaTime);
        AdvanceGaitClock(deltaTime);
        RecoverSupport();
        LiftFeetOnCue(intent.feetSpan);
        ReplanSwingsOnIntentChange(intent.feetSpan);
        SettleFeetWhenIdle();
        TickLegs(deltaTime);
        TrackSupport();
        CarryBodyOverFeet((deltaTime, intent.bodySpan));
        strideTarget.position = new Vector2(anchorX, StanceHeight);
    }

    // The leg whose foot is further along the facing direction leads.
    public (LegStepper lead, LegStepper trail) LegsOrderedByFacing(int facingSign) {
        bool frontLeads = frontLeg.FootTarget.x * facingSign >= backLeg.FootTarget.x * facingSign;
        return frontLeads ? (frontLeg, backLeg) : (backLeg, frontLeg);
    }

    public void PlaceStrideTargetUnderBody() => strideTarget.position = new Vector2(transform.position.x, StanceHeight);

    // Picks ground under both feet around centerX, within a step of groundY, and lets the legs settle onto it.
    public bool TrySettleStance((float centerX, int facingSign, float groundY) stance) {
        float frontX = stance.centerX + stance.facingSign * idle.stanceHalfWidth;
        float backX = stance.centerX - stance.facingSign * idle.stanceHalfWidth;
        if (!TryFindStanceGround((frontX, stance.groundY), out Vector2 frontGround)
            || !TryFindStanceGround((backX, stance.groundY), out Vector2 backGround)) {
            return false;
        }

        frontLeg.SettleOnto(frontGround);
        backLeg.SettleOnto(backGround);
        lastStepFoundNoFoothold = false;
        PlaceStrideTargetUnderBody();
        return true;
    }

    // Ground below the body within standing height plus extraDepth (e.g. this frame's fall distance).
    public bool TryFindGroundBelowBody(float extraDepth, out Vector2 ground) {
        Vector2 origin = transform.position;
        return FootholdProbe.TryFindGroundBelow((origin, bodyHeight + extraDepth), footholdLimits.groundMask, out ground);
    }

    private bool TryFindStanceGround((float x, float groundY) column, out Vector2 ground) {
        Vector2 origin = new Vector2(column.x, column.groundY + footholdLimits.maxStepUp);
        float depth = footholdLimits.maxStepUp + footholdLimits.maxStepDown;
        return FootholdProbe.TryFindGroundBelow((origin, depth), footholdLimits.groundMask, out ground);
    }

    private bool IsFootOnGround(LegStepper leg) {
        Vector2 origin = leg.FootTarget + Vector2.up * GroundContactTolerance;
        return FootholdProbe.TryFindGroundBelow((origin, 2f * GroundContactTolerance), footholdLimits.groundMask, out _);
    }

    // ---- Speed -----------------------------------------------------------------------------------------------

    // The run input only picks the target speed; the gait itself follows the speed the body reaches.
    private void AccelerateGround((float direction, bool running, float deltaTime) input) {
        intendedSpeed = Preview.IsActive
            ? Preview.IntendedSpeed((input.direction, facing?.Sign ?? 1, walk.speed, run.speed))
            : input.direction * (input.running ? run.speed : walk.speed);
        groundSpeed = GroundSpeedRamp.Step((groundSpeed, intendedSpeed, CurrentGait), input.deltaTime);
    }

    // The walk/run blend follows the ground speed, rate-limited so a sudden stop at a wall does not snap the gait.
    private void BlendGaitWithSpeed(float deltaTime) {
        float runningTarget = LocomotionPace.RunningAt(groundSpeed, (walk, run, transitions));
        float running = Mathf.MoveTowards(Pace.Running, runningTarget, transitions.runBlendRate * deltaTime);
        Pace = new LocomotionPace((LocomotionPace.MovingAt(groundSpeed, walk), running));
        CurrentGait = GaitProfile.Blend((walk, run), running);
    }

    // Walls always bound the body; without movement input, so does the stopping margin past the leading foot. Only the
    // side the body moves toward is bounded: a running body is often still behind feet landing ahead of it.
    private (float min, float max) AnchorSpan((float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        if (!Mathf.Approximately(intent.direction, 0f) || Mathf.Approximately(groundSpeed, 0f)) {
            return intent.bodySpan;
        }
        if (groundSpeed > 0f) {
            float supportMax = Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x) + stoppingOverhang;
            return (intent.bodySpan.min, Mathf.Min(intent.bodySpan.max, supportMax));
        }
        float supportMin = Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x) - stoppingOverhang;
        return (Mathf.Max(intent.bodySpan.min, supportMin), intent.bodySpan.max);
    }

    // The point the body is carried toward moves at the ground speed and stops at its span's ends.
    private void AdvanceAnchor((float deltaTime, (float min, float max) bodySpan) frame) {
        float advanced = anchorX + groundSpeed * frame.deltaTime;
        anchorX = Mathf.Clamp(advanced, frame.bodySpan.min, frame.bodySpan.max);
        if (!Mathf.Approximately(anchorX, advanced)) {
            groundSpeed = 0f;
        }
    }

    // ---- Steps -----------------------------------------------------------------------------------------------

    // The clock advances by distance over the stride, quickened while the feet catch up. From a stance the trailing
    // foot steps first.
    private void AdvanceGaitClock(float deltaTime) {
        bool frontLegTrails = LegsOrderedByFacing((int)Mathf.Sign(groundSpeed)).trail == frontLeg;
        float strideLength = GaitTiming.StrideLength(groundSpeed, CurrentGait) / supportCatchUp;
        float swingShare = SwingTiming.CycleShareAt(SwingTimingSpeed(), CurrentGait);
        scheduler.Advance((groundSpeed, strideLength, swingShare, deltaTime, frontLegTrails), transitions);
    }

    // Speeding up, swings are timed for at least part of the intended speed; slowing down, for the current speed.
    private float SwingTimingSpeed() {
        bool isSpeedingUp = intendedSpeed * groundSpeed > 0f && Mathf.Abs(intendedSpeed) > Mathf.Abs(groundSpeed);
        float speed = Mathf.Abs(groundSpeed);
        return isSpeedingUp ? Mathf.Max(speed, transitions.settingOffShare * Mathf.Abs(intendedSpeed)) : speed;
    }

    // A planted foot trailing the hip further than a normal stance ends quickens the cadence; one far behind takes an
    // emergency balance step out of turn.
    private void RecoverSupport() {
        if (!IsMoving) {
            supportCatchUp = 1f;
            diagnostics.RecordCatchUp(supportCatchUp);
            return;
        }

        (float excess, LegStepper leg) trailing = FurthestTrailingPlantedFoot();
        SupportRecovery.Response response = SupportRecovery.Assess((trailing.excess, Time.time - lastBalanceStepTime), balance);
        supportCatchUp = response.CatchUp;
        diagnostics.RecordCatchUp(supportCatchUp);

        bool isFrontLeg = trailing.leg == frontLeg;
        if (response.NeedsBalanceStep && !scheduler.IsCued(isFrontLeg)) {
            scheduler.CueOutOfTurn(isFrontLeg);
            lastBalanceStepTime = Time.time;
            diagnostics.RecordBalanceStep(Time.time);
        }
    }

    // How much further behind the hip than a normal stance ends the furthest planted foot is (negative: within it).
    private (float excess, LegStepper leg) FurthestTrailingPlantedFoot() {
        float normalLag = GaitTiming.LandingLead(groundSpeed, CurrentGait);
        float frontExcess = TrailingLag(frontLeg) - normalLag;
        float backExcess = TrailingLag(backLeg) - normalLag;
        return frontExcess >= backExcess ? (frontExcess, frontLeg) : (backExcess, backLeg);
    }

    private float TrailingLag(LegStepper leg) {
        return leg.IsGrounded ? (transform.position.x - leg.FootTarget.x) * Mathf.Sign(groundSpeed) : float.NegativeInfinity;
    }

    // A cued leg lifts as soon as it is planted and the other foot lets it; until then the cue stays pending.
    private void LiftFeetOnCue((float min, float max) feetSpan) {
        if (!IsMoving) {
            return;
        }
        TryLiftOnCue((frontLeg, backLeg), feetSpan);
        TryLiftOnCue((backLeg, frontLeg), feetSpan);
    }

    // A cue with no foothold ahead is used up, so at a ledge the body moves on past the planted feet and falls.
    private void TryLiftOnCue((LegStepper stepping, LegStepper other) legs, (float min, float max) feetSpan) {
        bool isFrontLeg = legs.stepping == frontLeg;
        if (!scheduler.MayLiftNow(isFrontLeg, (legs.stepping.IsGrounded, legs.other.LetsOtherLegLift))) {
            return;
        }
        scheduler.ConsumeCue(isFrontLeg);

        float direction = Mathf.Sign(groundSpeed);
        (float landingHipX, float desiredX, float duration) landing = ForecastLanding((direction, 0f, legs.other.FootTarget.x, feetSpan));
        SwingTiming timing = GaitSwingTiming(landing.duration);
        lastStepFoundNoFoothold = !TryStepToward((legs.stepping, legs.other, landing.desiredX, direction), (landing.landingHipX, timing, CurrentGait.liftHeight, Pace.Running));
        if (lastStepFoundNoFoothold) {
            diagnostics.RecordFootholdFailure(Time.time);
            return;
        }
        RememberPlannedIntent(isFrontLeg);
        Carriage.Push(CurrentGait.pushOffImpulse * Pace.Moving);
    }

    // When the intended speed changes mid-stride (stopping, turning, speeding up), a gait step still in the air is
    // re-planned for the new motion, so a running foot does not land where a running hip would have been.
    private void ReplanSwingsOnIntentChange((float min, float max) feetSpan) {
        ReplanSwing((frontLeg, backLeg), feetSpan);
        ReplanSwing((backLeg, frontLeg), feetSpan);
    }

    private void ReplanSwing((LegStepper stepping, LegStepper other) legs, (float min, float max) feetSpan) {
        bool isFrontLeg = legs.stepping == frontLeg;
        float planned = isFrontLeg ? plannedIntent.front : plannedIntent.back;
        if (!legs.stepping.IsSwingingOnClock || Mathf.Approximately(planned, intendedSpeed) || Mathf.Approximately(groundSpeed, 0f)) {
            return;
        }
        RememberPlannedIntent(isFrontLeg);

        float progress = legs.stepping.StepProgress;
        (float landingHipX, float desiredX, float duration) landing = ForecastLanding((Mathf.Sign(groundSpeed), progress, legs.other.FootTarget.x, feetSpan));
        if (!TryPlanFoothold((legs.stepping, legs.other, landing.desiredX), landing.landingHipX, out Vector2 foothold)) {
            return;
        }
        // The ceiling is a whole-swing duration: the remaining share of it must cover the forecast rest of the swing.
        float longestDuration = SwingCeilingSlack * landing.duration / Mathf.Max(0.05f, 1f - progress);
        SwingTiming timing = SwingTiming.OnClock((longestDuration, legs.stepping.SwingTiming.ReleaseProgress));
        legs.stepping.RetargetStep((foothold, timing), stepArc);
    }

    private void RememberPlannedIntent(bool frontLeg) {
        plannedIntent = frontLeg ? (intendedSpeed, plannedIntent.back) : (plannedIntent.front, intendedSpeed);
    }

    private bool IsStopping => Mathf.Approximately(intendedSpeed, 0f);

    // Where the hip will be when the rest of the swing ends, where the foot should land and how long that takes. Moving
    // on, the foot lands half a stance ahead of the hip; stopping, on a stance spot where the hip stops.
    private (float landingHipX, float desiredX, float duration) ForecastLanding((float direction, float startProgress, float otherFootX, (float min, float max) feetSpan) step) {
        // A stopping clock would stretch the last step into slow motion: it is held to a natural swing at the current speed.
        float longestDuration = IsStopping ? GaitTiming.SwingDuration(groundSpeed, CurrentGait) : CurrentGait.maxSwingDuration;
        SwingForecast.Request request = new SwingForecast.Request((groundSpeed, intendedSpeed, scheduler.SwingShare, longestDuration, step.startProgress), (walk, run, transitions));
        SwingForecast.Prediction swing = SwingForecast.Predict(request);
        float landingHipX = transform.position.x + swing.Travel + LagRecoveredBy(swing.LandingSpeed);
        float landingX = IsStopping
            ? StoppingSpot((landingHipX, step.direction, step.otherFootX))
            : landingHipX + step.direction * GaitTiming.LandingLead(swing.LandingSpeed, SwingForecast.GaitAt(swing.LandingSpeed, (walk, run, transitions)));
        return (landingHipX, Mathf.Clamp(landingX, step.feetSpan.min, step.feetSpan.max), swing.Duration);
    }

    // Of the two stance spots around where the hip stops, the one away from the other foot: the stopping step lands in
    // the stance rather than beside a foot that already took the other spot.
    private float StoppingSpot((float stopHipX, float direction, float otherFootX) stop) {
        float center = stop.stopHipX + stop.direction * idle.stanceLead;
        float frontSpot = center + stop.direction * idle.stanceHalfWidth;
        float backSpot = center - stop.direction * idle.stanceHalfWidth;
        return Mathf.Abs(stop.otherFootX - frontSpot) >= Mathf.Abs(stop.otherFootX - backSpot) ? frontSpot : backSpot;
    }

    // The body trails its anchor by a spring lag that shrinks as it slows: slowing to the landing speed, it catches up by
    // the difference (all of it when stopping). Speeding up gains nothing, the lag only grows.
    private float LagRecoveredBy(float landingSpeed) {
        float lagNow = (anchorX - transform.position.x) * Mathf.Sign(groundSpeed);
        float lagAtLanding = body.horizontalSpring.TrackingLag(Mathf.Abs(landingSpeed));
        return Mathf.Sign(groundSpeed) * Mathf.Max(0f, lagNow - lagAtLanding);
    }

    // The swing runs on the gait clock, finishing within a little more than its forecast duration if the clock stalls;
    // the other foot waits for it to be far enough along: late when walking, mid-swing when running.
    private SwingTiming GaitSwingTiming(float forecastDuration) {
        float releaseProgress = Mathf.Lerp(transitions.walkLiftOverlap, transitions.runLiftOverlap, Pace.Running);
        return SwingTiming.OnClock((SwingCeilingSlack * forecastDuration, releaseProgress));
    }

    // Standing still, each foot steps toward its spot under the torso, the furthest first, finishing in a stance.
    private void SettleFeetWhenIdle() {
        bool isIdle = !IsMoving && frontLeg.IsGrounded && backLeg.IsGrounded && Mathf.Abs(Carriage.Velocity.x) < SettledBodySpeed;
        if (!isIdle) {
            return;
        }

        float centerX = StanceCenterX();
        bool frontIsLeft = frontLeg.FootTarget.x <= backLeg.FootTarget.x;
        float frontSpot = centerX + (frontIsLeft ? -idle.stanceHalfWidth : idle.stanceHalfWidth);
        float backSpot = centerX + (frontIsLeft ? idle.stanceHalfWidth : -idle.stanceHalfWidth);
        float frontError = Mathf.Abs(frontLeg.FootTarget.x - frontSpot);
        float backError = Mathf.Abs(backLeg.FootTarget.x - backSpot);
        if (Mathf.Max(frontError, backError) <= idle.footTolerance) {
            return;
        }

        // With both feet off their spots, the leg that did not step last goes first, so the legs keep alternating.
        bool bothOff = Mathf.Min(frontError, backError) > idle.footTolerance;
        bool frontSteps = bothOff ? !scheduler.LastLiftWasFront : frontError >= backError;
        (LegStepper stepping, LegStepper planted, float spot) settle = frontSteps
            ? (frontLeg, backLeg, frontSpot)
            : (backLeg, frontLeg, backSpot);
        float towardSpot = Mathf.Sign(settle.spot - settle.stepping.FootTarget.x);
        TryStepToward((settle.stepping, settle.planted, settle.spot, towardSpot), (transform.position.x, SwingTiming.Timed(idle.settleStepDuration), idle.settleLiftHeight, 0f));
    }

    // The torso leans a little ahead of the pelvis, so the standing feet are centred slightly ahead of it.
    private float StanceCenterX() {
        int facingSign = facing?.Sign ?? 0;
        return transform.position.x + facingSign * idle.stanceLead;
    }

    // A step must carry the foot at least a minimum step along its direction; otherwise (e.g. at a ledge whose
    // nearest ground is under the foot already) no step is taken, rather than stepping on the spot forever.
    private bool TryStepToward(
        (LegStepper stepping, LegStepper planted, float desiredX, float direction) step,
        (float landingHipX, SwingTiming timing, float liftHeight, float runShare) motion) {
        if (!TryPlanFoothold((step.stepping, step.planted, step.desiredX), motion.landingHipX, out Vector2 foothold)) {
            return false;
        }
        if ((foothold.x - step.stepping.FootTarget.x) * step.direction < footholdLimits.minStepLength) {
            return false;
        }
        step.stepping.BeginStep((foothold, motion.timing), (motion.liftHeight, motion.runShare));
        if (scheduler.RecordLift(step.stepping == frontLeg)) {
            diagnostics.RecordPhaseResync(Time.time);
        }
        return true;
    }

    private bool TryPlanFoothold((LegStepper stepping, LegStepper planted, float desiredX) step, float landingHipX, out Vector2 foothold) {
        FootholdRequest request = new FootholdRequest {
            DesiredX = step.desiredX,
            LandingHipX = landingHipX,
            PlantedFoot = step.planted.FootTarget,
            HipHeight = step.stepping.HipPosition.y - StanceHeight,
            LegLength = step.stepping.LegLength,
            ForwardSign = Mathf.Sign(step.desiredX - transform.position.x),
        };

        FootholdProbe.CollectCandidates((request.DesiredX, request.PlantedFoot.y, step.stepping.FootTarget), footholdLimits, footholdCandidates);
        return FootholdPlanner.TryChooseFoothold(request, (footholdCandidates, footholdLimits), out foothold);
    }

    // Measured in the direction of motion only: a running body is often behind feet that are landing ahead of it,
    // which is not an edge. An idle body is held over its feet by the stopping margin, so it has no overhang.
    private float SupportOverhang() {
        if (!IsMoving) {
            return 0f;
        }
        float bodyPositionX = transform.position.x;
        float supportFront = groundSpeed > 0f
            ? Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x)
            : Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        return (bodyPositionX - supportFront) * Mathf.Sign(groundSpeed);
    }

    private void TickLegs(float deltaTime) {
        frontLeg.TickStep((deltaTime, scheduler.SwingProgressThisTick), stepArc);
        backLeg.TickStep((deltaTime, scheduler.SwingProgressThisTick), stepArc);
    }

    // ---- Body ------------------------------------------------------------------------------------------------

    private void CarryBodyOverFeet((float deltaTime, (float min, float max) bodySpan) frame) {
        if (Carriage.NeedsRestart) {
            BeginGroundedMotion(Vector2.zero);
        }

        AbsorbFootPlant(frontLeg);
        AbsorbFootPlant(backLeg);

        (float target, float ceiling) height = BodyHeightLimits();
        bodyTarget = new Vector2(BodyTargetX(), height.target);
        Carriage.Carry((bodyTarget, frame.deltaTime), (body.horizontalSpring, body.verticalSpring, frame.bodySpan, height.ceiling));
    }

    // The body takes a planted foot's impact as a downward push, like a knee bending under the weight.
    private void AbsorbFootPlant(LegStepper leg) {
        if (!leg.TryConsumePlant(out float impactSpeed)) {
            return;
        }
        Carriage.Push(-Mathf.Min(body.maxPlantImpulse, impactSpeed * CurrentGait.plantImpulse));
        diagnostics.RecordPlant(leg == frontLeg, Time.time);
        events?.RaiseFootPlanted((leg.FootTarget, impactSpeed));
    }

    // The moving anchor carries the body through the world; the support foot shifts the pelvis around it, so the pelvis
    // travels through the legs instead of on a rail above them (PelvisSupportMotion). Both shifts fade out as the
    // character slows to a stand.
    private float BodyTargetX() {
        SupportStance stance = supportResolver.Current;
        float support = PelvisSupportMotion.SupportOffset((stance, ExpectedPelvisX()), (body.supportCoupling, body.maxSupportOffset));
        float surge = PelvisSupportMotion.Surge(stance, CurrentGait.surge);
        pelvisOffsets = (support * Pace.Moving, surge * Pace.Moving);
        return anchorX + pelvisOffsets.support + pelvisOffsets.surge;
    }

    // Where the pelvis will be riding its anchor: it trails it by its spring's tracking lag. Centring the pelvis over its
    // support has to compare the foot with the pelvis, not with the target it chases.
    private float ExpectedPelvisX() => anchorX - Mathf.Sign(groundSpeed) * body.horizontalSpring.TrackingLag(Mathf.Abs(groundSpeed));

    private void TrackSupport() {
        float stanceTravel = 2f * GaitTiming.LandingLead(groundSpeed, CurrentGait);
        supportResolver.Track((frontLeg.IsGrounded, backLeg.IsGrounded), (frontLeg.FootTarget.x, backLeg.FootTarget.x), (anchorX, Mathf.Sign(groundSpeed), stanceTravel));
    }

    // The gait's height, never higher than the planted legs reach: the spring's target, and the ceiling it may not
    // overshoot. The reach is measured from where the body is: its springs trail the anchor a little at speed, and
    // measuring from the anchor would make a trailing foot seem further behind than it is. A foot so far away that
    // reaching it would sink the body more than the reach drop is about to step anyway, and does not hold it down.
    private (float target, float ceiling) BodyHeightLimits() {
        float gaitHeight = GaitHeight();
        float pelvisX = transform.position.x;
        float reachable = Mathf.Min(HighestPelvisOver((frontLeg, pelvisX)), HighestPelvisOver((backLeg, pelvisX)));
        float ceiling = Mathf.Max(reachable, gaitHeight - body.maxReachDrop);
        return (Mathf.Min(ceiling, gaitHeight), ceiling);
    }

    // Standing height over the feet with the step's bob, sunk lower the faster the gait moves and by any held crouch.
    // The bob is aimed ahead by the vertical spring's lag at the bob's rate, so the body's low point lands where the gait
    // puts it (just after contact walking, mid-stance running) rather than late.
    private float GaitHeight() {
        float bobRate = 2f / Mathf.Max(0.05f, CyclePeriod);
        float aimedPhase = scheduler.Phase + body.verticalSpring.PhaseLagCycles(bobRate) / 2f;
        float bob = GaitWaves.BodyBob((aimedPhase, LowestPhase)) * CurrentGait.bobAmplitude * Pace.Moving;
        return StanceHeight + bodyHeight + bob - CurrentGait.pelvisDrop * Pace.Moving - HeldCrouch;
    }

    private float LowestPhase => GaitWaves.LowestPhase((CurrentGait.dutyFactor, CurrentGait.lowAfterContact));

    private float HighestPelvisOver((LegStepper leg, float pelvisX) support) {
        if (!support.leg.IsGrounded) {
            return float.PositiveInfinity;
        }
        Vector2 hipOffset = support.leg.HipPosition - (Vector2)transform.position;
        float reach = support.leg.LegLength * body.legExtension;
        return GaitPose.HighestPelvisY((support.leg.FootTarget, support.pelvisX), (hipOffset, reach));
    }
}
