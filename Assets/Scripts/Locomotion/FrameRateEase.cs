using UnityEngine;

// Converts an ease tuned as "fraction of the remaining distance per frame at 60 FPS" into this frame's fraction.
public static class FrameRateEase {
    private const float ReferenceFrameRate = 60f;

    public static float FractionForFrame(float fractionPerReferenceFrame, float deltaTime) {
        return 1f - Mathf.Pow(1f - fractionPerReferenceFrame, deltaTime * ReferenceFrameRate);
    }
}
