using System;
using UnityEngine;

// How the foot travels and lifts over one step's normalized progress.
[Serializable]
public class StepCurves {
    [Tooltip("Share of the horizontal distance covered over step progress.")]
    public AnimationCurve travel;
    [Tooltip("Share of the lift height over step progress.")]
    public AnimationCurve lift;

    // The toe clears early, the foot passes under the hip near the top of the swing, then lowers gently and comes to
    // rest as it touches down.
    public static StepCurves Walk => new StepCurves {
        travel = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.3f), new Keyframe(1f, 1f, 0f, 0f)),
        lift = new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.38f, 1f, 0f, 0f), new Keyframe(1f, 0f, -1.2f, 0f)),
    };

    // The foot follows the body forward from push-off and stays low until it is under the hip, so the shin never folds
    // back flat behind the body; it rises with the knee drive and comes down hard into the plant.
    public static StepCurves Run => new StepCurves {
        travel = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.8f), new Keyframe(0.5f, 0.58f, 1.4f, 1.4f), new Keyframe(1f, 1f, 0.5f, 0f)),
        lift = new AnimationCurve(new Keyframe(0f, 0f, 0f, 1.2f), new Keyframe(0.55f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3.5f, 0f)),
    };
}

// Shape of the steps: walking and running curves, blended by how much the gait is running when a step starts.
[Serializable]
public class StepArcShape {
    public StepCurves walk = StepCurves.Walk;
    public StepCurves run = StepCurves.Run;

    // Shares of the distance covered and of the lift height at a step progress.
    public (float travel, float lift) Evaluate((float progress, float runShare) at) {
        float travel = Mathf.Lerp(walk.travel.Evaluate(at.progress), run.travel.Evaluate(at.progress), at.runShare);
        float lift = Mathf.Lerp(walk.lift.Evaluate(at.progress), run.lift.Evaluate(at.progress), at.runShare);
        return (travel, lift);
    }
}

// Pure step evaluation: where the foot is at a given step progress.
public static class StepArc {
    // The lift is measured from the straight path up to a clearance above the higher end, so step-ups are cleared.
    public static Vector2 Evaluate((Vector2 start, Vector2 end, float progress) step, (StepArcShape shape, float liftHeight, float runShare) arc) {
        (float travel, float lift) share = arc.shape.Evaluate((Mathf.Clamp01(step.progress), Mathf.Clamp01(arc.runShare)));
        Vector2 onPath = Vector2.LerpUnclamped(step.start, step.end, share.travel);

        float arcTop = Mathf.Max(step.start.y, step.end.y) + arc.liftHeight;
        return new Vector2(onPath.x, Mathf.Lerp(onPath.y, Mathf.Max(onPath.y, arcTop), Mathf.Max(0f, share.lift)));
    }
}
