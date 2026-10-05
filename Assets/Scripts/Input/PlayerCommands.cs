using UnityEngine;

// One frame of player intent, decoupled from devices and bindings.
public struct PlayerCommands {
    public float MoveAxis;
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
}
