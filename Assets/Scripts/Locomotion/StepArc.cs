using System;
using UnityEngine;

// Shape of one step over normalized progress: how the foot travels and lifts, ending in a firm plant rather than a glide.
[Serializable]
public class StepArcShape {
    [Tooltip("Share of the horizontal distance covered over step progress: the foot speeds up through mid-swing and comes to rest as it touches down.")]
    public AnimationCurve travel = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Share of the lift height over step progress: the toe clears early in the swing, then the foot lowers gently to the ground.")]
    public AnimationCurve lift = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.32f, 1f, 0f, 0f), new Keyframe(1f, 0f, -1.2f, 0f));
    [Header("Settling steps (small steps into a standing stance)")]
    public float settleLiftHeight = 0.25f;
    public float settleStepDuration = 0.22f;
}

// Pure step evaluation: where the foot is at a given step progress.
public static class StepArc {
    // The lift is measured from the straight path up to a clearance above the higher end, so step-ups are cleared.
    public static Vector2 Evaluate((Vector2 start, Vector2 end, float progress) step, (StepArcShape shape, float liftHeight) arc) {
        StepArcShape shape = arc.shape;
        float t = Mathf.Clamp01(step.progress);
        float travel = shape.travel.Evaluate(t);
        Vector2 onPath = Vector2.LerpUnclamped(step.start, step.end, travel);

        float arcTop = Mathf.Max(step.start.y, step.end.y) + arc.liftHeight;
        float lift = Mathf.Max(0f, shape.lift.Evaluate(t));
        return new Vector2(onPath.x, Mathf.Lerp(onPath.y, Mathf.Max(onPath.y, arcTop), lift));
    }
}
