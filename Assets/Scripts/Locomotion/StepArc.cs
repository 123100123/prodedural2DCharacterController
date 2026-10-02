using System;
using UnityEngine;

// Shape of one step over normalized progress: how the foot travels and lifts, ending in a firm plant rather than a glide.
[Serializable]
public class StepArcShape {
    [Tooltip("Share of the horizontal distance covered over step progress. Ends with a non-zero slope so the foot arrives with speed.")]
    public AnimationCurve travel = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(0.55f, 0.6f, 1.5f, 1.5f), new Keyframe(1f, 1f, 0.6f, 0f));
    [Tooltip("Share of the lift height over step progress: a quick lift, then the foot accelerates down into the plant.")]
    public AnimationCurve lift = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.4f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3.5f, 0f));
    [Tooltip("Foot clearance above the higher end of the step.")]
    public float liftHeight = 0.55f;
    [Tooltip("Foot speed along the ground while stepping; sets the step duration.")]
    public float stepSpeed = 9f;
    public float minStepDuration = 0.16f;
    public float maxStepDuration = 0.4f;

    public float DurationFor(float distance) => Mathf.Clamp(distance / Mathf.Max(0.01f, stepSpeed), minStepDuration, maxStepDuration);
}

// Pure step evaluation: where the foot is at a given step progress.
public static class StepArc {
    // The lift is measured from the straight path up to a fixed clearance above the higher end, so step-ups are cleared.
    public static Vector2 Evaluate((Vector2 start, Vector2 end, float progress) step, StepArcShape shape) {
        float t = Mathf.Clamp01(step.progress);
        float travel = shape.travel.Evaluate(t);
        Vector2 onPath = Vector2.LerpUnclamped(step.start, step.end, travel);

        float arcTop = Mathf.Max(step.start.y, step.end.y) + shape.liftHeight;
        float lift = Mathf.Max(0f, shape.lift.Evaluate(t));
        return new Vector2(onPath.x, Mathf.Lerp(onPath.y, Mathf.Max(onPath.y, arcTop), lift));
    }
}
