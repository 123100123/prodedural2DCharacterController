using UnityEngine;

// One frame of player intent, decoupled from devices and bindings.
public struct PlayerCommands {
    public float MoveAxis;
    public bool JumpPressed;
    public bool RollPressed;
    // Aim is held, or was latched on by a quick tap.
    public bool IsAiming;
    public bool FirePressed;
    public Vector2 PointerScreenPosition;
}
