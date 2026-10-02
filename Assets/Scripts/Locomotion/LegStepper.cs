using UnityEngine;
using UnityEngine.U2D.IK;

// Moves one leg's IK solver toward its foot target, lifting the foot during the first part of a step.
[RequireComponent(typeof(LimbSolver2D))]
public class LegStepper : MonoBehaviour {
    private const float LiftPhaseFraction = 0.5f;
    private const float LiftHeightFactor = 1.5f;
    private const float SolverEaseShare = 2f;
    private const float BodyLiftShare = 2f;
    private const float ContactToleranceFactor = 0.05f;

    [SerializeField] private Transform footTarget;
    [SerializeField] private float speed = 6.8f;

    private Transform hip;
    private float stepStartX;
    private float stepLength;
    private bool isGrounded = true;

    public bool IsGrounded => isGrounded;
    public Vector2 FootTarget => footTarget.position;
    public Vector2 HipPosition => hip.position;
    public float LegLength { get; private set; }
    public float BodyLift { get; private set; }

    // OnEnable rather than Awake so the chain is found again after a script recompile during play mode.
    private void OnEnable() {
        Transform[] chain = GetComponent<LimbSolver2D>().GetChain(0).transforms;
        hip = chain[0];
        LegLength = Vector2.Distance(chain[0].position, chain[1].position) + Vector2.Distance(chain[1].position, chain[2].position);
    }

    public void BeginStep(Vector2 foothold) {
        stepStartX = transform.position.x;
        stepLength = Mathf.Abs(foothold.x - stepStartX);
        footTarget.position = foothold;
        isGrounded = false;
    }

    // Lands the foot on a foothold without a lift arc, e.g. after a jump.
    public void SettleOnto(Vector2 foothold) {
        BeginStep(foothold);
        stepLength = 0f;
    }

    // Moves the foot freely toward a pose target while the leg carries no weight (airborne, rolling, climbing).
    public void SwingFreelyToward(Vector2 poseTarget, float easeFraction) {
        footTarget.position = poseTarget;
        transform.position = Vector2.Lerp(transform.position, poseTarget, easeFraction);
        isGrounded = false;
        BodyLift = 0f;
    }

    public void TickStep(float deltaTime, (float reachRange, float easeDivisor) gait) {
        float liftHeight = isGrounded ? 0f : LiftHeightForStepProgress(gait);
        MoveSolverToward(new Vector2(footTarget.position.x, footTarget.position.y + liftHeight), (deltaTime, gait.easeDivisor));

        BodyLift = isGrounded ? 0f : liftHeight / BodyLiftShare;
        RegisterLandingWhenFootArrives(gait.reachRange);
    }

    // Raises the foot during the first part of the step, then lets it settle onto the target.
    private float LiftHeightForStepProgress((float reachRange, float easeDivisor) gait) {
        float stepProgressX = Mathf.Abs(transform.position.x - stepStartX);
        float liftPhaseLength = Mathf.Min(gait.reachRange, stepLength) * LiftPhaseFraction;

        if (stepProgressX < liftPhaseLength) {
            return gait.reachRange * LiftHeightFactor;
        }
        return (footTarget.position.y - transform.position.y) / gait.easeDivisor / SolverEaseShare;
    }

    // Combines a constant-speed approach with an ease-out step, both measured from the pre-move position.
    private void MoveSolverToward(Vector2 liftedTarget, (float deltaTime, float easeDivisor) motion) {
        float easeFraction = FrameRateEase.FractionForFrame(1f / motion.easeDivisor / SolverEaseShare, motion.deltaTime);
        Vector2 easeStep = (liftedTarget - (Vector2)transform.position) * easeFraction;

        Vector2 approached = Vector2.MoveTowards(transform.position, liftedTarget, speed * motion.deltaTime);
        transform.position = approached + easeStep;
    }

    private void RegisterLandingWhenFootArrives(float reachRange) {
        float distanceToTarget = Vector2.Distance(transform.position, footTarget.position);

        if (!isGrounded && distanceToTarget < reachRange * ContactToleranceFactor) {
            isGrounded = true;
        }
    }
}
