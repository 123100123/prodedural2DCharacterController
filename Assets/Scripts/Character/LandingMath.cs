using UnityEngine;

// Pure relations of a landing: when the ground arrives, how much the character reaches for it, and how hard it lands.
public static class LandingMath {
    // Seconds until a body falling at fallSpeed (downward, positive) with the given gravity has dropped distance.
    public static float TimeToImpact((float distance, float fallSpeed) fall, float gravity) {
        float distance = Mathf.Max(0f, fall.distance);
        float speed = Mathf.Max(0f, fall.fallSpeed);
        if (gravity <= 0.0001f) {
            return speed > 0.0001f ? distance / speed : float.PositiveInfinity;
        }
        return (-speed + Mathf.Sqrt(speed * speed + 2f * gravity * distance)) / gravity;
    }

    // 0 while the ground is further than prepTime away, rising smoothly to 1 at touchdown.
    public static float Prep(float timeToImpact, float prepTime) {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timeToImpact / Mathf.Max(0.01f, prepTime)));
    }

    // How far below the body the ground is looked for: as far as it falls in the prep time, and at least the minimum.
    public static float LookAhead(float fallSpeed, LandingSettings settings) {
        return Mathf.Max(settings.lookAheadMin, Mathf.Max(0f, fallSpeed) * settings.prepTime);
    }

    // 0 for a light impact, 1 for a hard one.
    public static float ImpactShare(float impactSpeed, LandingSettings settings) {
        float hard = Mathf.Max(settings.lightImpactSpeed + 0.01f, settings.hardImpactSpeed);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.lightImpactSpeed, hard, impactSpeed));
    }

    // How far below standing height the body is placed at touchdown.
    public static float Sink(float impactShare, LandingSettings settings) => Mathf.Lerp(settings.sinkLight, settings.sinkHard, impactShare);

    // The downward speed handed to the body spring.
    public static float CarriedFallSpeed(float impactSpeed, LandingSettings settings) {
        return Mathf.Min(settings.maxCarriedSpeed, Mathf.Max(0f, impactSpeed) * settings.speedCarry);
    }

    // Horizontal speed after the touchdown: a hard landing costs a share of it.
    public static float SpeedAfter(float speedX, float impactShare, LandingSettings settings) {
        return speedX * (1f - settings.speedLossHard * impactShare);
    }

    // How much lower the pelvis is held at first, recovering through the compression spring; less from a crouch.
    public static float Compression(float impactShare, float crouchShare, LandingSettings settings) {
        return impactShare * settings.compressionHard * (1f - 0.5f * Mathf.Clamp01(crouchShare));
    }
}
