using System.Collections.Generic;
using UnityEngine;

// Walking gait: moves the stride target, plans the next foothold, steps one leg at a time and keeps the body over the feet.
public class StrideController : MonoBehaviour {
    private const float StrideClampFactor = 1.8f;
    private const float BodyAnchorCount = 3f;
    private const float StanceProbeHeadroom = 0.5f;
    private const float GroundContactTolerance = 0.15f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [SerializeField] private Transform strideTarget;

    [SerializeField] private float bodyHeight = 2.3f;
    [SerializeField] private float strideSpeed = 2.0f;
    [SerializeField] private float easeDivisor = 3.0f;
    [SerializeField] private float reachRange = 1.2f;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet toward a missing foothold before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("Half the distance between the feet when settling into a stance after landing.")]
    [SerializeField] private float stanceHalfWidth = 0.3f;
    [Tooltip("How quickly a landing dip recovers, per second.")]
    [SerializeField] private float landingDipRecovery = 5f;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private bool lastStepFoundNoFoothold;
    private float landingDip;

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight => bodyHeight;
    public float MaxStrideOffset => reachRange * StrideClampFactor;
    public float StanceHalfWidth => stanceHalfWidth;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;

    // Held by a state (e.g. a jump wind-up) to lower the body.
    public float HeldCrouch { get; set; }

    // The body leans past both feet toward a gap or drop that offers no foothold.
    public bool IsPastSupportEdge => lastStepFoundNoFoothold && SupportOverhang() > edgeOverhang;

    // Neither foot stands on ground any more (e.g. spawned in the air or the floor moved away).
    public bool HasLostGround => !IsFootOnGround(frontLeg) && !IsFootOnGround(backLeg);

    public void TickWalk(float deltaTime, (float direction, float minX, float maxX) stride) {
        MoveStrideTarget(stride.direction * strideSpeed * deltaTime, (stride.minX, stride.maxX));
        StartStepWhenStrideOutOfReach();
        TickLegs(deltaTime);
        RecoverLandingDip(deltaTime);
        CenterBodyHorizontally(deltaTime);
        MatchBodyHeightToFeet(deltaTime);
    }

    // The leg whose foot is further along the facing direction leads.
    public (LegStepper lead, LegStepper trail) LegsOrderedByFacing(int facingSign) {
        bool frontLeads = frontLeg.FootTarget.x * facingSign >= backLeg.FootTarget.x * facingSign;
        return frontLeads ? (frontLeg, backLeg) : (backLeg, frontLeg);
    }

    public void AbsorbLanding(float dipDepth) => landingDip = Mathf.Max(landingDip, dipDepth);

    public void PlaceStrideTargetUnderBody() => strideTarget.position = new Vector2(transform.position.x, StanceHeight);

    // Picks ground under both feet around centerX and lets the legs settle onto it.
    public bool TrySettleStance((float centerX, int facingSign) stance) {
        float frontX = stance.centerX + stance.facingSign * stanceHalfWidth;
        float backX = stance.centerX - stance.facingSign * stanceHalfWidth;
        if (!TryFindStanceGround(frontX, out Vector2 frontGround) || !TryFindStanceGround(backX, out Vector2 backGround)) {
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

    private bool TryFindStanceGround(float x, out Vector2 ground) {
        Vector2 origin = new Vector2(x, transform.position.y + StanceProbeHeadroom);
        float depth = StanceProbeHeadroom + bodyHeight + footholdLimits.maxStepDown;
        return FootholdProbe.TryFindGroundBelow((origin, depth), footholdLimits.groundMask, out ground);
    }

    private bool IsFootOnGround(LegStepper leg) {
        Vector2 origin = leg.FootTarget + Vector2.up * GroundContactTolerance;
        return FootholdProbe.TryFindGroundBelow((origin, 2f * GroundContactTolerance), footholdLimits.groundMask, out _);
    }

    private void MoveStrideTarget(float horizontalDelta, (float minX, float maxX) freeSpan) {
        float minX = Mathf.Max(transform.position.x - MaxStrideOffset, freeSpan.minX);
        float maxX = Mathf.Min(transform.position.x + MaxStrideOffset, freeSpan.maxX);
        float strideX = Mathf.Clamp(strideTarget.position.x + horizontalDelta, minX, maxX);

        strideTarget.position = new Vector2(strideX, StanceHeight);
    }

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

        lastStepFoundNoFoothold = !TryPlanFoothold(legs, out Vector2 foothold);
        if (!lastStepFoundNoFoothold) {
            legs.stepping.BeginStep(foothold);
        }
    }

    private bool TryPlanFoothold((LegStepper stepping, LegStepper planted) legs, out Vector2 foothold) {
        FootholdRequest request = new FootholdRequest {
            DesiredX = strideTarget.position.x,
            PlantedFoot = legs.planted.FootTarget,
            HipHeight = legs.stepping.HipPosition.y - StanceHeight,
            LegLength = legs.stepping.LegLength,
            ForwardSign = Mathf.Sign(strideTarget.position.x - transform.position.x),
        };

        FootholdProbe.CollectCandidates((request.DesiredX, request.PlantedFoot.y, legs.stepping.FootTarget), footholdLimits, footholdCandidates);
        return FootholdPlanner.TryChooseFoothold(request, (footholdCandidates, footholdLimits), out foothold);
    }

    private float SupportOverhang() {
        float bodyX = transform.position.x;
        float supportMin = Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        float supportMax = Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        return Mathf.Max(bodyX - supportMax, supportMin - bodyX);
    }

    private void TickLegs(float deltaTime) {
        frontLeg.TickStep(deltaTime, (reachRange, easeDivisor));
        backLeg.TickStep(deltaTime, (reachRange, easeDivisor));
    }

    private void RecoverLandingDip(float deltaTime) {
        landingDip *= Mathf.Exp(-landingDipRecovery * deltaTime);
    }

    // Eases the body toward the mean x of both feet and the stride target.
    private void CenterBodyHorizontally(float deltaTime) {
        Vector2 position = transform.position;
        float anchorMeanX = (frontLeg.FootTarget.x + backLeg.FootTarget.x + strideTarget.position.x) / BodyAnchorCount;

        position.x += (anchorMeanX - position.x) * BodyEaseFraction(deltaTime);
        transform.position = position;
    }

    // Eases the body toward the average foot height plus standing height, the step lift and any crouch.
    private void MatchBodyHeightToFeet(float deltaTime) {
        Vector2 position = transform.position;
        float stepLift = Mathf.Max(frontLeg.BodyLift, backLeg.BodyLift);
        float targetY = StanceHeight + bodyHeight + stepLift - HeldCrouch - landingDip;

        position.y += (targetY - position.y) * BodyEaseFraction(deltaTime);
        transform.position = position;
    }

    private float BodyEaseFraction(float deltaTime) => FrameRateEase.FractionForFrame(1f / easeDivisor, deltaTime);
}
