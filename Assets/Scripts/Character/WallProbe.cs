using UnityEngine;

// Physics queries for wall slides: whether solid geometry faces the body on one side at chest and knee height, and
// where its face is. A one-way platform is never a wall, and neither is a slope the character can walk on.
public static class WallProbe {
    // A surface counts as a wall when its normal is at least this horizontal.
    private const float MinWallNormalX = 0.7f;

    public readonly struct Contact {
        public Contact((bool chest, bool knee) touches, float faceX) {
            Chest = touches.chest;
            Knee = touches.knee;
            FaceX = faceX;
        }

        public bool Chest { get; }
        public bool Knee { get; }
        // The x of the wall face (from the chest probe, else the knee probe; the body's x when neither touches).
        public float FaceX { get; }
        public bool IsTouching => Chest || Knee;
    }

    // Rays from the body's centre line toward the side, reaching the hull's half width plus the detection distance.
    public static Contact Touch((Vector2 body, int side, float hullHalfWidth) probe, (WallSettings settings, LayerMask mask) query) {
        return Touch(probe, (query.settings, query.mask, KinematicBodyMotor.NoSlopes));
    }

    // maxSlopeAngle: surfaces no steeper than this are ground to walk on, not walls.
    public static Contact Touch((Vector2 body, int side, float hullHalfWidth) probe, (WallSettings settings, LayerMask mask, float maxSlopeAngle) query) {
        WallSettings settings = query.settings;
        float length = probe.hullHalfWidth + Mathf.Max(0f, settings.detectionDistance);
        float chestY = probe.body.y + settings.chestProbeHeight;
        float kneeY = chestY - settings.minWallHeight;

        bool chest = TryHitWall((new Vector2(probe.body.x, chestY), probe.side, length), (query.mask, query.maxSlopeAngle), out float chestFace);
        bool knee = TryHitWall((new Vector2(probe.body.x, kneeY), probe.side, length), (query.mask, query.maxSlopeAngle), out float kneeFace);
        float faceX = chest ? chestFace : knee ? kneeFace : probe.body.x;
        return new Contact((chest, knee), faceX);
    }

    private static bool TryHitWall((Vector2 origin, int side, float length) ray, (LayerMask mask, float maxSlopeAngle) query, out float faceX) {
        RaycastHit2D hit = OneWayFilter.Raycast((ray.origin, Vector2.right * ray.side, ray.length), query.mask, OneWaySupport.None);
        faceX = hit.point.x;
        bool isWalkable = query.maxSlopeAngle > 0f && SlopeMath.IsWalkable(hit.normal, query.maxSlopeAngle);
        return hit.collider != null && hit.distance > 0f && hit.normal.x * ray.side <= -MinWallNormalX && !isWalkable;
    }
}
