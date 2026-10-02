using UnityEngine;

// Analytic two-bone IK in a plane: where the middle joint and the tip go to reach a target.
public static class TwoBoneIk {
    private const float ReachSlack = 0.0001f;

    // Out-of-reach targets are approached as far as the chain allows; bendHint picks which side the middle joint bends to.
    public static (Vector2 joint, Vector2 tip) Solve(
        (Vector2 root, Vector2 target, Vector2 bendHint) reach,
        (float upper, float lower) lengths) {
        Vector2 toTarget = reach.target - reach.root;
        float distance = Mathf.Clamp(
            toTarget.magnitude,
            Mathf.Abs(lengths.upper - lengths.lower) + ReachSlack,
            lengths.upper + lengths.lower - ReachSlack);
        Vector2 direction = toTarget.sqrMagnitude > 0f ? toTarget.normalized : Vector2.right;

        float alongReach = (lengths.upper * lengths.upper - lengths.lower * lengths.lower + distance * distance) / (2f * distance);
        float jointOffset = Mathf.Sqrt(Mathf.Max(0f, lengths.upper * lengths.upper - alongReach * alongReach));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float bendSide = Vector2.Dot(perpendicular, reach.bendHint) >= 0f ? 1f : -1f;

        Vector2 joint = reach.root + direction * alongReach + perpendicular * (jointOffset * bendSide);
        Vector2 tip = reach.root + direction * distance;
        return (joint, tip);
    }
}
