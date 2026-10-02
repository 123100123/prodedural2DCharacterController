using UnityEngine;
using UnityEngine.U2D.IK;

// Moves one leg's IK solver: steps along a StepArc as its swing progresses and plants firmly, or swings freely
// while the leg carries no weight (airborne, climbing, rolling).
[RequireComponent(typeof(LimbSolver2D))]
public class LegStepper : MonoBehaviour {
    private const float SettleDuration = 0.12f;

    [SerializeField] private Transform footTarget;

    private readonly SwingProgress swing = new SwingProgress();
    private Transform hip;
    private (Vector2 start, Vector2 end) step;
    // Lift height and how much the gait was running when the step started; the step keeps its shape to the end.
    private (float liftHeight, float runShare) stepArc;
    private Vector2 previousSolverPosition;
    // Downward foot speed at the moment of the last plant; read once through TryConsumePlant.
    private float? pendingPlantImpact;

    public bool IsGrounded => swing.IsGrounded;
    public Vector2 FootTarget => footTarget.position;
    public Vector2 HipPosition => hip.position;
    public float LegLength { get; private set; }

    // Normalized progress of the current step; 0 while planted.
    public float StepProgress => swing.Progress;
    // The other foot may lift while this one is planted or far enough through its swing.
    public bool LetsOtherLegLift => swing.LetsOtherLegLift;
    public SwingTiming SwingTiming => swing.Timing;
    // A gait step in the air, timed by the gait clock (not settling, not swinging freely).
    public bool IsSwingingOnClock => !swing.IsGrounded && swing.Timing.IsOnClock && swing.Progress < 1f;

    // OnEnable rather than Awake so the chain is found again after a script recompile during play mode.
    private void OnEnable() {
        Transform[] chain = GetComponent<LimbSolver2D>().GetChain(0).transforms;
        hip = chain[0];
        LegLength = Vector2.Distance(chain[0].position, chain[1].position) + Vector2.Distance(chain[1].position, chain[2].position);
        previousSolverPosition = transform.position;
    }

    public void BeginStep((Vector2 foothold, SwingTiming timing) plannedStep, (float liftHeight, float runShare) arc) {
        StartStepFromCurrentPose(plannedStep);
        stepArc = arc;
    }

    // Lands the step in the air on a new foothold, e.g. when the character stops mid-stride. The path is re-anchored so the
    // foot carries on from where it is now rather than jumping to the new path.
    public void RetargetStep((Vector2 foothold, SwingTiming timing) replanned, StepArcShape shape) {
        float travel = shape.Evaluate((swing.Progress, stepArc.runShare)).travel;
        Vector2 onPath = Vector2.LerpUnclamped(step.start, step.end, travel);
        Vector2 start = travel < 0.99f ? (onPath - travel * replanned.foothold) / (1f - travel) : step.start;
        step = (start, replanned.foothold);
        footTarget.position = replanned.foothold;
        swing.Retime(replanned.timing);
    }

    // Lands the foot on a foothold without a lift arc, e.g. after a jump.
    public void SettleOnto(Vector2 foothold) {
        StartStepFromCurrentPose((foothold, SwingTiming.Timed(SettleDuration)));
        stepArc = (0f, 0f);
    }

    // Moves the foot freely toward a pose target while the leg carries no weight (airborne, rolling, climbing).
    public void SwingFreelyToward(Vector2 poseTarget, float easeFraction) {
        footTarget.position = poseTarget;
        transform.position = Vector2.Lerp(transform.position, poseTarget, easeFraction);
        swing.Release();
        previousSolverPosition = transform.position;
    }

    // Advances the swing by the gait clock's swing progress this tick (or by time alone, for steps off the clock).
    public void TickStep((float deltaTime, float clockProgress) advance, StepArcShape shape) {
        if (swing.IsGrounded) {
            transform.position = footTarget.position;
            return;
        }

        bool landed = swing.Advance((advance.clockProgress, advance.deltaTime));
        previousSolverPosition = transform.position;
        transform.position = FootPositionAtProgress(shape);

        if (landed) {
            Plant(advance.deltaTime);
        }
    }

    public bool TryConsumePlant(out float impactSpeed) {
        impactSpeed = pendingPlantImpact ?? 0f;
        bool hasPlanted = pendingPlantImpact.HasValue;
        pendingPlantImpact = null;
        return hasPlanted;
    }

    private void StartStepFromCurrentPose((Vector2 foothold, SwingTiming timing) plannedStep) {
        step = (transform.position, plannedStep.foothold);
        footTarget.position = plannedStep.foothold;
        swing.Begin(plannedStep.timing);
    }

    private Vector2 FootPositionAtProgress(StepArcShape shape) {
        float progress = swing.IsGrounded ? 1f : swing.Progress;
        if (stepArc.liftHeight <= 0f) {
            return Vector2.Lerp(step.start, step.end, Mathf.SmoothStep(0f, 1f, progress));
        }
        return StepArc.Evaluate((step.start, step.end, progress), (shape, stepArc.liftHeight, stepArc.runShare));
    }

    private void Plant(float deltaTime) {
        float downwardSpeed = deltaTime > 0f ? Mathf.Max(0f, (previousSolverPosition.y - transform.position.y) / deltaTime) : 0f;
        transform.position = step.end;
        pendingPlantImpact = downwardSpeed;
    }
}
