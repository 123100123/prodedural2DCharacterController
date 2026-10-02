using UnityEngine;

// Pure breath shape: how full the lungs are over one breath, 0 empty to 1 full. Breathing in is quicker than breathing
// out, and each eases in and out, so the chest rests a moment when full and when empty.
public static class Breathing {
    public const float InhaleShare = 0.4f;

    public static float Fullness(float breathPhase) {
        float phase = Mathf.Repeat(breathPhase, 1f);
        return phase < InhaleShare
            ? Mathf.SmoothStep(0f, 1f, phase / InhaleShare)
            : Mathf.SmoothStep(1f, 0f, (phase - InhaleShare) / (1f - InhaleShare));
    }
}
