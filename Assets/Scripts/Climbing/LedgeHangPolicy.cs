using UnityEngine;

// Pure rules of the ledge hang: what the input asks for while hanging, and the idle sway of a body left hanging.
public static class LedgeHangPolicy {
    public enum Choice { Hold, Climb, Drop }

    // What this frame's input asks for. toward: the move input along the climb direction (+1 toward the ledge); vertical:
    // up +1, down -1. Jump climbs; down, or pushing away from the ledge, lets go; up, or pushing toward it, climbs. Down
    // wins over up and toward, so a diagonal down-toward stick lets go.
    public static Choice Decide((float toward, float vertical, bool jumpPressed) input, LedgeHangSettings settings) {
        if (!settings.holdable || input.jumpPressed) {
            return Choice.Climb;
        }
        float threshold = settings.intentThreshold;
        if (input.vertical <= -threshold) {
            return Choice.Drop;
        }
        if (input.vertical >= threshold || input.toward >= threshold) {
            return Choice.Climb;
        }
        return input.toward <= -threshold ? Choice.Drop : Choice.Hold;
    }

    // The choice made at the grab: holding toward the ledge as the hands catch it climbs straight on, as before hangs
    // could be held.
    public static Choice OnGrab(float toward, LedgeHangSettings settings) {
        return !settings.holdable || toward >= settings.intentThreshold ? Choice.Climb : Choice.Hold;
    }

    // While the hands reach for the corner (and the shortest hang plays), the latest choice is remembered, so a tap made
    // before the hang begins is not lost; holding still keeps the earlier choice.
    public static Choice Remember(Choice remembered, Choice now) => now == Choice.Hold ? remembered : now;

    // Offset of a body left hanging from its hang position: it swings gently out from the wall (never into it) and rises
    // and sinks with its breath. timeHanging: seconds since the hang began holding; awayFromWall: -1 or +1.
    public static Vector2 IdleSway((float timeHanging, int awayFromWall) hang, LedgeHangSettings settings) {
        float sway = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * hang.timeHanging / Mathf.Max(0.1f, settings.swayPeriod));
        float breath = Mathf.Sin(2f * Mathf.PI * hang.timeHanging / Mathf.Max(0.1f, settings.breathPeriod));
        return new Vector2(hang.awayFromWall * settings.swayAmplitude * sway, settings.breathAmplitude * breath);
    }
}
