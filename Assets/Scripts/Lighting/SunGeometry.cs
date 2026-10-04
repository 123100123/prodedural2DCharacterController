using UnityEngine;

// The geometry of the sun, which a 2D renderer has no light for: a Freeform light whose polygon covers the view and
// whose pivot, where the shadows are thrown from, is placed far away toward the sun. The further the pivot, the more
// parallel the shadows. Angles are degrees counter-clockwise from the right horizon: 0 sets on the right, 90 is
// overhead, 180 rises on the left.
public static class SunGeometry {
    // The unit direction from the scene toward the sun.
    public static Vector2 Direction(float angleDegrees) {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    // Where the sun's pivot is, for a view centred on viewCenter.
    public static Vector2 Pivot(Vector2 viewCenter, (float angleDegrees, float distance) sun) {
        return viewCenter + Direction(sun.angleDegrees) * sun.distance;
    }

    // The covered area as a polygon in the light's own space (relative to its pivot, counter-clockwise): the coverage
    // rectangle centred on the view, which lies back toward the scene from the pivot.
    public static Vector3[] LocalShape((float angleDegrees, float distance) sun, Vector2 coverage) {
        Vector2 centre = -Direction(sun.angleDegrees) * sun.distance;
        Vector2 half = coverage / 2f;
        return new[] {
            new Vector3(centre.x - half.x, centre.y - half.y, 0f),
            new Vector3(centre.x + half.x, centre.y - half.y, 0f),
            new Vector3(centre.x + half.x, centre.y + half.y, 0f),
            new Vector3(centre.x - half.x, centre.y + half.y, 0f),
        };
    }

    // Which way a shadow is thrown: directly away from the sun.
    public static Vector2 ShadowDirection(float angleDegrees) => -Direction(angleDegrees);

    // A point is lit by the sun's light shape if it lies inside the coverage rectangle around the view centre.
    public static bool Covers(Vector2 point, Vector2 viewCenter, Vector2 coverage) {
        Vector2 half = coverage / 2f;
        return Mathf.Abs(point.x - viewCenter.x) <= half.x && Mathf.Abs(point.y - viewCenter.y) <= half.y;
    }
}
