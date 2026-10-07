using UnityEngine;

// What the ground's slope does to the gait at one moment, read for inspection (the debug panel's "Slope gait" section):
// the slope, the legs' extension, the reach that bounds the stance and the stride it allows, the last planned step, the
// pelvis and its reach-ceiling sink, and the recovery counts. Nothing in the gait reads it back.
public readonly struct SlopeGaitReadout {
    public SlopeGaitReadout(
        (float gradient, float direction) ground,
        (SupportStance.Leg leg, float extension, float trailingExtension) support,
        (SlopeStance stance, float stride) reach,
        (float desiredX, Vector2 foothold) plannedStep,
        (float targetY, float ceilingSink) pelvis,
        (float catchUp, int balanceSteps, int resyncs, int extensionReleases) recovery) {
        Gradient = ground.gradient;
        Direction = ground.direction == 0f ? 1f : Mathf.Sign(ground.direction);
        SupportLeg = support.leg;
        SupportExtension = support.extension;
        TrailingExtension = support.trailingExtension;
        Stance = reach.stance;
        Stride = reach.stride;
        PlannedX = plannedStep.desiredX;
        Foothold = plannedStep.foothold;
        PelvisTargetY = pelvis.targetY;
        CeilingSink = pelvis.ceilingSink;
        CatchUp = recovery.catchUp;
        BalanceSteps = recovery.balanceSteps;
        PhaseResyncs = recovery.resyncs;
        ExtensionReleases = recovery.extensionReleases;
    }

    // Rise per unit of x of the ground under the gait (positive rising toward world right) and the direction of motion.
    public float Gradient { get; }
    public float Direction { get; }
    public SupportStance.Leg SupportLeg { get; }
    // Hip-to-foot distance over leg length of the support foot and of the furthest planted foot behind its hip (NaN: none).
    public float SupportExtension { get; }
    public float TrailingExtension { get; }
    public SlopeStance Stance { get; }
    // The stride the gait takes now (two steps), horizontally.
    public float Stride { get; }
    // Where the last gait step aimed (x) and the foothold it took.
    public float PlannedX { get; }
    public Vector2 Foothold { get; }
    public float PelvisTargetY { get; }
    // How far below its gait height the planted legs' reach holds the pelvis.
    public float CeilingSink { get; }
    public float CatchUp { get; }
    public int BalanceSteps { get; }
    public int PhaseResyncs { get; }
    public int ExtensionReleases { get; }

    // Degrees, positive rising toward world right.
    public float Angle => Mathf.Atan(Gradient) * Mathf.Rad2Deg;
    // The ground's normal, and the unit direction along it the body moves.
    public Vector2 Normal => new Vector2(-Gradient, 1f).normalized;
    public Vector2 Tangent => new Vector2(Direction, Gradient * Direction).normalized;
    // Horizontal distances measured along the surface instead.
    public float AlongSurface(float horizontal) => horizontal * Mathf.Sqrt(1f + Gradient * Gradient);
    public float StrideAlongSurface => AlongSurface(Stride);
    public float BoundedStrideAlongSurface => AlongSurface(2f * Stance.Step);
    // How far the foothold landed from the spot the step aimed for, along the surface (positive: further along the motion).
    public float FootholdDisplacementAlongSurface => AlongSurface((Foothold.x - PlannedX) * Direction);
}
