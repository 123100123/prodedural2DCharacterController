using UnityEngine;

// The legs' geometry the gait is bounded by: how high the hip stands above the ground under it, how far a supporting leg
// reaches (its length times the extension it may straighten to), how far the pelvis may sink below its gait height to
// keep a pushing-off foot (the reach reserve), and how much further it may be lowered before the crouch limit.
public readonly struct LegReach {
    public LegReach((float hipHeight, float reach) leg, (float reserve, float maxDrop) sink) {
        HipHeight = leg.hipHeight;
        Reach = leg.reach;
        Reserve = Mathf.Max(0f, sink.reserve);
        MaxDrop = Mathf.Max(0f, sink.maxDrop);
    }

    public float HipHeight { get; }
    public float Reach { get; }
    public float Reserve { get; }
    public float MaxDrop { get; }
    public bool IsKnown => Reach > 0f && HipHeight > 0f;
}

// How a gait's stance fits the legs on a slope (see SlopeReach.Fit): the step it may take, how far the pelvis rides lower
// to reach it, and the reach behind and ahead of the hip that bounded it.
public readonly struct SlopeStance {
    public SlopeStance((float nominal, float bounded, float cap) step, (float maxLag, float maxLead) reach, float drop) {
        NominalStep = step.nominal;
        Step = step.bounded;
        StepCap = step.cap;
        MaxLag = reach.maxLag;
        MaxLead = reach.maxLead;
        Drop = drop;
    }

    public static SlopeStance Unbounded(float step) => new SlopeStance((step, step, float.PositiveInfinity), (float.PositiveInfinity, float.PositiveInfinity), 0f);

    // The step the gait would take at this speed with nothing bounding it, and the step it takes here.
    public float NominalStep { get; }
    public float Step { get; }
    // The longest step the legs reach with the pelvis at this drop, whatever the speed: the gait's steps are capped by it,
    // so a step timed for another speed (a swing timed for the speed the body is heading for) is bounded the same way.
    public float StepCap { get; }
    // How far behind the hip a planted foot may push off and how far ahead of it a foot may land (horizontal).
    public float MaxLag { get; }
    public float MaxLead { get; }
    // How much lower than its gait height the pelvis rides so the legs reach the step: 0 unless the reach would make the
    // step shorter than its swing can be timed.
    public float Drop { get; }
    public bool IsBounded => Step < NominalStep;
}

// Pure reach geometry of a planted leg on a slope. Distances are horizontal, measured from the hip; heights from the ground
// under the hip. A foot d behind (or ahead of) the hip on ground of signed gradient g (positive where the foot is lower
// than the ground under the hip) is d * g below it, so it is reachable while sqrt(d^2 + (H + g d)^2) <= reach. Flat
// ground is g = 0: sqrt(reach^2 - H^2).
public static class SlopeReach {
    // Bisection steps finding the pelvis drop: the drop is then known to about a ten-thousandth of the crouch depth.
    private const int DropSearchSteps = 14;

    // The largest horizontal distance behind the hip at which a planted foot on ground of signed gradient g (positive: the
    // foot is lower than the ground under the hip, a trailing foot going uphill) is still within reach of a hip H above
    // the ground under it. 0 when the foot is out of reach even under the hip.
    public static float MaxLag(float hipHeight, float gradient, float reach) {
        float slopeSquared = 1f + gradient * gradient;
        float discriminant = reach * reach * slopeSquared - hipHeight * hipHeight;
        if (discriminant < 0f) {
            return 0f;
        }
        return Mathf.Max(0f, (Mathf.Sqrt(discriminant) - gradient * hipHeight) / slopeSquared);
    }

    // The same reach ahead of the hip for a landing foot: gradient is positive where the landing foot is lower (a step
    // going downhill).
    public static float MaxLead(float hipHeight, float gradient, float reach) => MaxLag(hipHeight, gradient, reach);

    // The signed gradients behind and ahead of the hip for a body moving toward direction (+1 right, -1 left) on ground of
    // the given gradient (rise per unit of x toward world right): going uphill the trailing foot is lower (positive) and
    // the leading one higher; going downhill the other way round.
    public static (float trailing, float leading) SideGradients(float slopeGradient, float direction) {
        return (slopeGradient * direction, -slopeGradient * direction);
    }

    // How far the hip must sink below hipHeight for a planted foot the given horizontal distance from the hip and depth
    // below the ground under it to be within reach; 0 when it already is. A foot further away than the reach cannot be
    // reached at any height: the hip would have to be at the foot's own level.
    public static float Sink((float horizontal, float depth) foot, (float hipHeight, float reach) leg) {
        float horizontal = Mathf.Abs(foot.horizontal);
        float hipAboveFoot = leg.hipHeight + foot.depth;
        if (horizontal >= leg.reach) {
            return Mathf.Max(0f, hipAboveFoot);
        }
        return Mathf.Max(0f, hipAboveFoot - Mathf.Sqrt(leg.reach * leg.reach - horizontal * horizontal));
    }

