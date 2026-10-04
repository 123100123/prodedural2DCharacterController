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
    public bool FirePressed;
    public Vector2 PointerScreenPosition;
}
