using UnityEngine;

// Turns the raw up/down input (keys give -1, 0 or 1; a stick anything between) into a deadzoned axis and the frames on
// which up or down went down: a stick resting a little off centre is no input, and holding a direction is one press.
public sealed class VerticalIntent {
    // Stick deflections smaller than this are no input; the rest is rescaled to start from zero.
    public const float Deadzone = 0.2f;
    // The deadzoned axis counts as up (or down) held from this far.
    public const float HeldThreshold = 0.5f;

    private int heldSide;

    public static float ApplyDeadzone(float raw, float deadzone) {
        float magnitude = Mathf.Abs(raw);
        if (magnitude <= deadzone) {
            return 0f;
        }
        float share = Mathf.Clamp01((magnitude - deadzone) / Mathf.Max(0.0001f, 1f - deadzone));
        return Mathf.Sign(raw) * share;
    }

    // +1 up, -1 down, 0 neither, for a deadzoned axis value.
    public static int HeldSide(float axis) => axis >= HeldThreshold ? 1 : axis <= -HeldThreshold ? -1 : 0;

    // One frame of input: the deadzoned axis, and whether up or down was pressed this frame.
    public (float axis, bool upPressed, bool downPressed) Update(float raw) {
        float axis = ApplyDeadzone(raw, Deadzone);
        int side = HeldSide(axis);
        bool pressed = side != 0 && side != heldSide;
        heldSide = side;
        return (axis, pressed && side > 0, pressed && side < 0);
    }
}
