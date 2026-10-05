using UnityEngine;

// Pure wall slide rules: which wall the player presses into, when a flight catches a wall, when a slide ends and why,
// how the slide speed ramps to its cap, and the kick of a wall jump. The state gathers the facts (probes, input); these
// decide.
public static class WallSlidePolicy {
    // Stick input smaller than this does not press into a wall.
    public const float InputDeadZone = 0.2f;

    // Why a slide ends, or that it goes on. Listed in priority order: a ledge in reach wins over everything.
    public enum Outcome { Slide, Ledge, Ground, WallEnded, Released }

    // What the state found this frame, before it decides to catch the wall.
    public readonly struct Catch {
        public Catch((float verticalSpeed, bool pressingIntoWall) motion, (bool chest, bool knee) contact, (float groundClearance, bool ledgeInReach) surroundings) {
            VerticalSpeed = motion.verticalSpeed;
            PressingIntoWall = motion.pressingIntoWall;
            ChestContact = contact.chest;
            KneeContact = contact.knee;
            GroundClearance = surroundings.groundClearance;
            LedgeInReach = surroundings.ledgeInReach;
        }

        public float VerticalSpeed { get; }
        public bool PressingIntoWall { get; }
        public bool ChestContact { get; }
        public bool KneeContact { get; }
        // Gap between the standing feet and the ground below; infinity when no ground is near.
        public float GroundClearance { get; }
        public bool LedgeInReach { get; }
    }

    // +1 pressing right, -1 pressing left, 0 not pressing either way.
    public static int PressedSide(float moveAxis) => moveAxis > InputDeadZone ? 1 : moveAxis < -InputDeadZone ? -1 : 0;

    // Falling (or nearly at the top of a rise), not about to land, pressing into a wall that covers the body from chest
    // to knees, and no ledge to grab instead.
    public static bool CanCatch(Catch wall, WallSettings settings) {
        return settings.enabled
            && wall.PressingIntoWall
            && wall.VerticalSpeed <= settings.catchRiseSpeed
            && wall.ChestContact
            && wall.KneeContact
            && wall.GroundClearance > settings.minGroundClearance
            && !wall.LedgeInReach;
    }

    // How long the player has not pressed into the wall: any press into it starts the count again.
    public static float ReleasedFor((float releasedFor, bool pressingIntoWall, float deltaTime) input) {
        return input.pressingIntoWall ? 0f : input.releasedFor + input.deltaTime;
    }

    // landsNextFrame: the ground is within the next frame's fall, so the flight takes over to land at the slide speed.
    public static Outcome Continue((bool ledgeInReach, bool landsNextFrame, bool kneeContact, float releasedFor) slide, WallSettings settings) {
        if (slide.ledgeInReach) {
            return Outcome.Ledge;
        }
        if (slide.landsNextFrame) {
            return Outcome.Ground;
        }
        if (!slide.kneeContact) {
            return Outcome.WallEnded;
        }
        return slide.releasedFor > settings.releaseGrace ? Outcome.Released : Outcome.Slide;
    }

    // The vertical speed of the next frame of a slide: eased toward the slide speed at the slide acceleration, so a fast
    // fall brakes and a stop at the top of a jump speeds up, and the cap is never passed.
    public static float SlideSpeed((float verticalSpeed, float deltaTime) fall, WallSettings settings) {
        float target = -Mathf.Max(0f, settings.maxSlideSpeed);
        return Mathf.MoveTowards(fall.verticalSpeed, target, Mathf.Max(0f, settings.slideAcceleration) * fall.deltaTime);
    }

    // The kick off a wall on the given side (+1 the wall is to the right): away from it and up.
    public static Vector2 WallJumpVelocity(int wallSide, WallSettings settings) {
        return new Vector2(-Mathf.Sign(wallSide) * Mathf.Abs(settings.jumpVelocity.x), Mathf.Max(0f, settings.jumpVelocity.y));
    }

    // How high a wall jump rises when its button is held through the rise (the height the variable jump cuts from).
    public static float WallJumpHeight(float gravity, WallSettings settings) {
        float rise = Mathf.Max(0f, settings.jumpVelocity.y);
        return gravity > 0f ? rise * rise / (2f * gravity) : 0f;
    }

    // A jump this soon after dropping off a wall (without jumping off it) still kicks off that wall.
    public static bool IsWithinCoyote((float now, float leftWallAt, int wallSide) jump, WallSettings settings) {
        return settings.enabled && jump.wallSide != 0 && jump.now - jump.leftWallAt <= settings.coyoteTime && jump.now >= jump.leftWallAt;
    }
}
