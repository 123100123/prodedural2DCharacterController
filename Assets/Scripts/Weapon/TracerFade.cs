using UnityEngine;

// How a tracer dies away: full brightness when the shot is fired, nothing at the end of its duration.
public static class TracerFade {
    public static float Alpha(float age, float duration) {
        if (duration <= 0f || age >= duration) {
            return 0f;
        }
        float remaining = 1f - Mathf.Max(0f, age) / duration;
        return remaining * remaining;
    }
}
