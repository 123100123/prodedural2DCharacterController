using UnityEngine;

// Pure relations of the effects: how many puffs and how big, from how hard something hit, and how a casing fades.
public static class EffectMath {
    // 0 at or below the minimum impact speed, rising to 1 at the full impact speed.
    public static float ImpactShare((float impactSpeed, float minImpact, float fullImpact) impact) {
        if (impact.impactSpeed <= impact.minImpact) {
            return 0f;
        }
        float full = Mathf.Max(impact.minImpact + 0.01f, impact.fullImpact);
        return Mathf.Clamp01(Mathf.InverseLerp(impact.minImpact, full, impact.impactSpeed));
    }

    // Whole puffs for an impact: none at a share of 0, at least one above it, maxCount at full impact. A scale of 0 is none.
    public static int PuffCount((float share, float scale) impact, int maxCount) {
        if (impact.share <= 0f || impact.scale <= 0f) {
            return 0;
        }
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, maxCount, impact.share) * impact.scale));
    }

    // Puff diameter in world units: small for a light impact, bigger for a hard one.
    public static float PuffSize(float share, float scale) => Mathf.Lerp(0.25f, 0.6f, share) * Mathf.Max(0f, scale);

    // Whole particles for a fixed base count under a scale; 0 for a scale of 0.
    public static int ScaledCount(float baseCount, float scale) {
        return scale <= 0f || baseCount <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseCount * scale));
    }

    // 1 while a casing lies, fading to 0 over fadeTime at the end of its life; 0 once it is over.
    public static float CasingAlpha(float age, float lifetime, float fadeTime) {
        if (age >= lifetime) {
            return 0f;
        }
        float fade = Mathf.Min(Mathf.Max(0.01f, fadeTime), lifetime);
        return Mathf.Clamp01((lifetime - age) / fade);
    }

    // How far a points popup has risen at an age: it shoots up and slows to a stop (ease-out), 0 at its birth and the full
    // height at the end of its lifetime.
    public static float PopupRise(float age, float lifetime, float height) {
        float share = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
        return height * (1f - (1f - share) * (1f - share));
    }

    // A points popup's opacity: 1 at its birth, easing down to 0 at the end of its lifetime (it stays readable for the
    // first part of its rise).
    public static float PopupAlpha(float age, float lifetime) {
        float share = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
        return 1f - share * share;
    }

    // The side of a direction that points up (or the right-hand side for a vertical one): where a casing is thrown.
    public static Vector2 UpwardPerpendicular(Vector2 direction) {
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        return perpendicular.y < 0f || (perpendicular.y == 0f && perpendicular.x < 0f) ? -perpendicular : perpendicular;
    }
}
