using UnityEngine;

// Pure gait timing from speed. People keep step length over cadence roughly constant (the walk ratio), so both grow with
// the square root of speed: short, slow steps when starting or stopping, longer and quicker ones at full speed.
public static class GaitTiming {
    private const float MinSpeed = 0.05f;

    public static float StepLength(float speed, GaitProfile gait) {
        float natural = gait.stepLengthPerRootSpeed * Mathf.Sqrt(Mathf.Abs(speed));
        return Mathf.Clamp(natural, gait.minStepLength, gait.maxStepLength);
    }

    // One full cycle covers two steps (a stride).
    public static float StrideLength(float speed, GaitProfile gait) => 2f * StepLength(speed, gait);

    public static float CyclePeriod(float speed, GaitProfile gait) => StrideLength(speed, gait) / Mathf.Max(MinSpeed, Mathf.Abs(speed));

    // Each foot is in the air for the share of the cycle that is not stance.
    public static float SwingDuration(float speed, GaitProfile gait) {
        float swing = (1f - gait.dutyFactor) * CyclePeriod(speed, gait);
        return Mathf.Clamp(swing, gait.minSwingDuration, gait.maxSwingDuration);
    }

    // While planted, the body travels a duty share of the stride over the foot; landing half that ahead of the hip
    // centres the stance under the body.
    public static float LandingLead(float speed, GaitProfile gait) => gait.dutyFactor * StepLength(speed, gait);

    // How far ahead of the hip a step that starts now lands: the hip's travel during the swing plus the landing lead.
    public static float LandingReach(float speed, GaitProfile gait) => Mathf.Abs(speed) * SwingDuration(speed, gait) + LandingLead(speed, gait);
}
