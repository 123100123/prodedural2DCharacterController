using System.Collections.Generic;
using UnityEngine;

// Grounded gait: the body moves at the ground speed and the feet follow it. One gait cycle times both legs half a cycle
// apart, so steps alternate evenly; each foot lands where the hip will be at touchdown plus half a stance ahead. Walk and
// run are blended profiles, so idle, walking and running flow into each other. The body rides the feet as a mass on
// springs and bobs and surges in step with the cycle.
public class StrideController : MonoBehaviour {
    private const float GroundContactTolerance = 0.15f;
    // Walls are looked for at least this far ahead, and as far as a full-speed running step lands.
    private const float MinWallLookAhead = 2.5f;
    // Long frames (hitches) are split into gait steps no longer than this, so the feet keep up with the body.
    private const float MaxGaitStep = 1f / 30f;
    // How far through its swing the other foot must be before the trailing foot may lift: late when walking
    // (a heel-off just before touchdown), mid-swing when running (both feet in the air for a moment).
    private const float WalkingLiftOverlap = 0.8f;
    private const float RunningLiftOverlap = 0.5f;
    // Steps planned while speeding up assume at least this share of the intended speed, as people do when setting off:
    // the first step is a little shorter than a full one rather than a shuffle.
    private const float SettingOffShare = 0.65f;
    // A planted foot trailing the hip by this much more than a normal stance takes a balance step at once.
    private const float BalanceStepMargin = 0.3f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [Tooltip("Debug marker showing where the gait is carrying the body.")]
    [SerializeField] private Transform strideTarget;

