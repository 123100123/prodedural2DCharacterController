using UnityEngine;

// One frame of player intent, decoupled from devices and bindings.
public struct PlayerCommands {
    public float MoveAxis;
    // Up (+1) to down (-1), deadzoned: W/S, the up/down arrows and the left stick.
    public float VerticalAxis;
    // Up or down went down this frame.
    public bool UpPressed;
    public bool DownPressed;
    public bool JumpPressed;
    // The jump button is down this frame (releasing it early cuts a jump short).
    public bool JumpHeld;
    public bool RollPressed;
    public bool RunHeld;
    // The run input is held with a direction (RunIntent.Resolve): the player wants to run as soon as the character can.
    // Filled by the driver after reading the input; the states decide when it can be fulfilled.
    public bool RunIntent;
    // Aim is held, or was latched on by a quick tap.
    public bool IsAiming;
    // Crouch is held, or was latched on by a quick tap.
    public bool CrouchRequested;
    // The crouch button is physically down this frame (a held crouch, as opposed to one only a tap's latch holds).
    public bool CrouchHeld;
    // The crouch button is down on a press still short enough to end as a tap: not yet a held crouch.
    public bool CrouchTapPending;
    // The crouch button went down this frame (a press while running starts a slide).
    public bool CrouchPressed;
    public bool FirePressed;
    public bool ReloadPressed;
    public Vector2 PointerScreenPosition;

    public bool UpHeld => VerticalIntent.HeldSide(VerticalAxis) > 0;
    public bool DownHeld => VerticalIntent.HeldSide(VerticalAxis) < 0;
}
