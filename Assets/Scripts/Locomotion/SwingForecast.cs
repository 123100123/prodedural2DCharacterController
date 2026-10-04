using UnityEngine;

// Predicts a gait swing from its lift: how long it lasts, how far the body travels meanwhile and how fast it moves at
// touchdown. It replays the speed ramp toward the intended speed and the clock-driven swing progress the gait will
// apply, so the foot is planned where the hip will be at touchdown whether the body is speeding up, cruising or stopping.
public static class SwingForecast {
    private const float ForecastStep = 1f / 60f;

    public readonly struct Request {
        // startProgress: how far through the swing it already is (0 at lift; more when re-planning a swing in the air).
        public Request((float speed, float intendedSpeed, float swingShare, float longestDuration, float startProgress) motion, GaitSet gaits) {
            Speed = motion.speed;
            IntendedSpeed = motion.intendedSpeed;
            SwingShare = motion.swingShare;
            LongestDuration = motion.longestDuration;
            StartProgress = motion.startProgress;
            Gaits = gaits;
        }

        public float Speed { get; }
        public float IntendedSpeed { get; }
        public float SwingShare { get; }
        public float LongestDuration { get; }
        public float StartProgress { get; }
        public GaitSet Gaits { get; }
    }

    // The rest of the swing, from its start progress to touchdown.
    public readonly struct Prediction {
        public Prediction((float duration, float travel, float landingSpeed) swing) {
            Duration = swing.duration;
            Travel = swing.travel;
            LandingSpeed = swing.landingSpeed;
        }

        public float Duration { get; }
        // Signed distance the body moves during the swing (positive toward world right).
        public float Travel { get; }
        public float LandingSpeed { get; }
    }

    // The foot lifts after the body has already moved this tick, and the swing still advances once in the same tick:
    // that first step moves the foot but not the body.
    public static Prediction Predict(Request request) {
        float speed = request.Speed;
        float liftTickProgress = SwingStep((speed, request));
        (float elapsed, float travel, float progress) swing = (0f, 0f, request.StartProgress + liftTickProgress);
        float longestRemaining = (1f - request.StartProgress) * request.LongestDuration;
        while (swing.progress < 1f && swing.elapsed < longestRemaining) {
            speed = GroundSpeedRamp.Step((speed, request.IntendedSpeed, GaitAt(speed, request.Gaits)), ForecastStep);
            swing = (swing.elapsed + ForecastStep, swing.travel + speed * ForecastStep, swing.progress + SwingStep((speed, request)));
        }
        return new Prediction((swing.elapsed, swing.travel, speed));
    }

    // Swing progress over one step at a speed: the clock's advance through the swing share, at least the time ceiling's.
    private static float SwingStep((float speed, Request request) at) {
        GaitProfile gait = GaitAt(at.speed, at.request.Gaits);
        float strides = Mathf.Abs(at.speed) * ForecastStep / GaitTiming.StrideLength(at.speed, gait);
        float clockProgress = strides / Mathf.Max(0.05f, at.request.SwingShare);
        return SwingTiming.OnClock((at.request.LongestDuration, 1f)).ProgressOver((clockProgress, ForecastStep));
    }

    // The gait the body settles into at a speed.
    public static GaitProfile GaitAt(float speed, GaitSet gaits) => gaits.SettledAt(speed);
}
