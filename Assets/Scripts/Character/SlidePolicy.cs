using UnityEngine;

// Pure running slide rules: when a crouch press starts a slide, how fast it starts and slows, and why it ends. The state
// gathers the facts (input, ground, probes); these decide.
public static class SlidePolicy {
    // Stick input smaller than this is no move input (the run input alone, standing, is not running).
    public const float InputDeadZone = RunIntent.InputDeadZone;

    // Why a slide ends, or that it goes on. Listed in priority order: a jump wins over everything.
    public enum Outcome { Slide, Jump, GroundLost, HitWall, Stopped, Released }

    // What the walking state found on the frame crouch was pressed.
    public readonly struct Entry {
        public Entry((bool crouchPressed, bool runIntent) input, (float groundSpeed, float runSpeed) motion, (bool grounded, bool ready) state) {
            CrouchPressed = input.crouchPressed;
            RunIntent = input.runIntent;
            GroundSpeed = motion.groundSpeed;
            RunSpeed = motion.runSpeed;
            Grounded = state.grounded;
            Ready = state.ready;
        }

        // Crouch went down this frame (the press, not a held or latched crouch).
        public bool CrouchPressed { get; }
        // The run input is held while moving (and not aiming, which slows to a walk).
        public bool RunIntent { get; }
        // Signed ground speed of the gait (positive toward world right).
        public float GroundSpeed { get; }
        public float RunSpeed { get; }
        public bool Grounded { get; }
        // The cooldown since the last slide has run out.
        public bool Ready { get; }
    }

    // What the sliding state found this frame, before it decides to go on.
    public readonly struct Frame {
        public Frame((bool jumpPressed, bool crouchRequested) input, (bool groundLost, bool blocked, float speed) motion, bool roomToStand) {
            JumpPressed = input.jumpPressed;
            CrouchRequested = input.crouchRequested;
            GroundLost = motion.groundLost;
            Blocked = motion.blocked;
            Speed = motion.speed;
            RoomToStand = roomToStand;
        }

        public bool JumpPressed { get; }
        // Crouch is held or latched on.
        public bool CrouchRequested { get; }
        // No ground within a step below the body.
        public bool GroundLost { get; }
        // The hull ran into geometry ahead.
        public bool Blocked { get; }
        // Unsigned slide speed after this frame's friction.
        public float Speed { get; }
        // A standing hull fits where the character is.
        public bool RoomToStand { get; }
    }

    // Holding run while moving (not aiming): the input asks for a run.
    public static bool IsRunIntent((bool runHeld, float moveAxis, bool isAiming) input) {
        return RunIntent.Resolve((input.runHeld, input.moveAxis)) && !input.isAiming;
    }

    // A crouch press on the ground, at a speed worth sliding: running (or near the run speed), faster than a slide ends,
    // and not too soon after the last slide.
    public static bool CanStart(Entry entry, SlideSettings settings) {
        float speed = Mathf.Abs(entry.GroundSpeed);
        bool fastEnough = entry.RunIntent || speed >= settings.entrySpeedShare * entry.RunSpeed;
        return settings.enabled
            && entry.CrouchPressed
            && entry.Grounded
            && entry.Ready
            && speed > settings.minSpeed
            && fastEnough;
    }

    // The cooldown runs from the end of the last slide.
    public static bool IsReady(float now, float readyAt) => now >= readyAt;

    // The slide keeps the speed the character had, or the boost when that is faster.
    public static float StartSpeed(float groundSpeed, SlideSettings settings) => Mathf.Max(Mathf.Abs(groundSpeed), settings.boostSpeed);

    // Friction takes a constant amount of speed per second, down to a stop.
    public static float NextSpeed(float speed, float deltaTime, SlideSettings settings) {
        return Mathf.Max(0f, speed - Mathf.Max(0f, settings.friction) * deltaTime);
    }

    // Too slow to slide any further: the crouch walk takes over.
    public static bool IsTooSlow(float speed, SlideSettings settings) => speed <= settings.minSpeed;

    // Releasing crouch (when it is not latched on) ends the slide only where the character can stand; under a low
    // ceiling it slides on until it stops.
    public static Outcome Continue(Frame frame, SlideSettings settings) {
        if (frame.JumpPressed) {
            return Outcome.Jump;
        }
        if (frame.GroundLost) {
            return Outcome.GroundLost;
        }
        if (frame.Blocked) {
            return Outcome.HitWall;
        }
        if (IsTooSlow(frame.Speed, settings)) {
            return Outcome.Stopped;
        }
        return !frame.CrouchRequested && frame.RoomToStand ? Outcome.Released : Outcome.Slide;
    }
}
