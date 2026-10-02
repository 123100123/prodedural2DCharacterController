using System.Collections.Generic;
using UnityEngine;

// Grounded gait: the body moves at the ground speed and the feet follow it. A foot lifts once it trails half a
// stance behind the hip and lands where the hip will be at touchdown plus half a stance ahead. Walk and run are
// blended profiles, so idle, walking and running flow into each other. The body rides the feet as a mass on springs.
public class StrideController : MonoBehaviour {
    private const float GroundContactTolerance = 0.15f;
    private const float WallLookAhead = 2.5f;
    // Long frames (hitches) are split into gait steps no longer than this, so the feet keep up with the body.
    private const float MaxGaitStep = 1f / 30f;
    // How far through its swing the other foot must be before the trailing foot may lift: late when walking
    // (a heel-off just before touchdown), mid-swing when running (both feet in the air for a moment).
    private const float WalkingLiftOverlap = 0.8f;
    private const float RunningLiftOverlap = 0.5f;
    // A body found further than this from where the springs last put it was moved by something else (a respawn, a teleport).
    private const float ExternalMoveDistance = 0.5f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [Tooltip("Debug marker showing where the gait is carrying the body.")]
    [SerializeField] private Transform strideTarget;

    [SerializeField] private float bodyHeight = 2.3f;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("With no movement input, momentum carries the body at most this far past its feet, so stopping near a ledge settles there.")]
    [SerializeField] private float stoppingOverhang = 0.3f;
    [Tooltip("Half the distance between the feet when standing.")]
    [SerializeField] private float stanceHalfWidth = 0.3f;

    [Header("Gait")]
    [SerializeField] private GaitProfile walk = GaitProfile.Walk;
    [SerializeField] private GaitProfile run = GaitProfile.Run;
    [Tooltip("Speed gained per second when starting to move or speeding up.")]
    [SerializeField] private float acceleration = 18f;
    [Tooltip("Speed lost per second when stopping, slowing or turning.")]
    [SerializeField] private float deceleration = 30f;
    [Tooltip("How quickly the gait blends between walking and running (share per second).")]
    [SerializeField] private float runBlendRate = 2.5f;
    [Tooltip("Below this ground speed the character is idle and settles its feet into a stance.")]
    [SerializeField] private float idleSpeed = 0.2f;
    [Tooltip("An idle foot further than this from its stance spot takes a settling step.")]
    [SerializeField] private float idleFootTolerance = 0.25f;
    [SerializeField] private StepArcShape stepArc = new StepArcShape();

    [Header("Body mass")]
    [SerializeField] private SpringSettings horizontalSpring = new SpringSettings(3f, 0.7f, 1f);
    [SerializeField] private SpringSettings verticalSpring = new SpringSettings(3.2f, 0.45f, 0f);
    [Tooltip("Share of the leg length a supporting leg may extend to; the pelvis drops when the feet are spread further.")]
    [Range(0.8f, 1f)] [SerializeField] private float legExtension = 0.96f;
    [Tooltip("Share of the straight-leg drop the pelvis actually takes when the feet are spread. Real knees and ankles lengthen the leg, so hips bob less than straight legs would.")]
    [Range(0f, 1f)] [SerializeField] private float bobDepth = 0.5f;
    [SerializeField] private float maxPlantImpulse = 2.5f;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private SecondOrderDynamics bodyX;
    private SecondOrderDynamics bodyY;
    private float groundSpeed;
    private float runShare;
    private float anchorX;
    private Vector2 lastWrittenBodyPosition;
    private CharacterEvents events;

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight => bodyHeight;
    public float WallProbeDistance => WallLookAhead;
    public float StanceHalfWidth => stanceHalfWidth;
    public float MaxStepUp => footholdLimits.maxStepUp;
    public float MaxStepDown => footholdLimits.maxStepDown;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;
    public Vector2 BodyVelocity => bodyX == null ? Vector2.zero : new Vector2(bodyX.Velocity, bodyY.Velocity);

    // The blend of the walk and run profiles currently in use.
    public GaitProfile CurrentGait { get; private set; } = GaitProfile.Walk;
    // Ground speed as a share of the current gait's full speed: 0 idle, 1 at full walk or run speed.
    public float SpeedShare => Mathf.Clamp01(Mathf.Abs(groundSpeed) / Mathf.Max(0.01f, CurrentGait.speed));

    // Held by a state (e.g. a jump wind-up) to lower the body.
    public float HeldCrouch { get; set; }

    // The body leans past both feet (with a swinging foot counted at its landing spot), e.g. walking off a ledge.
    public bool IsPastSupportEdge => SupportOverhang() > edgeOverhang;

    // Neither foot stands on ground any more (e.g. spawned in the air or the floor moved away).
    public bool HasLostGround => !IsFootOnGround(frontLeg) && !IsFootOnGround(backLeg);

    public void ConnectEvents(CharacterEvents characterEvents) => events = characterEvents;

    // Hands the body to the gait with its current momentum, e.g. after a landing (a downward velocity becomes the squash).
    public void BeginGroundedMotion(Vector2 velocity) {
        Vector2 position = transform.position;
        bodyX = bodyX ?? new SecondOrderDynamics(position.x);
        bodyY = bodyY ?? new SecondOrderDynamics(position.y);
        bodyX.ResetWithVelocity((position.x, velocity.x));
        bodyY.ResetWithVelocity((position.y, velocity.y));
        groundSpeed = Mathf.Clamp(velocity.x, -run.speed, run.speed);
        anchorX = position.x;
        lastWrittenBodyPosition = position;
    }

    // feetSpan bounds where feet may go (up to wall faces); bodySpan bounds the body pivot so its hull stays out of walls.
    public void TickWalk(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        int gaitSteps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / MaxGaitStep));
        for (int step = 0; step < gaitSteps; step++) {
            TickGaitStep(deltaTime / gaitSteps, intent);
        }
    }

    private void TickGaitStep(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        BlendGait((intent.running, deltaTime));
        AccelerateGround((intent.direction, deltaTime));
        AdvanceAnchor((deltaTime, AnchorSpan(intent)));
        StepFeetAlongMotion(intent.feetSpan);
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
        bool isSpeedingUp = Mathf.Abs(targetSpeed) > Mathf.Abs(groundSpeed) && targetSpeed * groundSpeed >= 0f;
        float rate = isSpeedingUp ? acceleration : deceleration;
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

    // The trailing foot lifts once it is half a stance behind the hip and lands half a stance ahead of where the hip
    // will be at touchdown. Walking keeps the other foot down; running may lift before the other foot lands.
    private void StepFeetAlongMotion((float min, float max) feetSpan) {
        if (Mathf.Abs(groundSpeed) < idleSpeed) {
            return;
        }

        // A swinging foot's target is its landing spot ahead, so ordering by targets finds the planted, trailing foot.
        float direction = Mathf.Sign(groundSpeed);
        (LegStepper lead, LegStepper trail) legs = LegsOrderedByFacing((int)direction);
        if (!legs.trail.IsGrounded || !MayLiftWhile(legs.lead)) {
            return;
        }

        float hipX = transform.position.x;
        float halfExcursion = CurrentGait.FootExcursion(groundSpeed) / 2f;
        if ((hipX - legs.trail.FootTarget.x) * direction < halfExcursion) {
            return;
        }

        float landingHipX = hipX + groundSpeed * CurrentGait.swingTime;
        float desiredX = Mathf.Clamp(landingHipX + direction * halfExcursion, feetSpan.min, feetSpan.max);
        if (TryStepToward((legs.trail, legs.lead, desiredX, direction), (landingHipX, CurrentGait.swingTime, CurrentGait.liftHeight))) {
            bodyY.AddVelocity(CurrentGait.pushOffImpulse * SpeedShare);
        }
    }

    private bool MayLiftWhile(LegStepper otherLeg) {
        float requiredProgress = Mathf.Lerp(WalkingLiftOverlap, RunningLiftOverlap, runShare);
        return otherLeg.IsGrounded || otherLeg.StepProgress >= requiredProgress;
    }

    // Standing still, each foot steps toward its spot beside the hip, the furthest first, finishing in a stance.
    private void SettleFeetWhenIdle() {
        bool isIdle = Mathf.Abs(groundSpeed) < idleSpeed && frontLeg.IsGrounded && backLeg.IsGrounded;
        if (!isIdle) {
            return;
        }

        float hipX = transform.position.x;
        bool frontIsLeft = frontLeg.FootTarget.x <= backLeg.FootTarget.x;
        float frontSpot = hipX + (frontIsLeft ? -stanceHalfWidth : stanceHalfWidth);
        float backSpot = hipX + (frontIsLeft ? stanceHalfWidth : -stanceHalfWidth);
        float frontError = Mathf.Abs(frontLeg.FootTarget.x - frontSpot);
        float backError = Mathf.Abs(backLeg.FootTarget.x - backSpot);
        if (Mathf.Max(frontError, backError) <= idleFootTolerance) {
            return;
        }

        (LegStepper stepping, LegStepper planted, float spot) settle = frontError >= backError
            ? (frontLeg, backLeg, frontSpot)
            : (backLeg, frontLeg, backSpot);
        float towardSpot = Mathf.Sign(settle.spot - settle.stepping.FootTarget.x);
        TryStepToward((settle.stepping, settle.planted, settle.spot, towardSpot), (hipX, stepArc.settleStepDuration, stepArc.settleLiftHeight));
    }

    // A step must carry the foot at least a minimum step along its direction; otherwise (e.g. at a ledge whose
    // nearest ground is under the foot already) no step is taken, rather than stepping on the spot forever.
    private bool TryStepToward(
        (LegStepper stepping, LegStepper planted, float desiredX, float direction) step,
        (float landingHipX, float duration, float liftHeight) motion) {
        if (!TryPlanFoothold((step.stepping, step.planted, step.desiredX), motion.landingHipX, out Vector2 foothold)) {
            return false;
        }
        if ((foothold.x - step.stepping.FootTarget.x) * step.direction < footholdLimits.minStepLength) {
            return false;
        }
        step.stepping.BeginStep((foothold, motion.duration, motion.liftHeight));
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
        if (bodyX == null || WasMovedExternally()) {
            BeginGroundedMotion(Vector2.zero);
        }

        AbsorbFootPlant(frontLeg);
        AbsorbFootPlant(backLeg);

        float targetX = BodyTargetX();
        float x = KeepWithinSpan(bodyX.Update((targetX, frame.deltaTime), horizontalSpring), frame.bodySpan);
        float y = bodyY.Update((BodyTargetY(targetX), frame.deltaTime), verticalSpring);
        transform.position = new Vector3(x, y, transform.position.z);
        lastWrittenBodyPosition = transform.position;
    }

    // A body spring overshooting into a wall stops there and loses its speed toward it.
    private float KeepWithinSpan(float x, (float min, float max) span) {
        float clamped = Mathf.Clamp(x, Mathf.Min(span.min, span.max), Mathf.Max(span.min, span.max));
        if (!Mathf.Approximately(clamped, x)) {
            bodyX.ResetWithVelocity((clamped, 0f));
        }
        return clamped;
    }

    private bool WasMovedExternally() => Vector2.Distance(transform.position, lastWrittenBodyPosition) > ExternalMoveDistance;

    // The body takes a planted foot's impact as a downward push, like a knee bending under the weight.
    private void AbsorbFootPlant(LegStepper leg) {
        if (!leg.TryConsumePlant(out float impactSpeed)) {
            return;
        }
        bodyY.AddVelocity(-Mathf.Min(maxPlantImpulse, impactSpeed * CurrentGait.plantImpulse));
        events?.RaiseFootPlanted((leg.FootTarget, impactSpeed));
    }

    // The moving anchor, leaning toward the supporting foot while the other leg swings.
    private float BodyTargetX() {
        LegStepper support = SupportingLeg();
        return support == null ? anchorX : Mathf.Lerp(anchorX, support.FootTarget.x, CurrentGait.weightShift);
    }

    private LegStepper SupportingLeg() {
        if (frontLeg.IsGrounded == backLeg.IsGrounded) {
            return null;
        }
        return frontLeg.IsGrounded ? frontLeg : backLeg;
    }

    // Standing height over the feet, lowered wherever a planted leg could not reach the ground from it,
    // and sunk lower the faster the gait moves.
    private float BodyTargetY(float targetX) {
        float standing = StanceHeight + bodyHeight;
        float reachable = Mathf.Min(standing, Mathf.Min(HighestPelvisOver((frontLeg, targetX)), HighestPelvisOver((backLeg, targetX))));
        float bobbing = Mathf.Lerp(standing, reachable, bobDepth);
        return bobbing - CurrentGait.pelvisDrop * SpeedShare - HeldCrouch;
    }

    private float HighestPelvisOver((LegStepper leg, float pelvisX) support) {
        if (!support.leg.IsGrounded) {
            return float.PositiveInfinity;
        }
        Vector2 hipOffset = support.leg.HipPosition - (Vector2)transform.position;
        float reach = support.leg.LegLength * legExtension;
        return GaitPose.HighestPelvisY((support.leg.FootTarget, support.pelvisX), (hipOffset, reach));
    }
}
