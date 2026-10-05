using UnityEngine;

// After letting go of a ledge, the same ledge cannot be grabbed again for a moment, so the falling hands do not catch it
// straight back. Other ledges (a lower one to drop to) stay grabbable.
public sealed class LedgeRegrabLockout {
    // Corners closer than this are the same ledge (a re-found corner shifts a little with the edge search).
    public const float SameLedgeDistance = 0.3f;

    private Vector2 corner;
    private float until = float.NegativeInfinity;

    public void Lock(Vector2 ledgeCorner, float lockedUntil) {
        corner = ledgeCorner;
        until = lockedUntil;
    }

    public bool Blocks(Vector2 ledgeCorner, float now) {
        return now < until && (ledgeCorner - corner).sqrMagnitude <= SameLedgeDistance * SameLedgeDistance;
    }

    public void Clear() => until = float.NegativeInfinity;
}
