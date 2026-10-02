using UnityEngine;

// Pure body motions locked to the gait phase, so the torso moves with the legs. The front leg swings from phase 0 and
// lands at (1 - duty factor); everything that happens twice per stride (once per step) uses twice the phase.
public static class GaitWaves {
    private const float FullTurn = 2f * Mathf.PI;

    public static float FrontLegContact(float dutyFactor) => 1f - dutyFactor;

    // Phase of the body's low point after the front leg lands: just after contact when walking (the knee takes the
    // weight), mid-stance when running (the leg compresses like a spring).
    public static float LowestPhase((float dutyFactor, float lowAfterContact) gait) {
        return Mathf.Repeat(FrontLegContact(gait.dutyFactor) + gait.lowAfterContact, 1f);
    }

    // -1 at the low point, +1 at the high point, twice per stride.
    public static float BodyBob((float phase, float lowestPhase) at) => -Mathf.Cos(2f * FullTurn * (at.phase - at.lowestPhase));

    // Forward body offset: the body is fastest coming through the low point and slowest over the top of each step.
    public static float Surge((float phase, float lowestPhase) at) => Mathf.Sin(2f * FullTurn * (at.phase - at.lowestPhase));

    // +1 flexing forward as the weight is taken, -1 extending upright over the single supporting leg.
    public static float TorsoPitch((float phase, float lowestPhase) at) => Mathf.Cos(2f * FullTurn * (at.phase - at.lowestPhase));

    // +1 when the arm opposite the front leg is fully forward: as that leg lands, delayed by the arm's drag.
    public static float OppositeArmSwing((float phase, float frontLegContact, float lag) at) {
        return Mathf.Cos(FullTurn * (at.phase - at.frontLegContact - at.lag));
    }
}
