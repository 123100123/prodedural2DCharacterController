using UnityEngine;

// The brightness of a muzzle flash over time: full at once when the shot is fired, then dying away fast over its
// duration. A new shot while a flash is still alive starts it over at full brightness (it never adds up), so rapid fire
// flickers rather than building into a glare, and the state can never get stuck.
public sealed class FlashEnvelope {
    private float elapsed = float.PositiveInfinity;

    // Seconds since the last shot (infinite before the first).
    public float Elapsed => elapsed;

    public void Trigger() => elapsed = 0f;

    public void Advance(float deltaTime) {
        if (!float.IsPositiveInfinity(elapsed)) {
            elapsed += Mathf.Max(0f, deltaTime);
        }
    }

    public bool IsActive(LightingSettings settings) => elapsed < settings.muzzleDuration;

    // 1 at the shot, falling to 0 at the end of the duration along (1 - t) raised to the decay exponent.
    public float Level(LightingSettings settings) => LevelAt(elapsed, (settings.muzzleDuration, settings.muzzleDecayExponent));

    public static float LevelAt(float elapsed, (float duration, float exponent) decay) {
        if (elapsed < 0f || elapsed >= decay.duration || decay.duration <= 0f) {
            return 0f;
        }
        return Mathf.Pow(1f - elapsed / decay.duration, Mathf.Max(0.01f, decay.exponent));
    }
}
