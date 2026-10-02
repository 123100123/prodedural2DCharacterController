using UnityEngine;

// One clock for both legs: a phase in [0, 1) advanced by the distance travelled over a stride. The front leg lifts at
// phase 0 and the back leg half a cycle later, so steps always alternate evenly instead of drifting into a limp.
public sealed class GaitCycle {
    public const float FrontLegLiftPhase = 0f;
    public const float BackLegLiftPhase = 0.5f;
    private const float PrimeMargin = 0.0001f;

    public float Phase { get; private set; }

    // Advances by the given share of a stride and reports which legs reached their lift phase.
    public (bool frontLeg, bool backLeg) Advance(float strides) {
        float start = Phase;
        float end = start + Mathf.Max(0f, strides);
        Phase = Mathf.Repeat(end, 1f);
        return (HasCrossed((start, end), FrontLegLiftPhase), HasCrossed((start, end), BackLegLiftPhase));
    }

    // Puts the cycle exactly at the given leg's lift, after that leg was made to step (e.g. a balance step), so the
    // other leg follows half a cycle later and the alternation stays even.
    public void SyncToLift(bool frontLeg) => Phase = frontLeg ? FrontLegLiftPhase : BackLegLiftPhase;

    // Puts the cycle just before the given leg's lift, so that leg takes the first step when motion starts.
    public void PrimeToLift(bool frontLeg) {
        float liftPhase = frontLeg ? FrontLegLiftPhase : BackLegLiftPhase;
        Phase = Mathf.Repeat(liftPhase - PrimeMargin, 1f);
    }

    private static bool HasCrossed((float start, float end) span, float liftPhase) {
        return Mathf.FloorToInt(span.end - liftPhase) > Mathf.FloorToInt(span.start - liftPhase);
    }
}
