using UnityEngine;

// Pure forward lean of the locomotion posture, in degrees: a base lean that grows from the idle lean to the gait's lean
// with the pace (the run's lean is stronger than the walk's), plus a lean into acceleration, within a cap.
public static class PostureLean {
    // gaitLean: the walk/run blend's lean; moving: the pace from standing (0) to walking speed and above (1).
    public static float BaseLean((float idleLean, float gaitLean, float moving) pace) {
        return Mathf.Lerp(pace.idleLean, pace.gaitLean, Mathf.Clamp01(pace.moving));
    }

    // Forward acceleration leans the body into it; braking rocks it back.
    public static float AccelerationLean(float forwardAcceleration, PostureSettings settings) {
        return Mathf.Clamp(forwardAcceleration * settings.leanPerAcceleration, -settings.maxAccelerationLean, settings.maxAccelerationLean);
    }

    public static float Total((float baseLean, float accelerationLean) lean, PostureSettings settings) {
        return Mathf.Clamp(lean.baseLean + lean.accelerationLean, -settings.maxTotalLean, settings.maxTotalLean);
    }
}
