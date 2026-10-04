using UnityEngine;

// Keeps a repeating background looking fixed in the world while it follows the camera: it moves only in whole periods
// of its pattern, so the pattern never appears to slide.
public static class GridMath {
    // The nearest point of the lattice with the given period.
    public static Vector2 Snap(Vector2 point, float period) {
        float step = Mathf.Max(0.0001f, period);
        return new Vector2(Mathf.Round(point.x / step) * step, Mathf.Round(point.y / step) * step);
    }
}
