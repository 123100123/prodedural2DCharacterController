using System.Collections.Generic;
using UnityEngine;

// Walking gait: moves the stride target with momentum, plans the next foothold, steps one leg at a time,
// and carries the body over the feet as a mass on springs.
public class StrideController : MonoBehaviour {
    private const float StrideClampFactor = 1.8f;
    private const float BodyAnchorCount = 3f;
    private const float GroundContactTolerance = 0.15f;
    private const float IdleStrideSpeed = 0.1f;
    // A body found further than this from where the springs last put it was moved by something else (a respawn, a teleport).
    private const float ExternalMoveDistance = 0.5f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [SerializeField] private Transform strideTarget;

    [SerializeField] private float bodyHeight = 2.3f;
    [SerializeField] private float reachRange = 1.2f;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet toward a missing foothold before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("Half the distance between the feet when settling into a stance after landing.")]
    [SerializeField] private float stanceHalfWidth = 0.3f;
    [Tooltip("When standing still with the feet further apart than this, the far foot takes a settling step under the body.")]
    [SerializeField] private float maxIdleFeetGap = 1f;

    [Header("Movement")]
    [SerializeField] private float strideSpeed = 2.0f;
    [Tooltip("Speed gained per second when starting to walk.")]
    [SerializeField] private float strideAcceleration = 45f;
    [Tooltip("Speed lost per second when stopping or turning.")]
    [SerializeField] private float strideDeceleration = 70f;
    [SerializeField] private StepArcShape stepArc = new StepArcShape();

    [Header("Body mass")]
    [SerializeField] private SpringSettings horizontalSpring = new SpringSettings(2.2f, 0.65f, 0.5f);
    [SerializeField] private SpringSettings verticalSpring = new SpringSettings(3.2f, 0.45f, 0f);

    [Header("Gait weight")]
    [Tooltip("Share of the leg length a supporting leg may extend to; the pelvis drops when the feet are spread further.")]
    [Range(0.8f, 1f)] [SerializeField] private float legExtension = 0.96f;
    [Tooltip("Downward body velocity added per unit of foot impact speed when a foot plants.")]
    [SerializeField] private float plantImpulsePerSpeed = 0.3f;
    [SerializeField] private float maxPlantImpulse = 2.5f;
    [Tooltip("Share of the way the body leans toward the supporting foot while the other leg swings.")]
    [Range(0f, 1f)] [SerializeField] private float weightShiftShare = 0.2f;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private SecondOrderDynamics bodyX;
    private SecondOrderDynamics bodyY;
    private float strideVelocity;
    private Vector2 lastWrittenBodyPosition;
    private bool lastStepFoundNoFoothold;
    private CharacterEvents events;

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight => bodyHeight;
    public float MaxStrideOffset => reachRange * StrideClampFactor;
    public float StanceHalfWidth => stanceHalfWidth;
    public float MaxStepUp => footholdLimits.maxStepUp;
    public float MaxStepDown => footholdLimits.maxStepDown;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;
    public Vector2 BodyVelocity => bodyX == null ? Vector2.zero : new Vector2(bodyX.Velocity, bodyY.Velocity);

    // Held by a state (e.g. a jump wind-up) to lower the body.
    public float HeldCrouch { get; set; }

    // The body leans past both feet toward a gap or drop that offers no foothold.
    public bool IsPastSupportEdge => lastStepFoundNoFoothold && SupportOverhang() > edgeOverhang;

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
        lastWrittenBodyPosition = position;
        strideVelocity = Mathf.Clamp(velocity.x, -strideSpeed, strideSpeed);
    }

    // feetSpan bounds where feet may go (up to wall faces); bodySpan bounds the body pivot so its hull stays out of walls.
    public void TickWalk(float deltaTime, (float direction, (float min, float max) feetSpan, (float min, float max) bodySpan) walk) {
        AccelerateStride((walk.direction, deltaTime));
        MoveStrideTarget(strideVelocity * deltaTime, walk.feetSpan);
        StartStepWhenStrideOutOfReach();
        SettleFeetWhenIdle();
        TickLegs(deltaTime);
        CarryBodyOverFeet((deltaTime, walk.bodySpan));
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

    // ---- Stride ----------------------------------------------------------------------------------------------

    // Speeds up gradually and brakes harder than it accelerates, so starts and stops carry momentum.
    private void AccelerateStride((float direction, float deltaTime) input) {
        float targetSpeed = input.direction * strideSpeed;
        bool isSpeedingUp = Mathf.Abs(targetSpeed) > Mathf.Abs(strideVelocity) && targetSpeed * strideVelocity >= 0f;
        float rate = isSpeedingUp ? strideAcceleration : strideDeceleration;
        strideVelocity = Mathf.MoveTowards(strideVelocity, targetSpeed, rate * input.deltaTime);
    }

    // A stride target held back by the reach clamp or a wall loses its speed in that direction.
    private void MoveStrideTarget(float horizontalDelta, (float min, float max) freeSpan) {
        float minX = Mathf.Max(transform.position.x - MaxStrideOffset, freeSpan.min);
        float maxX = Mathf.Min(transform.position.x + MaxStrideOffset, freeSpan.max);
        float desiredX = strideTarget.position.x + horizontalDelta;
        float strideX = Mathf.Clamp(desiredX, minX, maxX);

        if (!Mathf.Approximately(strideX, desiredX)) {
            strideVelocity = 0f;
        }
        strideTarget.position = new Vector2(strideX, StanceHeight);
    }

    // ---- Steps -----------------------------------------------------------------------------------------------

    // Only one leg steps at a time: the one whose foot trails farther behind the stride target.
    private void StartStepWhenStrideOutOfReach() {
        if (!frontLeg.IsGrounded || !backLeg.IsGrounded) {
            return;
        }

        float frontGap = Mathf.Abs(strideTarget.position.x - frontLeg.FootTarget.x);
        float backGap = Mathf.Abs(strideTarget.position.x - backLeg.FootTarget.x);

        if (frontGap > backGap) {
            StartStepIfBeyondPlantedFoot((frontLeg, backLeg));
        }
        else {
            StartStepIfBeyondPlantedFoot((backLeg, frontLeg));
        }
    }

    private void StartStepIfBeyondPlantedFoot((LegStepper stepping, LegStepper planted) legs) {
        if (Mathf.Abs(strideTarget.position.x - legs.planted.FootTarget.x) <= reachRange) {
            return;
        }

        lastStepFoundNoFoothold = !TryStepToward((legs.stepping, legs.planted, strideTarget.position.x));
    }

    // Standing still with the feet wide apart, the far foot steps back under the body, finishing the stop in a stance.
    private void SettleFeetWhenIdle() {
        bool isIdle = Mathf.Abs(strideVelocity) < IdleStrideSpeed && frontLeg.IsGrounded && backLeg.IsGrounded;
        if (!isIdle || Mathf.Abs(frontLeg.FootTarget.x - backLeg.FootTarget.x) <= maxIdleFeetGap) {
            return;
        }

        float bodyX = transform.position.x;
        bool frontIsFarther = Mathf.Abs(frontLeg.FootTarget.x - bodyX) > Mathf.Abs(backLeg.FootTarget.x - bodyX);
        (LegStepper stepping, LegStepper planted) legs = frontIsFarther ? (frontLeg, backLeg) : (backLeg, frontLeg);
        float stanceX = bodyX + Mathf.Sign(legs.stepping.FootTarget.x - bodyX) * stanceHalfWidth;
        TryStepToward((legs.stepping, legs.planted, stanceX));
    }

    private bool TryStepToward((LegStepper stepping, LegStepper planted, float desiredX) step) {
        if (!TryPlanFoothold(step, out Vector2 foothold)) {
            return false;
        }
        float distance = Vector2.Distance(step.stepping.FootTarget, foothold);
        step.stepping.BeginStep((foothold, stepArc.DurationFor(distance)));
        return true;
    }

    private bool TryPlanFoothold((LegStepper stepping, LegStepper planted, float desiredX) step, out Vector2 foothold) {
        FootholdRequest request = new FootholdRequest {
            DesiredX = step.desiredX,
            PlantedFoot = step.planted.FootTarget,
            HipHeight = step.stepping.HipPosition.y - StanceHeight,
            LegLength = step.stepping.LegLength,
            ForwardSign = Mathf.Sign(step.desiredX - transform.position.x),
        };

        FootholdProbe.CollectCandidates((request.DesiredX, request.PlantedFoot.y, step.stepping.FootTarget), footholdLimits, footholdCandidates);
        return FootholdPlanner.TryChooseFoothold(request, (footholdCandidates, footholdLimits), out foothold);
    }

    private float SupportOverhang() {
        float bodyX = transform.position.x;
        float supportMin = Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        float supportMax = Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        return Mathf.Max(bodyX - supportMax, supportMin - bodyX);
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
        bodyY.AddVelocity(-Mathf.Min(maxPlantImpulse, impactSpeed * plantImpulsePerSpeed));
        events?.RaiseFootPlanted((leg.FootTarget, impactSpeed));
    }

    // Mean of both feet and the stride target, leaning toward the supporting foot while the other leg swings.
    private float BodyTargetX() {
        float anchorMeanX = (frontLeg.FootTarget.x + backLeg.FootTarget.x + strideTarget.position.x) / BodyAnchorCount;
        LegStepper support = SupportingLeg();
        return support == null ? anchorMeanX : Mathf.Lerp(anchorMeanX, support.FootTarget.x, weightShiftShare);
    }

    private LegStepper SupportingLeg() {
        if (frontLeg.IsGrounded == backLeg.IsGrounded) {
            return null;
        }
        return frontLeg.IsGrounded ? frontLeg : backLeg;
    }

    // Standing height over the feet, lowered wherever a planted leg could not reach the ground from it.
    private float BodyTargetY(float targetX) {
        float highest = StanceHeight + bodyHeight;
        highest = Mathf.Min(highest, HighestPelvisOver((frontLeg, targetX)));
        highest = Mathf.Min(highest, HighestPelvisOver((backLeg, targetX)));
        return highest - HeldCrouch;
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
