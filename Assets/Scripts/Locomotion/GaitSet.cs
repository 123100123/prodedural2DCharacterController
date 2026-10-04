using UnityEngine;

// The gaits the body can be in and how they blend: the walk and run profiles blend by speed, and the crouch profile
// blends over them by how crouched the character is, but only at the speeds it is for. Everything that needs "the gait
// at a speed" asks here, so a further gait is one more profile and one more blend term in this struct.
public readonly struct GaitSet {
    public GaitSet((GaitProfile walk, GaitProfile run, GaitProfile crouch) profiles, GaitTransitionSettings transitions, float crouchShare) {
        Walk = profiles.walk;
        Run = profiles.run;
        Crouch = profiles.crouch;
        Transitions = transitions;
        CrouchShare = Mathf.Clamp01(crouchShare);
    }

    public GaitProfile Walk { get; }
    public GaitProfile Run { get; }
    public GaitProfile Crouch { get; }
    public GaitTransitionSettings Transitions { get; }
    // 0 standing, 1 fully crouched.
    public float CrouchShare { get; }

    // A walk and run pair with no crouch, as the gait was before crouching existed.
    public static implicit operator GaitSet((GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) gaits) {
        return new GaitSet((gaits.walk, gaits.run, GaitProfile.Crouch), gaits.transitions, 0f);
    }

    // How far the crouch gait applies at a speed: all of the crouch share up to the crouch speed, fading to none by
    // walking speed. A character that crouches while running slows through the run and walk gaits (their timing follows
    // the speed) and the crouch gait takes over only as it reaches a crouch walk; the pelvis, which follows the crouch
    // share directly, drops at once.
    public float CrouchWeightAt(float speed) {
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Crouch.speed, Mathf.Max(Crouch.speed + 0.01f, Walk.speed), Mathf.Abs(speed)));
        return CrouchShare * (1f - fade);
    }

    // The speed that counts as full motion at this speed: walking speed, or the crouch speed when crouching, so that a
    // crouch walk moves the body, arms and posture as much as a walk does.
    public float ReferenceSpeedAt(float speed) => Mathf.Lerp(Walk.speed, Crouch.speed, CrouchWeightAt(speed));

    // 0 standing, 1 at the reference speed and above.
    public float MovingAt(float speed) => Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, ReferenceSpeedAt(speed)));

    // The walk-to-run blend the speed asks for (none while the crouch gait is in use: it is slower than a walk).
    public float RunningAt(float speed) => LocomotionPace.RunningAt(speed, (Walk, Run, Transitions));

    // The gait in use at a speed, with the walk-to-run blend the body has actually reached.
    public GaitProfile At(float speed, float runningShare) {
        GaitProfile standing = GaitProfile.Blend((Walk, Run), runningShare);
        return GaitProfile.Blend((standing, Crouch), CrouchWeightAt(speed));
    }

    // The gait the body settles into at a speed.
    public GaitProfile SettledAt(float speed) => At(speed, RunningAt(speed));

    // The speed to head for: the walk or run speed, or the crouch speed as far as the character is crouched.
    public float TargetSpeed((float direction, bool running) intent) {
        float standing = intent.direction * (intent.running ? Run.speed : Walk.speed);
        return Mathf.Lerp(standing, intent.direction * Crouch.speed, CrouchShare);
    }
}
