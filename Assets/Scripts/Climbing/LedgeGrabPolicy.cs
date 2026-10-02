using UnityEngine;

// Pure climbing rules: which ledge heights can be grabbed, and where the body goes during a climb.
public static class LedgeGrabPolicy {
    // From the ground: higher than a step, low enough to reach with a jump.
    public static (float minTop, float maxTop) StandingGrabWindow((float stanceY, float maxStepUp) stance, ClimbSettings settings) {
        return (stance.stanceY + stance.maxStepUp, stance.stanceY + settings.maxGrabRise);
    }

    // In the air: around the shoulders, within reach of the hands.
    public static (float minTop, float maxTop) AirGrabWindow((float shoulderY, float armReach) arm, ClimbSettings settings) {
        return (arm.shoulderY - settings.airGrabBelowShoulder, arm.shoulderY + arm.armReach * settings.airGrabAboveShoulderShare);
    }

    public static bool IsInWindow(float topY, (float minTop, float maxTop) window) => topY > window.minTop && topY <= window.maxTop;

    // Body pivot positions for hang, pull-up, mount and stand; the hang never moves a higher body down.
    public static (Vector2 hang, Vector2 pulled, Vector2 mounted, Vector2 standing) BodyKeyframes(
        (Ledge ledge, Vector2 bodyStart, float standingHeight) climb,
        ClimbSettings settings) {
        Vector2 corner = climb.ledge.Corner;
        int toward = climb.ledge.ClimbDirection;

        Vector2 hang = new Vector2(corner.x - toward * settings.hangInset, Mathf.Max(climb.bodyStart.y, corner.y - settings.hangDrop));
        Vector2 pulled = corner + new Vector2(toward * settings.pulledUp.x, settings.pulledUp.y);
        Vector2 mounted = corner + new Vector2(toward * settings.mounted.x, settings.mounted.y);
        Vector2 standing = corner + new Vector2(toward * settings.standingInset, climb.standingHeight);
        return (hang, pulled, mounted, standing);
    }
}
