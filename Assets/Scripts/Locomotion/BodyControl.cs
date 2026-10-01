using UnityEngine;
using UnityEngine.InputSystem;

// Moves the stride target from input, decides which leg steps next, and keeps the body centred over the feet.
public class BodyControl : MonoBehaviour {
    private const int LegCount = 2;
    private const float StrideClampFactor = 1.8f;
    private const float BodyAnchorCount = 3f;

    [SerializeField] private Transform frontFootTarget;
    [SerializeField] private Transform backFootTarget;
    [SerializeField] private Transform strideTarget;

    [SerializeField] private MoveTarget frontLegSolver;
    [SerializeField] private MoveTarget backLegSolver;

    [SerializeField] private Transform frontDesiredFoothold;
    [SerializeField] private Transform backDesiredFoothold;

    [SerializeField] private float bodyHeight = 2.3f;
    [SerializeField] private float strideSpeed = 2.0f;
    [SerializeField] private float easeDivisor = 3.0f;
    [SerializeField] private float reachRange = 1.2f;

    private int groundedLegCount = LegCount;
    private float stepLiftHeight;

    public float ReachRange => reachRange;
    public float EaseDivisor => easeDivisor;

    public void SetStepLift(float liftHeight) => stepLiftHeight = liftHeight;

    public void RegisterLegLanded() => groundedLegCount += 1;

    private void Update() {
        MoveStrideTargetFromInput();
        StartStepWhenStrideOutOfReach();
        CenterBodyHorizontally();
        MatchBodyHeightToFeet();
    }

    private void MoveStrideTargetFromInput() {
        Vector2 position = strideTarget.position;
        position.x += ReadStrideDirection() * strideSpeed * Time.deltaTime;

        float maxOffset = reachRange * StrideClampFactor;
        position.x = Mathf.Clamp(position.x, transform.position.x - maxOffset, transform.position.x + maxOffset);

        strideTarget.position = position;
    }

    private static float ReadStrideDirection() {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) {
            return 0f;
        }

        float direction = 0f;
        if (keyboard.dKey.isPressed) {
            direction += 1f;
        }
        if (keyboard.aKey.isPressed) {
            direction -= 1f;
        }
        return direction;
    }

    // Only one leg steps at a time: the one whose foot is farther from the stride target.
    private void StartStepWhenStrideOutOfReach() {
        if (groundedLegCount != LegCount) {
            return;
        }

        float frontDistance = Vector2.Distance(strideTarget.position, frontFootTarget.position);
        float backDistance = Vector2.Distance(strideTarget.position, backFootTarget.position);

        if (frontDistance > backDistance) {
            StartStepIfBeyondPlantedFoot((frontLegSolver, frontDesiredFoothold), backDesiredFoothold);
        }
        else {
            StartStepIfBeyondPlantedFoot((backLegSolver, backDesiredFoothold), frontDesiredFoothold);
        }
    }

    private void StartStepIfBeyondPlantedFoot(
        (MoveTarget solver, Transform desiredFoothold) steppingLeg,
        Transform plantedFoothold) {
        if (Vector2.Distance(strideTarget.position, plantedFoothold.position) <= reachRange) {
            return;
        }

        steppingLeg.desiredFoothold.position = strideTarget.position;
        steppingLeg.solver.LiftOff();
        groundedLegCount -= 1;
    }

    // Eases the body toward the mean x of both desired footholds and the stride target.
    private void CenterBodyHorizontally() {
        Vector2 position = transform.position;
        float anchorMeanX = (frontDesiredFoothold.position.x + backDesiredFoothold.position.x + strideTarget.position.x) / BodyAnchorCount;

        position.x += (anchorMeanX - position.x) * BodyEaseFraction();
        transform.position = position;
    }

    // Eases the body toward the average foot height plus standing height and the current step lift.
    private void MatchBodyHeightToFeet() {
        Vector2 position = transform.position;
        float averageFootY = (frontFootTarget.position.y + backFootTarget.position.y) / 2;
        float targetY = averageFootY + bodyHeight + stepLiftHeight;

        position.y += (targetY - position.y) * BodyEaseFraction();
        transform.position = position;
    }

    private float BodyEaseFraction() => FrameRateEase.FractionForFrame(1f / easeDivisor, Time.deltaTime);
}
