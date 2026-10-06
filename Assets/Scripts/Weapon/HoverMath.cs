using UnityEngine;

// The pure parts of a hover target: where its route is at a time, how it bobs and leans, what a hit scores and how the
// rotor blades look at a moment of their spin.
public static class HoverMath {
    // The offset from the anchor on the route at a time (seconds since the start of the route).
    public static Vector2 PathOffset(HoverTargetSettings settings, float time) {
        float cycle = time / Mathf.Max(0.01f, settings.period) + settings.startShare;
        switch (settings.path) {
            case HoverPathKind.Loop: {
                float angle = cycle * 2f * Mathf.PI;
                return new Vector2(settings.extent.x * Mathf.Sin(angle), settings.extent.y * Mathf.Cos(angle));
            }
            case HoverPathKind.FigureEight: {
                float angle = cycle * 2f * Mathf.PI;
                return new Vector2(settings.extent.x * Mathf.Sin(angle), settings.extent.y * Mathf.Sin(2f * angle));
            }
            default: {
                // 0 to 1 and back over one period, eased so it slows to a stop and turns round at each end.
                float swing = Mathf.PingPong(cycle * 2f, 1f);
                float eased = swing * swing * (3f - 2f * swing);
                return Vector2.LerpUnclamped(-settings.extent, settings.extent, eased);
            }
        }
    }

    public static float Bob(HoverTargetSettings settings, float time) {
        return settings.bobHeight * Mathf.Sin(2f * Mathf.PI * time / Mathf.Max(0.01f, settings.bobPeriod));
    }

    // How far below the anchor the board's centre can get: the route's depth plus the bob. The walking path stays clear
    // when the anchor sits this far plus half the board above head height.
    public static float Descent(HoverTargetSettings settings) => settings.extent.y + settings.bobHeight;

    // The lean of the board (degrees, counter-clockwise positive) for a horizontal speed: into the motion, so a board
    // flying right tips its right side down.
    public static float Tilt(HoverTargetSettings settings, float horizontalSpeed) {
        return Mathf.Clamp(-horizontalSpeed * settings.tiltPerSpeed, -settings.maxTilt, settings.maxTilt);
    }

    // What a hit that did or did not break the board is worth: the breaking hit pays its own bonus, not both.
    public static int Points(HoverTargetSettings settings, bool broke) => broke ? settings.breakPoints : settings.hitPoints;

    // The x scale of a blade at a point of the rotor's spin (turns, any value): a blade turning round the mast is seen
    // edge-on from the side, so its length shrinks and grows with the cosine; the other blade is half a turn behind.
    public static float BladeScale(float spinTurns, int blade) {
        float scale = Mathf.Cos(spinTurns * 2f * Mathf.PI);
        return blade == 0 ? scale : -scale;
    }

    // The blade's opacity: a fast rotor smears (a blade is fainter the shorter it looks), a slow one is solid.
    public static float BladeAlpha(float bladeScale, float spinShare) {
        float smear = Mathf.Lerp(0.45f, 0.9f, Mathf.Abs(bladeScale));
        return Mathf.Lerp(1f, smear, Mathf.Clamp01(spinShare));
    }
}
