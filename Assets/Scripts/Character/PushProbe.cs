using UnityEngine;

// Physics query for pushable bodies: a box as wide as the hull, from a little above the highest step the feet take up
// to the top of the hull, cast sideways. It finds the nearest dynamic Rigidbody2D whose face is mostly upright: a crate
// in front of the legs that the hull (hips to head) would pass over, as well as a taller one. Static and kinematic
// geometry, one-way platforms and triggers are left to the motor and the feet as before. A crate low enough to step on
// is below the box: the feet step onto it like any step.
public static class PushProbe {
    // The face must point at least this much against the cast (a mostly upright side, not a sloped top).
    private const float MinFaceNormalX = 0.7f;
    // The box starts this far above the highest step the feet take up.
    private const float StepClearance = 0.1f;
    // A box thinner than this is not cast (a deep crouch under a low ceiling).
    private const float MinBandHeight = 0.1f;
    private const int MaxHits = 8;

    private static readonly RaycastHit2D[] hits = new RaycastHit2D[MaxHits];

    public readonly struct Hit {
        public Hit(RaycastHit2D hit, int direction) {
            Collider = hit.collider;
            Body = hit.collider.attachedRigidbody;
            Distance = hit.distance;
            FaceX = hit.point.x;
            Direction = direction;
            Bounds bounds = hit.collider.bounds;
            Bottom = bounds.min.y;
            Top = bounds.max.y;
        }

        public Collider2D Collider { get; }
        public Rigidbody2D Body { get; }
        // How far the hull can move toward the face before it touches.
        public float Distance { get; }
        // The x of the face where the box met it.
        public float FaceX { get; }
        // +1 the crate is right of the body, -1 left.
        public int Direction { get; }
        public float Bottom { get; }
        public float Top { get; }
        // How far the body may travel toward the crate, leaving the motor's skin.
        public float FreeDistance => Mathf.Max(0f, Distance - KinematicBodyMotor.SkinWidth);
    }

    // The nearest pushable body within maxDistance (plus the motor's skin) on the given side (+1 right, -1 left).
    public static bool TryFind(CharacterRig rig, (int direction, float maxDistance) cast, out Hit found) {
        found = default;
        if (cast.direction == 0) {
            return false;
        }
        KinematicBodyMotor motor = rig.Motor;
        Rect hull = motor.CurrentHull;
        float bottom = rig.Stride.StanceHeight + rig.Stride.MaxStepUp + StepClearance;
        float top = hull.yMax;
        if (top - bottom < MinBandHeight) {
            return false;
        }

        Vector2 direction = Vector2.right * cast.direction;
        Vector2 center = new Vector2(hull.center.x, (top + bottom) / 2f);
        Vector2 size = new Vector2(hull.width, top - bottom);
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = motor.SolidMask, useTriggers = false };
        int count = Physics2D.BoxCast(center, size, 0f, direction, filter, hits, Mathf.Max(0f, cast.maxDistance) + KinematicBodyMotor.SkinWidth);

        int nearest = -1;
        for (int index = 0; index < count; index++) {
            if (IsPushableFace(hits[index], cast.direction) && (nearest < 0 || hits[index].distance < hits[nearest].distance)) {
                nearest = index;
            }
        }
        if (nearest < 0) {
            return false;
        }
        found = new Hit(hits[nearest], cast.direction);
        return true;
    }

    // Whether the body is something a push can move: dynamic and simulated. Any dynamic body counts, not only crates.
    public static bool IsPushable(Rigidbody2D body) {
        return body != null && body.bodyType == RigidbodyType2D.Dynamic && body.simulated;
    }

    // A body the box already overlaps (distance 0) does not count, like the motor's own casts.
    private static bool IsPushableFace(RaycastHit2D hit, int direction) {
        return hit.collider != null
            && hit.distance > 0f
            && hit.normal.x * direction <= -MinFaceNormalX
            && !OneWayPlatform.IsOneWay(hit.collider)
            && IsPushable(hit.collider.attachedRigidbody);
    }
}
