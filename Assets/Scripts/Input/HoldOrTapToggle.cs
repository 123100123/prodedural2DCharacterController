// Turns a button into a mode that is on while the button is held, or latched on and off by quick taps.
// Taps matter for touchpads, whose right-click presses and releases instantly (often within one frame).
public sealed class HoldOrTapToggle {
    private readonly float maxTapDuration;
    private float? pressStartTime;
    private bool isLatched;

    public HoldOrTapToggle(float maxTapDuration) {
        this.maxTapDuration = maxTapDuration;
    }

    public bool Update((bool wentDown, bool wentUp, bool isHeld) button, float now) {
        if (button.wentDown) {
            pressStartTime = now;
        }
        if (button.wentUp && pressStartTime.HasValue) {
            ToggleLatchIfTap(now - pressStartTime.Value);
            pressStartTime = null;
        }
        return button.isHeld || isLatched;
    }

    private void ToggleLatchIfTap(float pressDuration) {
        if (pressDuration <= maxTapDuration) {
            isLatched = !isLatched;
        }
    }
}
