using UnityEngine;

// What a jump takes from the character's motion on the ground. The launch keeps the ground speed (plus a little extra
// forward speed at a run), and the windup that loads the jump fades away as the character moves faster, so a running
// jump leaves the ground off its stride instead of stopping to crouch first.
public static class JumpMomentum {
    // 0 standing, 1 at the run speed and above.
    public static float SpeedShare(float groundSpeed, float runSpeed) {
        return Mathf.Clamp01(Mathf.Abs(groundSpeed) / Mathf.Max(0.01f, runSpeed));
    }

    // The signed horizontal launch speed: the ground speed kept, plus a forward bonus that grows with the speed share,
    // never past the run speed plus the bonus.
    public static float LaunchSpeedX((float groundSpeed, float runSpeed) motion, JumpSettings jump) {
        float bonus = jump.forwardImpulse * Mathf.SmoothStep(0f, 1f, SpeedShare(motion.groundSpeed, motion.runSpeed));
        float launch = motion.groundSpeed * jump.carry + Mathf.Sign(motion.groundSpeed) * bonus;
        float cap = motion.runSpeed + jump.forwardImpulse;
        return Mathf.Clamp(launch, -cap, cap);
    }

    // Apex height of a held jump: the full height, a little lower from a crouch.
    public static float FullHeight(JumpSettings jump, float crouchShare) {
        return Mathf.Lerp(jump.jumpHeight, jump.jumpHeight * jump.crouchJumpScale, Mathf.Clamp01(crouchShare));
    }

    // 1 standing, falling to 0 across the windup speed band.
    public static float WindupScale(float speedShare, JumpSettings jump) {
        float band = Mathf.InverseLerp(jump.windupSpeedBand.x, Mathf.Max(jump.windupSpeedBand.x + 0.01f, jump.windupSpeedBand.y), speedShare);
        return 1f - Mathf.SmoothStep(0f, 1f, band);
    }

    // How long the jump loads: the full windup standing, none to speak of running, half as long again from a crouch.
    public static float WindupDuration((float speedShare, float crouchShare) motion, JumpSettings jump) {
        float scaled = jump.windupDuration * WindupScale(motion.speedShare, jump) * (1f - 0.5f * Mathf.Clamp01(motion.crouchShare));
        return Mathf.Max(jump.minWindup, scaled);
    }

    // How far the pelvis sinks while loading: less the faster the character moves, and none from a crouch (already low).
    public static float WindupDepth((float speedShare, float crouchShare) motion, JumpSettings jump) {
        return jump.crouchDepth * WindupScale(motion.speedShare, jump) * (1f - Mathf.Clamp01(motion.crouchShare));
    }
}
