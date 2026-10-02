using UnityEngine;

// Where one leg is in its step: planted, or part of the way through a swing timed by its SwingTiming.
public sealed class SwingProgress {
    public bool IsGrounded { get; private set; } = true;
    // Normalized progress of the current swing; 0 while planted.
    public float Progress => IsGrounded ? 0f : progress;
    public SwingTiming Timing { get; private set; } = SwingTiming.Timed(1f);
    // The other foot may lift while this one is planted or far enough through its swing.
    public bool LetsOtherLegLift => IsGrounded || progress >= Timing.ReleaseProgress;

    private float progress = 1f;

    public void Begin(SwingTiming timing) {
        Timing = timing;
        progress = 0f;
        IsGrounded = false;
    }

    // The leg carries no weight and swings freely (airborne, rolling, climbing).
    public void Release() {
        progress = 1f;
        IsGrounded = false;
    }

    // Advances the swing by one tick; returns true on the tick the foot lands.
    public bool Advance((float clockProgress, float deltaTime) advance) {
        if (IsGrounded) {
            return false;
        }
        progress = Mathf.Min(1f, progress + Timing.ProgressOver(advance));
        IsGrounded = progress >= 1f;
        return IsGrounded;
    }
}
