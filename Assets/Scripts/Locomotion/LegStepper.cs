using UnityEngine;
using UnityEngine.U2D.IK;

// Moves one leg's IK solver: steps along a StepArc over a timed progress and plants firmly, or swings freely
// while the leg carries no weight (airborne, climbing, rolling).
[RequireComponent(typeof(LimbSolver2D))]
public class LegStepper : MonoBehaviour {
    private const float SettleDuration = 0.12f;

    [SerializeField] private Transform footTarget;

    private Transform hip;
    private (Vector2 start, Vector2 end, float duration) step;
    // Lift height and how much the gait was running when the step started; the step keeps its shape to the end.
    private (float liftHeight, float runShare) stepArc;
    private float stepProgress = 1f;
    private bool isGrounded = true;
    private Vector2 previousSolverPosition;
    // Downward foot speed at the moment of the last plant; read once through TryConsumePlant.
    private float? pendingPlantImpact;

    public bool IsGrounded => isGrounded;
    public Vector2 FootTarget => footTarget.position;
    public Vector2 HipPosition => hip.position;
    public float LegLength { get; private set; }

    // Normalized progress of the current step; 0 while planted.
    public float StepProgress => isGrounded ? 0f : stepProgress;

    // OnEnable rather than Awake so the chain is found again after a script recompile during play mode.
    private void OnEnable() {
        Transform[] chain = GetComponent<LimbSolver2D>().GetChain(0).transforms;
        hip = chain[0];
        LegLength = Vector2.Distance(chain[0].position, chain[1].position) + Vector2.Distance(chain[1].position, chain[2].position);
        previousSolverPosition = transform.position;
    }

    public void BeginStep((Vector2 foothold, float duration) plannedStep, (float liftHeight, float runShare) arc) {
        StartStepFromCurrentPose(plannedStep);
        stepArc = arc;
    }

    // Lands the foot on a foothold without a lift arc, e.g. after a jump.
    public void SettleOnto(Vector2 foothold) {
        StartStepFromCurrentPose((foothold, SettleDuration));
        stepArc = (0f, 0f);
    }

    // Moves the foot freely toward a pose target while the leg carries no weight (airborne, rolling, climbing).
    public void SwingFreelyToward(Vector2 poseTarget, float easeFraction) {
        footTarget.position = poseTarget;
        transform.position = Vector2.Lerp(transform.position, poseTarget, easeFraction);
        isGrounded = false;
        stepProgress = 1f;
        previousSolverPosition = transform.position;
    }

    public void TickStep(float deltaTime, StepArcShape shape) {
        if (isGrounded) {
            transform.position = footTarget.position;
            return;
        }

        stepProgress = Mathf.Min(1f, stepProgress + deltaTime / step.duration);
        previousSolverPosition = transform.position;
        transform.position = FootPositionAtProgress(shape);

        if (stepProgress >= 1f) {
            Plant(deltaTime);
        }
    }

    public bool TryConsumePlant(out float impactSpeed) {
        impactSpeed = pendingPlantImpact ?? 0f;
        bool hasPlanted = pendingPlantImpact.HasValue;
        pendingPlantImpact = null;
        return hasPlanted;
    }

    private void StartStepFromCurrentPose((Vector2 foothold, float duration) plannedStep) {
        step = (transform.position, plannedStep.foothold, Mathf.Max(0.01f, plannedStep.duration));
        stepProgress = 0f;
        footTarget.position = plannedStep.foothold;
        isGrounded = false;
    }

    private Vector2 FootPositionAtProgress(StepArcShape shape) {
        if (stepArc.liftHeight <= 0f) {
            return Vector2.Lerp(step.start, step.end, Mathf.SmoothStep(0f, 1f, stepProgress));
        }
        return StepArc.Evaluate((step.start, step.end, stepProgress), (shape, stepArc.liftHeight, stepArc.runShare));
    }

    private void Plant(float deltaTime) {
        float downwardSpeed = deltaTime > 0f ? Mathf.Max(0f, (previousSolverPosition.y - transform.position.y) / deltaTime) : 0f;
        transform.position = step.end;
        isGrounded = true;
        pendingPlantImpact = downwardSpeed;
    }
}
