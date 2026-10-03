using UnityEngine;

// Pure horizontal pelvis motion around the locomotion anchor, from the support foot. The anchor carries the body
// through the world; these are local corrections that make the pelvis travel through the legs:
// - the support offset steers the pelvis toward a stance centred over the actual planted foot (behind it at contact,
//   over it at mid-stance, ahead of it at toe-off), so a foot that landed a little off its plan still carries the body
//   instead of stretching away from it. It fades in and out over the stance, so it is zero at contact, at toe-off and in
//   flight, and the pelvis target stays continuous when the support changes;
// - the surge is the body's speed change over its support: fastest at contact, slowest at mid-stance as the leg takes
//   the weight, fast again through push-off. As a position around the anchor that is a little ahead in early stance and
//   a little behind in late stance, and zero at contact, mid-stance and toe-off, so the pelvis is right over its foot at
//   mid-stance.
public static class PelvisSupportMotion {
    // Where the hip would be over a centred stance at the stance's progress.
    public static float CentredHipX(SupportStance stance) {
        float progress = Mathf.Clamp01(stance.Progress);
        return stance.FootX + stance.Direction * stance.StanceTravel * (progress - 0.5f);
    }

    // pelvisX: where the pelvis rides without the offset. coupling: share of the gap to the centred stance the pelvis
    // takes at mid-stance; maxOffset bounds it.
    public static float SupportOffset((SupportStance stance, float pelvisX) at, (float coupling, float maxOffset) limits) {
        if (!at.stance.HasSupport || at.stance.Progress >= 1f) {
            return 0f;
        }
        float gap = CentredHipX(at.stance) - at.pelvisX;
        float offset = limits.coupling * StanceWeight(at.stance.Progress) * gap;
        return Mathf.Clamp(offset, -limits.maxOffset, limits.maxOffset);
    }

    // Signed along the world x axis: positive is ahead for a body moving right.
    public static float Surge(SupportStance stance, float amplitude) {
        if (!stance.HasSupport || stance.Progress >= 1f) {
            return 0f;
        }
        return stance.Direction * amplitude * Mathf.Sin(2f * Mathf.PI * stance.Progress);
    }

    // 0 at contact and toe-off, 1 at mid-stance.
    private static float StanceWeight(float progress) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
}
