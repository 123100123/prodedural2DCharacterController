using UnityEngine;
using UnityEngine.U2D.IK;

// Moves one leg's IK solver: steps along a StepArc as its swing progresses and plants firmly, or swings freely
// while the leg carries no weight (airborne, climbing, rolling). A planted foot turns to lie along the ground it stands
// on and turns back to level while it is lifted: the IK target is rotated before the IK solves (the solver constrains the
// effector, the foot bone, to its target's rotation).
[RequireComponent(typeof(LimbSolver2D))]
public class LegStepper : MonoBehaviour {
    private const float SettleDuration = 0.12f;

    [SerializeField] private Transform footTarget;

    private readonly SwingProgress swing = new SwingProgress();
    private Transform hip;
    // The solver's target and its effector (the foot bone, which takes the target's rotation).
    private Transform ikTarget;
    private Transform effector;
    // The foot's turn toward the ground, in degrees as seen in the world (positive rising to the right).
    private float ankleTilt;
    // The leg swings freely (airborne, rolling, climbing): its foot is not stepping onto any ground.
    private bool swingsFreely;
    private (Vector2 start, Vector2 end) step;
    // Lift height and how much the gait was running when the step started; the step keeps its shape to the end.
    private (float liftHeight, float runShare) stepArc;
    private Vector2 previousSolverPosition;
    // Downward foot speed at the moment of the last plant; read once through TryConsumePlant.
    private float? pendingPlantImpact;

    public bool IsGrounded => swing.IsGrounded;
    public Vector2 FootTarget => footTarget.position;
    // The world-space target transforms the IK chases (for rig validation): the planted foothold and the solver target.
    public (Transform foothold, Transform solver) WorldSpaceTargets => (footTarget, transform);
    public Vector2 HipPosition => hip.position;
    public float LegLength { get; private set; }
    // The normal of the ground the foot stands on or is stepping onto (up on flat ground), set by the gait.
    public Vector2 GroundNormal { get; set; } = Vector2.up;
    // How far a planted foot follows the ground (0 level, 1 along it) and how long it takes to turn (seconds).
    public (float weight, float blendTime) AnkleBlend { get; set; } = (1f, 0.08f);
    // The foot's current turn toward the ground in world degrees (positive rising to the right), and the foot bone itself.
    public float AnkleTilt => ankleTilt;
    public Transform FootBone => effector;

    // Normalized progress of the current step; 0 while planted.
    public float StepProgress => swing.Progress;
    // The other foot may lift while this one is planted or far enough through its swing.
    public bool LetsOtherLegLift => swing.LetsOtherLegLift;
    public SwingTiming SwingTiming => swing.Timing;
    // A gait step in the air, timed by the gait clock (not settling, not swinging freely).
    public bool IsSwingingOnClock => !swing.IsGrounded && swing.Timing.IsOnClock && swing.Progress < 1f;

    // OnEnable rather than Awake so the chain is found again after a script recompile during play mode.
    private void OnEnable() {
        IKChain2D ikChain = GetComponent<LimbSolver2D>().GetChain(0);
        Transform[] chain = ikChain.transforms;
        hip = chain[0];
        ikTarget = ikChain.target;
        effector = ikChain.effector;
        LegLength = Vector2.Distance(chain[0].position, chain[1].position) + Vector2.Distance(chain[1].position, chain[2].position);
        previousSolverPosition = transform.position;
    }

    // Share of a step's progress after which the foot starts turning toward the ground it lands on.
    private const float AnkleTurnInStart = 0.45f;

    // After the state machine has moved the feet and before the IK solves (in LateUpdate): the foot turns toward the
    // ground while planted, back toward level early in a step and onto the landing ground late in it (so it touches down
    // along the slope), and stays level while swinging freely. The target rotation is mirrored back under a mirrored root.
    private void Update() {
        float groundTilt = SlopeMath.SurfaceTilt(GroundNormal) * Mathf.Clamp01(AnkleBlend.weight);
        float landingShare = swingsFreely ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AnkleTurnInStart, 1f, swing.Progress));
        float target = IsGrounded ? groundTilt : groundTilt * landingShare;
        float blendTime = Mathf.Max(0.001f, AnkleBlend.blendTime);
        ankleTilt = Mathf.Lerp(ankleTilt, target, 1f - Mathf.Exp(-Time.deltaTime / blendTime));
        if (ikTarget != null) {
            int mirrorSign = transform.lossyScale.x < 0f ? -1 : 1;
            ikTarget.localRotation = Quaternion.Euler(0f, 0f, SlopeMath.FootBoneAngle(ankleTilt, mirrorSign));
        }
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
        swingsFreely = true;
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
        swingsFreely = false;
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
