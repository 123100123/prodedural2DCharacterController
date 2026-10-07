using UnityEngine;

// Pure slope geometry and the slope's effect on the moves. Slopes are described by their gradient (rise per unit of x,
// positive rising toward world right) or by a ground normal; flat ground (gradient 0) changes nothing.
public static class SlopeMath {
    // Surfaces flatter than this (degrees) are flat ground: the gait treats them exactly as before slopes existed.
    public const float MinSlopeAngle = 2f;
    // The steepness the "at 45°" settings are given for.
    private const float ReferenceAngle = 45f;

    // Degrees between the normal and straight up (0 flat, 90 a wall).
    public static float AngleOf(Vector2 normal) => Vector2.Angle(Vector2.up, normal);

    // Ground to walk on: facing up and no steeper than the limit.
    public static bool IsWalkable(Vector2 normal, float maxSlopeAngle) => normal.y > 0f && AngleOf(normal) <= maxSlopeAngle;

    // Walkable and steep enough to count as a slope rather than flat ground.
    public static bool IsSloped(Vector2 normal, float maxSlopeAngle) => IsWalkable(normal, maxSlopeAngle) && AngleOf(normal) > MinSlopeAngle;

    // Rise per unit of x of the surface with this normal (positive: rising toward world right).
    public static float Gradient(Vector2 normal) => normal.y > 1e-4f ? -normal.x / normal.y : 0f;

    // Degrees from the gradient (0 flat, 45 at a gradient of 1).
    public static float AngleOfGradient(float gradient) => Mathf.Atan(Mathf.Abs(gradient)) * Mathf.Rad2Deg;

    // The surface's tilt as seen in the world: positive rising toward world right.
    public static float SurfaceTilt(Vector2 normal) => Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;

    // The rotation (degrees about z) a foot bone under a root with the given x-scale sign takes to lie along the ground:
    // a mirrored root shows a local angle mirrored, so the angle is mirrored back.
    public static float AnkleAngle(Vector2 normal, int mirrorSign) => FootBoneAngle(SurfaceTilt(normal), mirrorSign);

    // The local rotation (degrees about z) that shows a foot tilted by worldTilt under a root with the given x-scale sign.
    public static float FootBoneAngle(float worldTilt, int mirrorSign) => mirrorSign < 0 ? -worldTilt : worldTilt;

    // How steep the slope is relative to 45°, 0 on flat ground, at most 1.
    public static float Steepness(float gradient) => Mathf.Clamp01(AngleOfGradient(gradient) / ReferenceAngle);

    // Unit direction along the surface for travel toward direction (+1 right, -1 left).
    public static Vector2 AlongSurface(float gradient, int direction) => new Vector2(direction, gradient * direction).normalized;

    // The speed a running slide has after a frame of the slope's pull: gaining going down, losing going up. Downhill the
    // pull stops at the slide's top speed (a faster slide keeps its own speed); uphill it brakes down to a stop.
    public static float SlideSpeed(float speed, (float gradient, int direction, float deltaTime) motion, SlopeSettings settings) {
        if (motion.gradient == 0f) {
            return speed;
        }
        float sine = Mathf.Sin(Mathf.Atan(Mathf.Abs(motion.gradient)));
        float pull = (motion.gradient * motion.direction < 0f ? 1f : -1f) * settings.slideSlopeAcceleration * sine * motion.deltaTime;
        float pulled = speed + pull;
        return pull > 0f ? Mathf.Min(pulled, Mathf.Max(speed, settings.slideMaxSpeed)) : Mathf.Max(0f, pulled);
    }
}

// The slope under the gait as the gait sees it: the gradient, the settings that shape walking on it, and the legs whose
// reach bounds the stance on it (SlopeReach). The default is flat ground with no legs known, which leaves every gait value
// as it is.
public readonly struct SlopeGait {
    public SlopeGait(float gradient, SlopeSettings settings) : this(gradient, (settings.uphillSpeedFactor, settings.downhillSpeedFactor)) {
    }

    public SlopeGait(float gradient, (float uphill, float downhill) factors) {
        Gradient = gradient;
        UphillFactor = factors.uphill;
        DownhillFactor = factors.downhill;
        Legs = default;
    }

    private SlopeGait(SlopeGait slope, LegReach legs) {
        Gradient = slope.Gradient;
        UphillFactor = slope.UphillFactor;
        DownhillFactor = slope.DownhillFactor;
        Legs = legs;
    }

    public static SlopeGait Flat => default;

    public float Gradient { get; }
    public float UphillFactor { get; }
    public float DownhillFactor { get; }
    // The legs the stance is fitted to; unknown (default) leaves the steps as the gait has them.
    public LegReach Legs { get; }
    public bool IsFlat => Gradient == 0f;

    // The same slope with the legs whose reach bounds the stance on it.
    public SlopeGait Reaching(LegReach legs) => new SlopeGait(this, legs);

    // Horizontal ground speed on the slope for each unit of flat-ground speed, moving toward directionSign: the cosine of
    // the slope (the surface is longer than its run) times the uphill or downhill factor, which grows with steepness.
    public float SpeedScale(float directionSign) {
        if (IsFlat || directionSign == 0f) {
            return 1f;
        }
        float angle = SlopeMath.AngleOfGradient(Gradient);
        bool uphill = Gradient * directionSign > 0f;
        float factor = Mathf.Lerp(1f, uphill ? UphillFactor : DownhillFactor, SlopeMath.Steepness(Gradient));
        return Mathf.Max(0.05f, Mathf.Cos(angle * Mathf.Deg2Rad) * factor);
    }

    // How the gait's stance at a speed fits the legs on this slope (SlopeReach.Fit). Flat ground fits the same way: the
    // flat gait is designed within the legs' reach, so there the bound leaves the steps as they are.
    public SlopeStance StanceOf(GaitProfile gait, float speed) => SlopeReach.Fit((gait, speed), Gradient, Legs);

    // The gait with its steps no longer than the legs reach on this slope (with the pelvis as low as the stance fitted at
    // this speed rides it): a cap rather than a share, so the gait keeps its own steps wherever they are shorter.
    public GaitProfile Bound(GaitProfile gait, float speed) {
        if (!Legs.IsKnown) {
            return gait;
        }
        float cap = StanceOf(gait, speed).StepCap;
        gait.maxStepLength = Mathf.Min(gait.maxStepLength, cap);
        gait.minStepLength = Mathf.Min(gait.minStepLength, cap);
        return gait;
    }
}
