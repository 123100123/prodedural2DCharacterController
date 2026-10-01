using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Walking gait: moves the stride target, plans the next foothold, steps one leg at a time and keeps the body over the feet.
public class StrideController : MonoBehaviour {
    private const float StrideClampFactor = 1.8f;
    private const float BodyAnchorCount = 3f;

    [FormerlySerializedAs("frontLegSolver")]
    [SerializeField] private LegStepper frontLeg;
    [FormerlySerializedAs("backLegSolver")]
    [SerializeField] private LegStepper backLeg;
    [SerializeField] private Transform strideTarget;

    [SerializeField] private float bodyHeight = 2.3f;
    [SerializeField] private float strideSpeed = 2.0f;
    [SerializeField] private float easeDivisor = 3.0f;
    [SerializeField] private float reachRange = 1.2f;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();

    private void Update() {
        TickWalk(Time.deltaTime, ReadStrideDirection());
    }

    private static float ReadStrideDirection() {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) {
            return 0f;
        }
        return (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
    }

    public void TickWalk(float deltaTime, float strideDirection) {
        MoveStrideTarget(strideDirection * strideSpeed * deltaTime);
        StartStepWhenStrideOutOfReach();
        TickLegs(deltaTime);
        CenterBodyHorizontally(deltaTime);
        MatchBodyHeightToFeet(deltaTime);
    }

    private float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;

    private void MoveStrideTarget(float horizontalDelta) {
        float maxOffset = reachRange * StrideClampFactor;
        float strideX = Mathf.Clamp(strideTarget.position.x + horizontalDelta, transform.position.x - maxOffset, transform.position.x + maxOffset);

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

        if (TryPlanFoothold(legs, out Vector2 foothold)) {
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

    private void TickLegs(float deltaTime) {
        frontLeg.TickStep(deltaTime, (reachRange, easeDivisor));
        backLeg.TickStep(deltaTime, (reachRange, easeDivisor));
    }

    // Eases the body toward the mean x of both feet and the stride target.
    private void CenterBodyHorizontally(float deltaTime) {
        Vector2 position = transform.position;
        float anchorMeanX = (frontLeg.FootTarget.x + backLeg.FootTarget.x + strideTarget.position.x) / BodyAnchorCount;

        position.x += (anchorMeanX - position.x) * BodyEaseFraction(deltaTime);
        transform.position = position;
    }

    // Eases the body toward the average foot height plus standing height and the current step lift.
    private void MatchBodyHeightToFeet(float deltaTime) {
        Vector2 position = transform.position;
        float stepLift = Mathf.Max(frontLeg.BodyLift, backLeg.BodyLift);
        float targetY = StanceHeight + bodyHeight + stepLift;

        position.y += (targetY - position.y) * BodyEaseFraction(deltaTime);
        transform.position = position;
    }

    private float BodyEaseFraction(float deltaTime) => FrameRateEase.FractionForFrame(1f / easeDivisor, deltaTime);
}