    // Hip-to-foot distance as a share of the leg's length (1: straight).
    public static float Extension(Vector2 hip, Vector2 foot, float legLength) => legLength > 0f ? Vector2.Distance(hip, foot) / legLength : 0f;

    // The longest step whose stance keeps the push-off within maxLag and the landing within maxLead: the stance travel
    // (dutyFactor of the stride, two steps) splits contactLeadShare ahead of the hip and the rest behind it.
    public static float StepBound((float maxLag, float maxLead) reach, (float dutyFactor, float contactLeadShare) gait) {
        float lead = Mathf.Clamp(gait.contactLeadShare, 0.01f, 0.99f);
        float stanceTravel = Mathf.Min(reach.maxLag / (1f - lead), reach.maxLead / lead);
        return stanceTravel / (2f * Mathf.Max(0.01f, gait.dutyFactor));
    }

    // The hip's height over the ground under it when the leading foot plants and when the trailing foot pushes off: the
    // standing hip height less the gait's pelvis drop, with the body's bob at those moments of the cycle (the front leg
    // lands at 1 - duty factor and pushes off at 0). A push-off foot may pull the pelvis down by the reach reserve; a
    // landing foot may not (the pelvis would drop the moment it plants), so its height is the gait's own.
    public static (float pushOff, float contact) StanceHipHeights(GaitProfile gait, LegReach leg) {
        float lowest = GaitWaves.LowestPhase((gait.dutyFactor, gait.lowAfterContact));
        float pushOffBob = GaitWaves.BodyBob((0f, lowest)) * gait.bobAmplitude;
        float contactBob = GaitWaves.BodyBob((GaitWaves.FrontLegContact(gait.dutyFactor), lowest)) * gait.bobAmplitude;
        float standing = leg.HipHeight - gait.pelvisDrop;
        return (standing + pushOffBob - leg.Reserve, standing + contactBob);
    }

    // Fits the gait's stance at a speed (signed: positive toward world right) to the legs on ground of the given gradient.
    // The step is the gait's own unless the reach behind or ahead of the hip bounds it; the cadence then rises on its own
    // (the cycle is the stride over the speed). Only when that bound would make the step shorter than its swing can be
    // timed (the gait's shortest swing over its swing share) does the pelvis ride lower, just far enough for the legs to
    // reach the shortest such step, and never past the crouch limit.
    public static SlopeStance Fit((GaitProfile gait, float speed) at, float gradient, LegReach leg) {
        GaitProfile gait = at.gait;
        float nominal = GaitTiming.StepLength(at.speed, gait);
        if (!leg.IsKnown) {
            return SlopeStance.Unbounded(nominal);
        }
        (float pushOff, float contact) heights = StanceHipHeights(gait, leg);
        float direction = Mathf.Sign(at.speed);
        // Standing still, either foot may lead: both sides are held to the steeper case.
        (float trailing, float leading) sides = direction == 0f ? (Mathf.Abs(gradient), Mathf.Abs(gradient)) : SideGradients(gradient, direction);
        (float maxLag, float maxLead) reach = ReachAt(0f, (heights, sides, leg.Reach));
        float bound = StepBound(reach, (gait.dutyFactor, gait.contactLeadShare));
        float shortestTimedStep = Mathf.Abs(at.speed) * gait.minSwingDuration / (2f * Mathf.Max(0.01f, 1f - gait.dutyFactor));
        float wanted = Mathf.Min(nominal, shortestTimedStep);
        if (bound >= wanted) {
            return new SlopeStance((nominal, Mathf.Min(nominal, bound), bound), reach, 0f);
        }

        (float low, float high) drop = (0f, leg.MaxDrop);
        (float maxLag, float maxLead) deepest = ReachAt(drop.high, (heights, sides, leg.Reach));
        float deepestBound = StepBound(deepest, (gait.dutyFactor, gait.contactLeadShare));
        if (deepestBound <= wanted) {
            return new SlopeStance((nominal, deepestBound, deepestBound), deepest, drop.high);
        }
        for (int step = 0; step < DropSearchSteps; step++) {
            float middle = (drop.low + drop.high) / 2f;
            bool reaches = StepBound(ReachAt(middle, (heights, sides, leg.Reach)), (gait.dutyFactor, gait.contactLeadShare)) >= wanted;
            drop = reaches ? (drop.low, middle) : (middle, drop.high);
        }
        (float maxLag, float maxLead) dropped = ReachAt(drop.high, (heights, sides, leg.Reach));
        return new SlopeStance((nominal, wanted, StepBound(dropped, (gait.dutyFactor, gait.contactLeadShare))), dropped, drop.high);
    }

    private static (float maxLag, float maxLead) ReachAt(float drop, ((float pushOff, float contact) heights, (float trailing, float leading) sides, float reach) leg) {
        return (MaxLag(leg.heights.pushOff - drop, leg.sides.trailing, leg.reach), MaxLead(leg.heights.contact - drop, leg.sides.leading, leg.reach));
    }
}
