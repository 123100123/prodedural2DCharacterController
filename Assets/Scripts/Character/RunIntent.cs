using UnityEngine;

// Pure run intent: the player wants to run as soon as the character can. Input only says what is wanted; the state the
// character is in decides when it can be fulfilled (ICharacterState.RunReadinessFor). The intent is read again every
// frame while the run input is held, so it carries across states that cannot run yet without being latched.
public static class RunIntent {
    // Stick input smaller than this is no move input: the run input alone, with no direction, is no intent.
    public const float InputDeadZone = 0.2f;

    // The run input held with a direction to run in.
    public static bool Resolve((bool runHeld, float moveAxis) input) {
        return input.runHeld && Mathf.Abs(input.moveAxis) > InputDeadZone;
    }

    // Moving against the way the character faces while aiming (backpedalling): the one aimed movement that stays a walk.
    // Moving toward the aim side, or any movement without aiming, is not backpedalling. facing is +1 right, -1 left.
    public static bool AimingBackward(bool isAiming, float moveAxis, int facing) {
        return isAiming && moveAxis * facing < 0f;
    }

    // Whether the walk heads for the run speed: the run input with any move input, so a gamepad stick tilted less than
    // the dead zone still runs at its share of the run speed (the gait scales the speed by the axis). While aiming it runs
    // only toward the side the character aims at and faces (backpedalling stays a walk). The dead zone only gates the
    // intent's effects on other states (the crouch, the slide, the roll and climb exits).
    public static bool GaitRuns((bool runHeld, float moveAxis, bool isAiming, int facing) input) {
        return input.runHeld && input.moveAxis != 0f && !AimingBackward(input.isAiming, input.moveAxis, input.facing);
    }

    // The intent points the given way (+1 right, -1 left): what a roll or a climb asks before it hands over to the run.
    public static bool Along((bool runIntent, float moveAxis) intent, int direction) {
        return intent.runIntent && intent.moveAxis * direction > 0f;
    }

    // A crouch held only by a tap's latch yields to the run intent; a held crouch button or a low ceiling does not
    // (the state reports those as other reasons).
    public static bool CancelsCrouchLatch(bool runIntent, RunReadiness readiness) {
        return runIntent && readiness.Block == RunBlock.CrouchLatched;
    }

    // A crouch press still short enough to be a tap does not crouch under the run intent. The slide is unaffected: it
    // starts on the press edge (PlayerCommands.CrouchPressed), which this leaves alone, and holds its own crouch.
    public static bool IgnoresCrouchTap(bool runIntent, RunReadiness readiness) {
        return runIntent && readiness.Block == RunBlock.CrouchTap;
    }
}
