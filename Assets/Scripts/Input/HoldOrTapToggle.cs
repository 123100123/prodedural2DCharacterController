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

    // The button is down on a press still short enough to end as a tap (one whose start was forgotten by CancelLatch is a
    // hold).
    public bool IsPossibleTap(float now) => pressStartTime.HasValue && now - pressStartTime.Value <= maxTapDuration;

    // Drops the latch and forgets the press in progress, so that press's release does not toggle it back on.
    public void CancelLatch() {
        isLatched = false;
        pressStartTime = null;
    }

    private void ToggleLatchIfTap(float pressDuration) {
        if (pressDuration <= maxTapDuration) {
            isLatched = !isLatched;
        }
    }
}