    [Tooltip("Height of the body pivot above the feet when standing, with the legs nearly straight.")]
    [SerializeField] private float bodyHeight = 2.58f;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("With no movement input, momentum carries the body at most this far past its feet, so stopping near a ledge settles there.")]
    [SerializeField] private float stoppingOverhang = 0.3f;
    [Tooltip("Half the distance between the feet when standing.")]
    [SerializeField] private float stanceHalfWidth = 0.22f;
    [Tooltip("How far ahead of the pelvis the standing feet are centred, so they stand under the torso rather than behind it.")]
    [SerializeField] private float stanceLead = 0.1f;

    [Header("Gait")]
    [SerializeField] private GaitProfile walk = GaitProfile.Walk;
    [SerializeField] private GaitProfile run = GaitProfile.Run;
    [Tooltip("How quickly the gait blends between walking and running (share per second).")]
    [SerializeField] private float runBlendRate = 2.5f;
    [Tooltip("Below this ground speed the character is idle and settles its feet into a stance.")]
    [SerializeField] private float idleSpeed = 0.2f;
    [Tooltip("An idle foot further than this from its stance spot takes a settling step.")]
    [SerializeField] private float idleFootTolerance = 0.18f;
    [SerializeField] private StepArcShape stepArc = new StepArcShape();

    [Header("Body mass")]
    [SerializeField] private SpringSettings horizontalSpring = new SpringSettings(3f, 0.7f, 1f);
    [SerializeField] private SpringSettings verticalSpring = new SpringSettings(3.2f, 0.45f, 0f);
    [Tooltip("Share of the leg length a supporting leg may extend to; the pelvis never rises higher than its planted legs reach.")]
    [Range(0.8f, 1f)] [SerializeField] private float legExtension = 0.99f;
    [SerializeField] private float maxPlantImpulse = 2.5f;
    [Tooltip("Most the leg-reach limit may lower the pelvis below its gait height; a foot further away is about to step anyway.")]
    [SerializeField] private float maxReachDrop = 0.35f;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private readonly GaitCycle cycle = new GaitCycle();
    private (bool front, bool back) pendingLift;
    private bool wasMoving;
    private bool lastStepFoundNoFoothold;
    private int lastGaitFrame = -10;
    private BodyCarriage carriage;
    private float groundSpeed;
    private float intendedSpeed;
    private float runShare;
    private float anchorX;
    private CharacterEvents events;
    private CharacterFacing facing;

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight => bodyHeight;
    // Far enough that open ground never shortens a step: a shorter look-ahead would land running feet under the hip.
    public float WallProbeDistance => Mathf.Max(MinWallLookAhead, GaitTiming.LandingReach(run.speed, run));
    public float StanceHalfWidth => stanceHalfWidth;
    public float MaxStepUp => footholdLimits.maxStepUp;
    public float MaxStepDown => footholdLimits.maxStepDown;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;
    public Vector2 BodyVelocity => Carriage.Velocity;

    // The blend of the walk and run profiles currently in use.
    public GaitProfile CurrentGait { get; private set; } = GaitProfile.Walk;
    // Ground speed as a share of the current gait's full speed: 0 idle, 1 at full walk or run speed.
    public float SpeedShare => Mathf.Clamp01(Mathf.Abs(groundSpeed) / Mathf.Max(0.01f, CurrentGait.speed));
    // Signed ground speed the gait is carrying the body at (positive toward world right).
    public float GroundSpeed => groundSpeed;
    // Where the legs are in their shared cycle; the torso and arms move in step with it.
    public float GaitPhase => cycle.Phase;
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
        anchorX = transform.position.x;
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
        BlendGait((intent.running, deltaTime));
        AccelerateGround((intent.direction, deltaTime));
        AdvanceAnchor((deltaTime, AnchorSpan(intent)));
        AdvanceGaitCycle(deltaTime);
        CueBalanceSteps();
        LiftFeetOnCue(intent.feetSpan);
        SettleFeetWhenIdle();
        TickLegs(deltaTime);
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
        float frontX = stance.centerX + stance.facingSign * stanceHalfWidth;
        float backX = stance.centerX - stance.facingSign * stanceHalfWidth;
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

    private void BlendGait((bool running, float deltaTime) input) {
        runShare = Mathf.MoveTowards(runShare, input.running ? 1f : 0f, runBlendRate * input.deltaTime);
        CurrentGait = GaitProfile.Blend((walk, run), runShare);
    }

    // Speeds up gradually and brakes harder than it accelerates, so starts and stops carry momentum.
    private void AccelerateGround((float direction, float deltaTime) input) {
        float targetSpeed = input.direction * CurrentGait.speed;
        intendedSpeed = targetSpeed;
        bool isSpeedingUp = Mathf.Abs(targetSpeed) > Mathf.Abs(groundSpeed) && targetSpeed * groundSpeed >= 0f;
        float rate = isSpeedingUp ? CurrentGait.acceleration : CurrentGait.deceleration;
        groundSpeed = Mathf.MoveTowards(groundSpeed, targetSpeed, rate * input.deltaTime);
    }

    // Walls always bound the body; without movement input, so does the stopping margin past the feet.
    private (float min, float max) AnchorSpan((float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        if (!Mathf.Approximately(intent.direction, 0f)) {
            return intent.bodySpan;
        }
        float supportMin = Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x) - stoppingOverhang;
        float supportMax = Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x) + stoppingOverhang;
        return (Mathf.Max(intent.bodySpan.min, supportMin), Mathf.Min(intent.bodySpan.max, supportMax));
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

    private bool IsMoving => Mathf.Abs(groundSpeed) >= idleSpeed;

    // The cycle advances by distance over the stride. When motion starts it is primed so the trailing foot steps first.
    private void AdvanceGaitCycle(float deltaTime) {
        if (!IsMoving) {
            wasMoving = false;
            return;
        }
        if (!wasMoving) {
            (LegStepper lead, LegStepper trail) legs = LegsOrderedByFacing((int)Mathf.Sign(groundSpeed));
            cycle.PrimeToLift(legs.trail == frontLeg);
            wasMoving = true;
        }

        float strides = Mathf.Abs(groundSpeed) * deltaTime / GaitTiming.StrideLength(groundSpeed, CurrentGait);
        (bool front, bool back) cues = cycle.Advance(strides);
        pendingLift = (pendingLift.front || cues.front, pendingLift.back || cues.back);
    }

    // When the body outruns a planted foot (starting off, speeding up) that foot steps now instead of waiting for its cue,
    // and the cycle re-syncs to it so the steps that follow stay even.
    private void CueBalanceSteps() {
        if (!IsMoving) {
            return;
        }
        float normalLag = GaitTiming.LandingLead(groundSpeed, CurrentGait) + BalanceStepMargin;
        if (IsTrailingBeyond((frontLeg, normalLag)) && !pendingLift.front) {
            pendingLift.front = true;
            cycle.SyncToLift(true);
        }
        else if (IsTrailingBeyond((backLeg, normalLag)) && !pendingLift.back) {
            pendingLift.back = true;
            cycle.SyncToLift(false);
        }
    }

    // Speeding up, steps are planned for at least part of the intended speed; slowing down, for the current speed.
    private float PlanningSpeed() {
        bool isSpeedingUp = intendedSpeed * groundSpeed > 0f && Mathf.Abs(intendedSpeed) > Mathf.Abs(groundSpeed);
        float speed = Mathf.Abs(groundSpeed);
        return isSpeedingUp ? Mathf.Max(speed, SettingOffShare * Mathf.Abs(intendedSpeed)) : speed;
    }

    private bool IsTrailingBeyond((LegStepper leg, float lag) check) {
        float lag = (transform.position.x - check.leg.FootTarget.x) * Mathf.Sign(groundSpeed);
        return check.leg.IsGrounded && lag > check.lag;
    }

    // A leg cued by the cycle lifts as soon as it is planted (and, when walking, the other foot is nearly down).
    private void LiftFeetOnCue((float min, float max) feetSpan) {
        if (!IsMoving) {
            pendingLift = (false, false);
            return;
        }
        if (pendingLift.front && TryLiftOnCue((frontLeg, backLeg), feetSpan)) {
            pendingLift.front = false;
        }
        if (pendingLift.back && TryLiftOnCue((backLeg, frontLeg), feetSpan)) {
            pendingLift.back = false;
        }
    }

    // Returns false to keep the cue pending (the leg is still swinging or the other leg needs it down). A cue with no
    // foothold ahead is used up, so at a ledge the body moves on past the planted feet and falls.
    private bool TryLiftOnCue((LegStepper stepping, LegStepper other) legs, (float min, float max) feetSpan) {
        if (!legs.stepping.IsGrounded || !MayLiftWhile(legs.other)) {
            return false;
        }

        float direction = Mathf.Sign(groundSpeed);
        float planningSpeed = PlanningSpeed();
        float swing = GaitTiming.SwingDuration(planningSpeed, CurrentGait);
        float landingHipX = transform.position.x + direction * planningSpeed * swing;
        float desiredX = Mathf.Clamp(transform.position.x + direction * GaitTiming.LandingReach(planningSpeed, CurrentGait), feetSpan.min, feetSpan.max);
        lastStepFoundNoFoothold = !TryStepToward((legs.stepping, legs.other, desiredX, direction), (landingHipX, swing, CurrentGait.liftHeight, runShare));
        if (!lastStepFoundNoFoothold) {
            Carriage.Push(CurrentGait.pushOffImpulse * SpeedShare);
        }
        return true;
    }

    private bool MayLiftWhile(LegStepper otherLeg) {
        float requiredProgress = Mathf.Lerp(WalkingLiftOverlap, RunningLiftOverlap, runShare);
        return otherLeg.IsGrounded || otherLeg.StepProgress >= requiredProgress;
    }

    // Standing still, each foot steps toward its spot under the torso, the furthest first, finishing in a stance.
    private void SettleFeetWhenIdle() {
        bool isIdle = Mathf.Abs(groundSpeed) < idleSpeed && frontLeg.IsGrounded && backLeg.IsGrounded;
        if (!isIdle) {
            return;
        }

        float centerX = StanceCenterX();
        bool frontIsLeft = frontLeg.FootTarget.x <= backLeg.FootTarget.x;
        float frontSpot = centerX + (frontIsLeft ? -stanceHalfWidth : stanceHalfWidth);
        float backSpot = centerX + (frontIsLeft ? stanceHalfWidth : -stanceHalfWidth);
        float frontError = Mathf.Abs(frontLeg.FootTarget.x - frontSpot);
        float backError = Mathf.Abs(backLeg.FootTarget.x - backSpot);
        if (Mathf.Max(frontError, backError) <= idleFootTolerance) {
            return;
        }

        (LegStepper stepping, LegStepper planted, float spot) settle = frontError >= backError
            ? (frontLeg, backLeg, frontSpot)
            : (backLeg, frontLeg, backSpot);
        float towardSpot = Mathf.Sign(settle.spot - settle.stepping.FootTarget.x);
        TryStepToward((settle.stepping, settle.planted, settle.spot, towardSpot), (transform.position.x, stepArc.settleStepDuration, stepArc.settleLiftHeight, 0f));
    }

    // The torso leans a little ahead of the pelvis, so the standing feet are centred slightly ahead of it.
    private float StanceCenterX() {
        int facingSign = facing?.Sign ?? 0;
        return transform.position.x + facingSign * stanceLead;
    }

    // A step must carry the foot at least a minimum step along its direction; otherwise (e.g. at a ledge whose
    // nearest ground is under the foot already) no step is taken, rather than stepping on the spot forever.
    private bool TryStepToward(
        (LegStepper stepping, LegStepper planted, float desiredX, float direction) step,
        (float landingHipX, float duration, float liftHeight, float runShare) motion) {
        if (!TryPlanFoothold((step.stepping, step.planted, step.desiredX), motion.landingHipX, out Vector2 foothold)) {
            return false;
        }
        if ((foothold.x - step.stepping.FootTarget.x) * step.direction < footholdLimits.minStepLength) {
            return false;
        }
        step.stepping.BeginStep((foothold, motion.duration), (motion.liftHeight, motion.runShare));
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
        if (Mathf.Abs(groundSpeed) < idleSpeed) {
            return 0f;
        }
        float bodyPositionX = transform.position.x;
        float supportFront = groundSpeed > 0f
            ? Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x)
            : Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        return (bodyPositionX - supportFront) * Mathf.Sign(groundSpeed);
    }

    private void TickLegs(float deltaTime) {
        frontLeg.TickStep(deltaTime, stepArc);
        backLeg.TickStep(deltaTime, stepArc);
    }

    // ---- Body ------------------------------------------------------------------------------------------------

    private void CarryBodyOverFeet((float deltaTime, (float min, float max) bodySpan) frame) {
        if (Carriage.NeedsRestart) {
            BeginGroundedMotion(Vector2.zero);
        }

        AbsorbFootPlant(frontLeg);
        AbsorbFootPlant(backLeg);

        Vector2 target = new Vector2(BodyTargetX(), BodyTargetY());
        Carriage.Carry((target, frame.deltaTime), (horizontalSpring, verticalSpring, frame.bodySpan));
    }

    // The body takes a planted foot's impact as a downward push, like a knee bending under the weight.
    private void AbsorbFootPlant(LegStepper leg) {
        if (!leg.TryConsumePlant(out float impactSpeed)) {
            return;
        }
        Carriage.Push(-Mathf.Min(maxPlantImpulse, impactSpeed * CurrentGait.plantImpulse));
        events?.RaiseFootPlanted((leg.FootTarget, impactSpeed));
    }

    // The moving anchor plus the step's surge: the body runs slightly ahead coming through each low point.
    private float BodyTargetX() {
        float surge = GaitWaves.Surge((cycle.Phase, LowestPhase)) * CurrentGait.surge * SpeedShare;
        return anchorX + Mathf.Sign(groundSpeed) * surge;
    }

    // The gait's height, never higher than the planted legs reach. The reach is measured from where the body is: its
    // springs trail the anchor a little at speed, and measuring from the anchor would make a trailing foot seem further
    // behind than it is.
    private float BodyTargetY() {
        float gaitHeight = GaitHeight();
        float pelvisX = transform.position.x;
        float reachable = Mathf.Min(HighestPelvisOver((frontLeg, pelvisX)), HighestPelvisOver((backLeg, pelvisX)));
        return Mathf.Clamp(reachable, gaitHeight - maxReachDrop, gaitHeight);
    }

    // Standing height over the feet with the step's bob, sunk lower the faster the gait moves and by any held crouch.
    private float GaitHeight() {
        float bob = GaitWaves.BodyBob((cycle.Phase, LowestPhase)) * CurrentGait.bobAmplitude * SpeedShare;
        return StanceHeight + bodyHeight + bob - CurrentGait.pelvisDrop * SpeedShare - HeldCrouch;
    }

    private float LowestPhase => GaitWaves.LowestPhase((CurrentGait.dutyFactor, CurrentGait.lowAfterContact));

    private float HighestPelvisOver((LegStepper leg, float pelvisX) support) {
        if (!support.leg.IsGrounded) {
            return float.PositiveInfinity;
        }
        Vector2 hipOffset = support.leg.HipPosition - (Vector2)transform.position;
        float reach = support.leg.LegLength * legExtension;
        return GaitPose.HighestPelvisY((support.leg.FootTarget, support.pelvisX), (hipOffset, reach));
    }
}
