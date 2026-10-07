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

    // The intent points the given way (+1 right, -1 left): what a roll or a climb asks before it hands over to the run.
    public static bool Along((bool runIntent, float moveAxis) intent, int direction) {
        return intent.runIntent && intent.moveAxis * direction > 0f;
    }

    // A crouch held only by a tap's latch yields to the run intent; a held crouch button or a low ceiling does not
    // (the state reports those as other reasons).
    public static bool CancelsCrouchLatch(bool runIntent, RunReadiness readiness) {
        return runIntent && readiness.Block == RunBlock.CrouchLatched;
    }
}
