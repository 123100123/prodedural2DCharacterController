using UnityEngine;

// The roll's phases as the momentum sees them: each has its own drag and brake.
public enum RollMotionPhase { Dive, HandContact, Roll, Recover }

// What the move input asks of the recovery: stop (no input, or input against the roll), walk on, or run on along it.
public enum RollRecoverIntent { Settle, Walk, Along }

// How a roll starts: the incoming speed along it, how much of it is kept, the forward throw added on top, and the speed
// the throw takes it to.
public readonly struct RollLaunch {
    public RollLaunch((float incoming, float runShare) entry, (float inherited, float impulse, float target, float throwTime) thrown) {
        Incoming = entry.incoming;
        RunShare = entry.runShare;
        Inherited = thrown.inherited;
        Throw = thrown.impulse;
        Target = thrown.target;
        ThrowAcceleration = thrown.throwTime > 0f ? Mathf.Abs(thrown.target - entry.incoming) / thrown.throwTime : float.PositiveInfinity;
    }

    // u_in: the speed along the roll the previous state handed over (never negative).
    public float Incoming { get; }
    // How close the incoming speed is to the full run speed (0 standing, 1 running).
    public float RunShare { get; }
    // The share of the incoming speed the roll keeps.
    public float Inherited { get; }
    // The forward impulse added on top of it.
    public float Throw { get; }
    // The speed the throw takes the roll to: kept speed plus throw, clamped.
    public float Target { get; }
    // How fast the speed moves from Incoming to Target during the throw (u/s²).
    public float ThrowAcceleration { get; }
}

// The roll's travel speed as momentum: the roll starts at the incoming speed, rises to the kept speed plus a forward
// throw over a short throw time, then decays through its phases. A phase change changes only the drag and brake, never
// the speed itself, so there is no step at any boundary; the speed the recovery ends at is what the walk is handed.
public static class RollMomentum {
    // The dive's pitch leads by up to this much faster at the full run speed (a fixed feel, not a setting).
    private const float PitchLeadGain = 0.6f;
    // Where in the dive the throw's pelvis arc peaks: the rise is quick and the descent toward the hands longer.
    private const float LiftArcExponent = 0.6f;

    // The incoming velocity's part along the roll's direction of travel (a unit vector along the surface).
    public static float IncomingSpeed(Vector2 velocity, Vector2 rollDirection) => Mathf.Max(0f, Vector2.Dot(velocity, rollDirection));

    public static RollLaunch Launch((float incoming, float runSpeed) entry, RollMomentumSettings settings) {
        float incoming = Mathf.Max(0f, entry.incoming);
        float runShare = entry.runSpeed > 0f ? Mathf.Clamp01(incoming / entry.runSpeed) : 1f;
        float inherited = settings.inheritShare * incoming;
        float impulse = Mathf.Lerp(settings.standingThrow, settings.runningThrow, runShare);
        float target = Mathf.Max(Mathf.Min(inherited + impulse, settings.maxEntrySpeed), settings.minRollSpeed);
        return new RollLaunch((incoming, runShare), (inherited, impulse, target, settings.throwTime));
    }

    // The speed on the roll's first frame: the incoming speed, or the target at once when there is no throw time.
    public static float StartSpeed(RollLaunch launch, RollMomentumSettings settings) => settings.throwTime > 0f ? launch.Incoming : launch.Target;

    // The speed after one frame. sinceEntry: time from the start of the roll to the start of this frame. walkSpeed: what
    // walk input pulls the recovery toward.
    public static float Advance(float speed, (RollLaunch launch, float sinceEntry, RollMotionPhase phase, RollRecoverIntent intent, float walkSpeed) state, float deltaTime, RollMomentumSettings settings) {
        float throwing = Mathf.Clamp(settings.throwTime - state.sinceEntry, 0f, deltaTime);
        if (throwing > 0f) {
            speed = Mathf.MoveTowards(speed, state.launch.Target, state.launch.ThrowAcceleration * throwing);
        }
        float decaying = deltaTime - throwing;
        if (decaying > 0f) {
            speed = Decay(speed, (state.phase, state.intent, state.walkSpeed), decaying, settings);
            if (state.phase != RollMotionPhase.Recover) {
                speed = Mathf.Max(speed, settings.minRollSpeed);
            }
        }
        return speed;
    }

    // One frame of the phase's drag and brake (no throw, no floor). Drag decays exponentially, so the result does not
    // depend on the frame rate; a brake never takes the speed below zero, and walk input never past the walk speed.
    public static float Decay(float speed, (RollMotionPhase phase, RollRecoverIntent intent, float walkSpeed) state, float deltaTime, RollMomentumSettings settings) {
        (float drag, float brake) = Resistance((state.phase, state.intent), settings);
        bool walksOn = state.phase == RollMotionPhase.Recover && state.intent == RollRecoverIntent.Walk;
        if (walksOn && speed < state.walkSpeed) {
            return Mathf.MoveTowards(speed, state.walkSpeed, settings.settleBrake * deltaTime);
        }
        float floor = walksOn ? state.walkSpeed : 0f;
        return Mathf.Max(floor, speed * Mathf.Exp(-drag * deltaTime) - brake * deltaTime);
    }

    // The drag (1/s) and brake (u/s²) of a phase. Only the recovery depends on the move input.
    public static (float drag, float brake) Resistance((RollMotionPhase phase, RollRecoverIntent intent) state, RollMomentumSettings settings) {
        switch (state.phase) {
            case RollMotionPhase.Dive: return (settings.diveDrag, 0f);
            case RollMotionPhase.HandContact: return (settings.diveDrag, settings.contactBrake);
            case RollMotionPhase.Roll: return (settings.rollDrag, 0f);
            default: return (settings.recoverDrag, state.intent == RollRecoverIntent.Along ? 0f : settings.settleBrake);
        }
    }

    // The pelvis rise the throw adds to the dive's height at this share of the dive: a quick rise and a longer descent
    // back to nothing by the end of the dive, higher the faster the roll started.
    public static float DiveLift(float diveProgress, float runShare, RollMomentumSettings settings) {
        float arc = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(diveProgress), LiftArcExponent));
        return Mathf.Lerp(settings.standingLift, settings.runningLift, Mathf.Clamp01(runShare)) * Mathf.Max(0f, arc);
    }

    // The dive's pitch blends over this share of the dive: sooner the faster the roll started, so a running roll throws
    // the body forward onto the hands.
    public static float PitchLeadScale(float runShare) => 1f / (1f + Mathf.Clamp01(runShare) * PitchLeadGain);

    // The velocity handed to the next state: the roll's speed along the surface it travels on.
    public static Vector2 HandBack(float speed, Vector2 rollDirection) => speed * rollDirection;
}
