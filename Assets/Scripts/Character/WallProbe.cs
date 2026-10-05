using UnityEngine;

// Physics queries for wall slides: whether solid geometry faces the body on one side at chest and knee height, and
// where its face is.
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
        WallSettings settings = query.settings;
        float length = probe.hullHalfWidth + Mathf.Max(0f, settings.detectionDistance);
        float chestY = probe.body.y + settings.chestProbeHeight;
        float kneeY = chestY - settings.minWallHeight;

        bool chest = TryHitWall((new Vector2(probe.body.x, chestY), probe.side, length), query.mask, out float chestFace);
        bool knee = TryHitWall((new Vector2(probe.body.x, kneeY), probe.side, length), query.mask, out float kneeFace);
        float faceX = chest ? chestFace : knee ? kneeFace : probe.body.x;
        return new Contact((chest, knee), faceX);
    }

    private static bool TryHitWall((Vector2 origin, int side, float length) ray, LayerMask mask, out float faceX) {
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, Vector2.right * ray.side, ray.length, mask);
        faceX = hit.point.x;
        return hit.collider != null && hit.distance > 0f && hit.normal.x * ray.side <= -MinWallNormalX;
    }
}
