using UnityEngine;

// A hill's shape for scenario tests: an up-ramp from its foot, a flat top, a down-ramp of the same angle. Knows the height
// of its surface and the slope of the ground at any x, so tests can measure feet and bodies against it.
public readonly struct Hill {
    public Hill((float x, float y) foot, (float height, float angle, float topWidth) shape) {
        FootX = foot.x;
        BaseY = foot.y;
        Height = shape.height;
        Angle = shape.angle;
        TopWidth = shape.topWidth;
    }

    public float FootX { get; }
    public float BaseY { get; }
    public float Height { get; }
    // Degrees from flat.
    public float Angle { get; }
    public float TopWidth { get; }

    public float Run => Height / Mathf.Tan(Angle * Mathf.Deg2Rad);
    public float TopStartX => FootX + Run;
    public float TopEndX => TopStartX + TopWidth;
    public float EndX => TopEndX + Run;
    public float TopY => BaseY + Height;
    public float Gradient => Mathf.Tan(Angle * Mathf.Deg2Rad);

    // The ground's height at x (the track beside the hill).
    public float SurfaceY(float x) {
        if (x <= FootX || x >= EndX) {
            return BaseY;
        }
        if (x < TopStartX) {
            return BaseY + (x - FootX) * Gradient;
        }
        if (x <= TopEndX) {
            return TopY;
        }
        return BaseY + (EndX - x) * Gradient;
    }

    // The ground's rise per unit of x at x (0 on the track and the top).
    public float GradientAt(float x) {
        if (x > FootX && x < TopStartX) {
            return Gradient;
        }
        if (x > TopEndX && x < EndX) {
            return -Gradient;
        }
        return 0f;
    }

    // Whether x is on a ramp at least margin in from its ends (where the ground under both feet is the same slope).
    public bool IsMidRamp(float x, float margin) {
        return (x > FootX + margin && x < TopStartX - margin) || (x > TopEndX + margin && x < EndX - margin);
    }

    // Distance from a point to the hill's surface, measured straight up or down (positive above it).
    public float HeightAbove(Vector2 point) => point.y - SurfaceY(point.x);

    // Distance from a point to the ramp line it is over, measured along the surface normal (positive above it).
    public float NormalDistance(Vector2 point) {
        float gradient = GradientAt(point.x);
        return HeightAbove(point) / Mathf.Sqrt(1f + gradient * gradient);
    }

    // The outline for a PolygonCollider2D, with a base a block deep under the track so nothing slips under it.
    public Vector2[] Outline() {
        float bottom = BaseY - 1f;
        return new[] {
            new Vector2(FootX, bottom), new Vector2(FootX, BaseY), new Vector2(TopStartX, TopY),
            new Vector2(TopEndX, TopY), new Vector2(EndX, BaseY), new Vector2(EndX, bottom),
        };
    }
}
