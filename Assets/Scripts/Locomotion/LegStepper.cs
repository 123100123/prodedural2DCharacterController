using UnityEngine;

// Moves one leg's IK solver toward its foot target along a lift-then-settle arc and reports landing to the body.
// Runs after RaycastTarget has corrected the desired foothold this frame.
[DefaultExecutionOrder(20)]
public class LegStepper : MonoBehaviour {
    private const float LiftPhaseFraction = 0.5f;
    private const float LiftHeightFactor = 1.5f;
    private const float SolverEaseShare = 2f;
    private const float BodyLiftShare = 2f;
    private const float ContactToleranceFactor = 0.05f;

    [SerializeField] private StrideController body;
    [SerializeField] private Transform footTarget;
    [SerializeField] private Transform desiredFoothold;
    [SerializeField] private float speed = 6.8f;

    private float stepStartX;
    private bool isGrounded = true;

    public void LiftOff() => isGrounded = false;

    private void Update() {
        RetargetWhenFootholdOutOfReach();
        FollowFootTarget();
        RegisterLandingWhenFootArrives();
    }

    private void RetargetWhenFootholdOutOfReach() {
        if (Vector2.Distance(footTarget.position, desiredFoothold.position) <= body.ReachRange) {
            return;
        }

        footTarget.position = desiredFoothold.position;
        stepStartX = transform.position.x;
    }

    private void FollowFootTarget() {
        float liftHeight = LiftHeightForStepProgress();
        Vector2 liftedTarget = new Vector2(footTarget.position.x, footTarget.position.y + liftHeight);

        MoveSolverToward(liftedTarget);

        if (!isGrounded) {
            body.SetStepLift(liftHeight / BodyLiftShare);
        }
    }

    // Raises the foot during the first part of the step, then lets it settle onto the target.
    private float LiftHeightForStepProgress() {
        float stepProgressX = Mathf.Abs(transform.position.x - stepStartX);

        if (stepProgressX < body.ReachRange * LiftPhaseFraction) {
            return body.ReachRange * LiftHeightFactor;
        }
        return (footTarget.position.y - transform.position.y) / body.EaseDivisor / SolverEaseShare;
    }

    // Combines a constant-speed approach with an ease-out step, both measured from the pre-move position.
    private void MoveSolverToward(Vector2 liftedTarget) {
        float easeFraction = FrameRateEase.FractionForFrame(1f / body.EaseDivisor / SolverEaseShare, Time.deltaTime);
        Vector2 easeStep = (liftedTarget - (Vector2)transform.position) * easeFraction;

        Vector2 approached = Vector2.MoveTowards(transform.position, liftedTarget, speed * Time.deltaTime);
        transform.position = approached + easeStep;
    }

    private void RegisterLandingWhenFootArrives() {
        float distanceToTarget = Vector2.Distance(transform.position, footTarget.position);

        if (distanceToTarget < body.ReachRange * ContactToleranceFactor && !isGrounded) {
            body.RegisterLegLanded();
            isGrounded = true;
        }
    }
}
