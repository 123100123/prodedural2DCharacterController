using UnityEngine;

// The shape of the reload over its progress (0 to 1): how far the support hand has dropped to the magazine and how far
// the pistol is tilted up. The hand drops, waits while the magazine goes in, then slaps back up with a short kick.
public static class ReloadMotion {
    private const float DropEnd = 0.3f;
    private const float SlapStart = 0.65f;
    private const float SlapEnd = 0.8f;
    private const float SlapKick = 1.35f;

    // 0 with the hand at its grip, 1 with it at the magazine.
    public static float HandDrop(float progress) {
        if (progress <= 0f || progress >= SlapEnd) {
            return 0f;
        }
        if (progress < DropEnd) {
            return Smooth(progress / DropEnd);
        }
        if (progress < SlapStart) {
            return 1f;
        }
        // The way up is fast and ends hard: eased in, so the hand arrives at the grip with speed.
        float up = (progress - SlapStart) / (SlapEnd - SlapStart);
        return 1f - up * up;
    }

    // 0 level, 1 tilted up: tilts as the hand goes down, a little more at the slap, then settles level.
    public static float Tilt(float progress) {
        if (progress <= 0f || progress >= 1f) {
            return 0f;
        }
        if (progress < SlapStart) {
            return Smooth(Mathf.Clamp01(progress / DropEnd));
        }
        if (progress < SlapEnd) {
            return Mathf.Lerp(1f, SlapKick, (progress - SlapStart) / (SlapEnd - SlapStart));
        }
        return SlapKick * (1f - Smooth((progress - SlapEnd) / (1f - SlapEnd)));
    }

    // A direction raised by the given angle toward up, as seen from a character facing left (-1) or right (+1).
    public static Vector2 TiltedUp(Vector2 direction, (int facing, float degrees) tilt) {
        float elevation = Mathf.Atan2(direction.y, direction.x * tilt.facing) + tilt.degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(elevation) * tilt.facing, Mathf.Sin(elevation));
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
