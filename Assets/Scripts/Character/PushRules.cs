using UnityEngine;

// Pure push rules: when a push takes hold and lets go (with hysteresis, so a body bobbing with its steps never flickers
// in and out of contact), how fast the character may walk while pushing, how fast the crate is driven, the force that
// drives it within the push's strength, and the spring that blends the pose. Speeds are measured along the push.
public static class PushRules {
    // Stick input smaller than this pushes nothing.
    public const float InputDeadZone = 0.2f;
    // The pose blend's spring settles to about 95% in the blend time at this frequency times the inverse blend time
    // (a critically damped spring covers 95% in about 4.74 / (2 pi f) seconds).
    private const float BlendFrequencyPerInverseSecond = 0.755f;

    // +1 pressing right, -1 pressing left, 0 not pressing either way.
    public static int PressedSide(float moveAxis) => moveAxis > InputDeadZone ? 1 : moveAxis < -InputDeadZone ? -1 : 0;

    // A push takes hold with this little room left between where the body is heading and the crate's face.
    public static float EngageDistance(PushSettings settings) => 2f * Mathf.Max(0f, settings.contactSkin);

    // A push lets go with more room than this; always further than it takes hold, so the two never alternate.
    public static float ReleaseDistance(PushSettings settings) => Mathf.Max(settings.releaseDistance, EngageDistance(settings) + 0.01f);

    // Whether the push holds this frame. canPush: enabled, pressing toward a crate standing on the character's ground,
    // grounded and not aiming. room: how far the crate's face is ahead of where the body is heading.
    public static bool Holds(bool wasPushing, (bool canPush, float room) contact, PushSettings settings) {
        if (!contact.canPush) {
            return false;
        }
        return contact.room <= (wasPushing ? ReleaseDistance(settings) : EngageDistance(settings));
    }

    // The fastest the character may walk while pushing: the crate's own speed, plus the slack scaled by the room left
    // (closing in while there is more than the contact skin, backing off while there is less), never faster than the
    // push speed and never backward. A crate that does not move holds the character at the skin.
    public static float CharacterSpeed((float crateSpeed, float room) contact, PushSettings settings) {
        float skin = Mathf.Max(0.001f, settings.contactSkin);
        float closing = Mathf.Clamp((contact.room - skin) / skin, -1f, 1f);
        float speed = Mathf.Max(0f, contact.crateSpeed) + Mathf.Max(0f, settings.speedSlack) * closing;
        return Mathf.Clamp(speed, 0f, Mathf.Max(0f, settings.pushSpeed));
    }

    // The speed the crate is driven toward: the push speed, but never more than the slack ahead of the character, so
    // the two speed up together and the crate cannot run away from the hands.
    public static float CrateTargetSpeed(float characterSpeed, PushSettings settings) {
        return Mathf.Min(Mathf.Max(0f, settings.pushSpeed), Mathf.Max(0f, characterSpeed) + Mathf.Max(0f, settings.speedSlack));
    }

    // The force along the push that brings the crate to its target speed in one physics step, within the push's
    // strength. A push never pulls: a crate already faster than the target gets nothing. Friction is the physics
    // engine's: a heavy crate whose friction exceeds the strength does not move.
    public static float CrateForce((float mass, float speed, float targetSpeed) crate, (float maxForce, float fixedDeltaTime) step) {
        if (step.fixedDeltaTime <= 0f || crate.mass <= 0f) {
            return 0f;
        }
        float matching = crate.mass * (crate.targetSpeed - crate.speed) / step.fixedDeltaTime;
        return Mathf.Clamp(matching, 0f, Mathf.Max(0f, step.maxForce));
    }

    // The pull of gravity along a slope the crate is being pushed up: its weight times the sine of the slope's angle (degrees
    // from flat; 0 on flat ground).
    public static float SlopeLoad((float mass, float gravity) crate, float slopeAngle) {
        if (slopeAngle <= 0f || crate.mass <= 0f || crate.gravity <= 0f) {
            return 0f;
        }
        return crate.mass * crate.gravity * Mathf.Sin(Mathf.Min(slopeAngle, 90f) * Mathf.Deg2Rad);
    }

    // The force along a slope that holds the crate at its target speed up it: the crate's weight along the slope (what a
    // flat push does not have to carry) plus the force that matches the speeds, which here may also be negative, so a crate
    // the load runs ahead of the hands is held back. Speeds are along the slope; within the push's strength, never a pull.
    public static float SlopeForce((float mass, float speed, float targetSpeed) crate, (float load, float maxForce, float fixedDeltaTime) step) {
        if (step.fixedDeltaTime <= 0f || crate.mass <= 0f) {
            return 0f;
        }
        float matching = crate.mass * (crate.targetSpeed - crate.speed) / step.fixedDeltaTime;
        return Mathf.Clamp(step.load + matching, 0f, Mathf.Max(0f, step.maxForce));
    }

    // A critically damped spring that blends the pose in or out in about the blend time.
    public static SpringSettings BlendSpring(float blendTime) {
        return new SpringSettings(BlendFrequencyPerInverseSecond / Mathf.Max(0.02f, blendTime), 1f, 0f);
    }
}
