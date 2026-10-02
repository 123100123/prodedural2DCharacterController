using UnityEngine;

// A debug override of the gait's target speed, for inspecting a gait consistently without holding keys: Auto follows
// the player's input; Idle stands; Walk and Run move at full walk or run speed, or at a chosen speed. The direction
// still follows the move input, or the facing without it. Shipping gameplay never sets one.
public readonly struct GaitPreview {
    public enum Mode { Auto, Idle, Walk, Run }

    public static readonly GaitPreview Off = new GaitPreview((Mode.Auto, null));

    public GaitPreview((Mode mode, float? speed) preview) {
        PreviewMode = preview.mode;
        Speed = preview.speed;
    }

    public Mode PreviewMode { get; }
    // Overrides the walk or run speed when set (units per second).
    public float? Speed { get; }
    public bool IsActive => PreviewMode != Mode.Auto;

    // The signed speed the gait should head for under this preview.
    public float IntendedSpeed((float moveAxis, int facingSign, float walkSpeed, float runSpeed) input) {
        if (PreviewMode == Mode.Idle) {
            return 0f;
        }
        float direction = input.moveAxis != 0f ? Mathf.Sign(input.moveAxis) : input.facingSign;
        float fullSpeed = PreviewMode == Mode.Run ? input.runSpeed : input.walkSpeed;
        return direction * (Speed ?? fullSpeed);
    }
}
