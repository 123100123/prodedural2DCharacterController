using UnityEngine;

// Trauma-based screen shake (Squirrel Eiserloh, "Juicing Your Cameras With Math"): events add trauma in [0, 1], it
// drains at a steady rate, and the shake is Perlin noise scaled by trauma squared, so a little trauma is barely
// felt and a lot of it is violent. Pure state: the camera reads Offset and Roll and applies them on top of its position.
public sealed class CameraShake {
    // Where each channel samples the noise, far apart so they do not move together.
    private const float SeedX = 3.7f;
    private const float SeedY = 41.3f;
    private const float SeedRoll = 97.9f;

    private readonly CameraFeelSettings settings;
    private float elapsed;

    public CameraShake(CameraFeelSettings settings) => this.settings = settings;

    public float Trauma { get; private set; }

    // Camera shove in world units, never more than maxShakeOffset on either axis.
    public Vector2 Offset {
        get {
            float strength = Strength();
            return strength > 0f ? new Vector2(Noise(SeedX), Noise(SeedY)) * (settings.maxShakeOffset * strength) : Vector2.zero;
        }
    }

    // Camera roll in degrees, never more than maxShakeRoll either way.
    public float Roll {
        get {
            float strength = Strength();
            return strength > 0f ? Noise(SeedRoll) * settings.maxShakeRoll * strength : 0f;
        }
    }

    public void AddTrauma(float amount) {
        if (!settings.shakeEnabled || amount <= 0f) {
            return;
        }
        Trauma = Mathf.Clamp01(Trauma + amount);
    }

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }
        elapsed += deltaTime;
        Trauma = settings.shakeEnabled ? Mathf.Max(0f, Trauma - settings.traumaDecay * deltaTime) : 0f;
    }

    // Trauma a landing adds: nothing up to the threshold impact speed, then a steady amount per unit above it.
    public static float LandingTrauma(float impactSpeed, CameraFeelSettings settings) {
        return Mathf.Clamp01((impactSpeed - settings.landingTraumaThreshold) * settings.landingTraumaPerSpeed);
    }

    // The share of the full shake: trauma squared, scaled by the accessibility amount.
    private float Strength() {
        return settings.shakeEnabled ? Trauma * Trauma * Mathf.Clamp01(settings.shakeAmount) : 0f;
    }

    private float Noise(float seed) {
        return Mathf.Clamp(Mathf.PerlinNoise(seed, elapsed * settings.shakeFrequency) * 2f - 1f, -1f, 1f);
    }
}
