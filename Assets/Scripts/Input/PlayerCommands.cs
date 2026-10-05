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
    // Aim is held, or was latched on by a quick tap.
    public bool IsAiming;
    // Crouch is held, or was latched on by a quick tap.
    public bool CrouchRequested;
    // The crouch button went down this frame (a press while running starts a slide).
    public bool CrouchPressed;
    public bool FirePressed;
    public bool ReloadPressed;
    public Vector2 PointerScreenPosition;

    public bool UpHeld => VerticalIntent.HeldSide(VerticalAxis) > 0;
    public bool DownHeld => VerticalIntent.HeldSide(VerticalAxis) < 0;
}
