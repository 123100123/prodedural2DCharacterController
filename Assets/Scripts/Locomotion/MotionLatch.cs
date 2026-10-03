using UnityEngine;

// Whether the gait is stepping or standing, with hysteresis: motion starts above the start speed and stops below the
// lower stop speed, so a speed hovering near either threshold cannot restart the gait every frame.
public sealed class MotionLatch {
    public enum Change { None, Started, Stopped }

    public bool IsMoving { get; private set; }

    // Back to standing, e.g. when the feet have just been placed in a stance after a landing.
    public void Reset() => IsMoving = false;

    public Change Track(float speed, GaitTransitionSettings thresholds) {
        float magnitude = Mathf.Abs(speed);
        if (!IsMoving && magnitude >= thresholds.startSpeed) {
            IsMoving = true;
            return Change.Started;
        }
        if (IsMoving && magnitude < thresholds.stopSpeed) {
            IsMoving = false;
            return Change.Stopped;
        }
        return Change.None;
    }
}
