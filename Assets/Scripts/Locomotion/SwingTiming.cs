using UnityEngine;

// How one step's swing is timed, fixed when the foot lifts. A gait step runs on the shared gait clock (StepScheduler):
// it advances with the clock through the gait's swing share of the cycle, however the cadence changes meanwhile, so
// both feet keep landing half a cycle apart through speed changes. A time ceiling still finishes the swing when the
// clock slows or stops (stopping). Steps outside the gait (settling into a stance, landing a jump) run on time alone.
public readonly struct SwingTiming {
    private SwingTiming((bool isOnClock, float longestDuration, float releaseProgress) timing) {
        IsOnClock = timing.isOnClock;
        LongestDuration = Mathf.Max(0.01f, timing.longestDuration);
        ReleaseProgress = timing.releaseProgress;
    }

    // Advances with the gait clock; otherwise on time alone.
    public bool IsOnClock { get; }
    // The swing never lasts longer than this, in seconds.
    public float LongestDuration { get; }
    // How far through this swing the foot must be before the other foot may lift: late when walking (a foot is always
    // down), mid-swing when running (both feet leave the ground for a moment).
    public float ReleaseProgress { get; }

    public static SwingTiming OnClock((float longestDuration, float releaseProgress) timing) {
        return new SwingTiming((true, timing.longestDuration, timing.releaseProgress));
    }

    // The share of the cycle a gait's swing lasts at the given speed (its swing duration over its cycle period).
    public static float CycleShareAt(float speed, GaitProfile gait) {
        return GaitTiming.SwingDuration(speed, gait) / GaitTiming.CyclePeriod(speed, gait);
    }

    public static SwingTiming Timed(float duration) => new SwingTiming((false, duration, 1f));

    // Progress made over one tick: the clock's swing progress this tick, at least the time ceiling's.
    public float ProgressOver((float clockProgress, float deltaTime) advance) {
        float onClock = IsOnClock ? advance.clockProgress : 0f;
        return Mathf.Max(onClock, advance.deltaTime / LongestDuration);
    }
}
