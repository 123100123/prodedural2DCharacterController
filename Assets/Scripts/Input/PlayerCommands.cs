using UnityEngine;

// One frame of player intent, decoupled from devices and bindings.
public struct PlayerCommands {
    public float MoveAxis;
    public bool JumpPressed;
    public bool RollPressed;
    public bool AimHeld;
    public bool FirePressed;
    public Vector2 PointerScreenPosition;
}
