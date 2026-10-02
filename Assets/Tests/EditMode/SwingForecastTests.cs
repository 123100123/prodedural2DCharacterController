using NUnit.Framework;
using UnityEngine;

public class SwingForecastTests {
    private static readonly (GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) Gaits =
        (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default);

    private static SwingForecast.Prediction Forecast((float speed, float intendedSpeed) motion) {
        GaitProfile gait = SwingForecast.GaitAt(motion.speed, Gaits);
        float swingShare = SwingTiming.CycleShareAt(motion.speed, gait);
        return SwingForecast.Predict(new SwingForecast.Request((motion.speed, motion.intendedSpeed, swingShare, gait.maxSwingDuration, 0f), Gaits));
    }

    [Test]
    public void AtSteadySpeedTheSwingLastsItsDurationAndTheBodyTravelsAtThatSpeed([Values(2f, 3.5f, 6f, 8.5f)] float speed) {
        SwingForecast.Prediction swing = Forecast((speed, speed));
        float expected = GaitTiming.SwingDuration(speed, SwingForecast.GaitAt(speed, Gaits));
        Assert.That(swing.Duration, Is.EqualTo(expected).Within(0.02f));
        Assert.That(swing.Travel, Is.EqualTo(speed * swing.Duration).Within(0.001f));
        Assert.That(swing.LandingSpeed, Is.EqualTo(speed));
    }

    [Test]
    public void SpeedingUpTheHipTravelsFurtherAndLandsFaster() {
        float speed = Gaits.walk.speed;
        SwingForecast.Prediction cruising = Forecast((speed, speed));
        SwingForecast.Prediction speedingUp = Forecast((speed, Gaits.run.speed));
        Assert.That(speedingUp.LandingSpeed, Is.GreaterThan(speed));
        Assert.That(speedingUp.Travel, Is.GreaterThan(speed * speedingUp.Duration));
        Assert.That(speedingUp.Duration, Is.LessThan(cruising.Duration), "a quickening clock ends the swing sooner");
    }

    // Stopping from a run, the last steps must not land where a running hip would have been.
    [Test]
    public void StoppingTheHipTravelsLessAndSlowsDown() {
        float speed = Gaits.run.speed;
        SwingForecast.Prediction stopping = Forecast((speed, 0f));
        Assert.That(stopping.LandingSpeed, Is.LessThan(speed));
        Assert.That(stopping.Travel, Is.LessThan(0.6f * speed * stopping.Duration));
    }

    [Test]
    public void MovingLeftTravelsLeft() {
        Assert.That(Forecast((-Gaits.walk.speed, -Gaits.walk.speed)).Travel, Is.LessThan(0f));
    }

    [Test]
    public void TheSwingNeverOutlastsItsLongestDuration() {
        SwingForecast.Prediction swing = SwingForecast.Predict(new SwingForecast.Request((0.3f, 0f, 0.4f, 0.5f, 0f), Gaits));
        Assert.That(swing.Duration, Is.LessThanOrEqualTo(0.5f + 1f / 60f));
    }

    [Test]
    public void ASwingHalfDoneHasHalfOfItLeft() {
        float speed = Gaits.walk.speed;
        GaitProfile gait = SwingForecast.GaitAt(speed, Gaits);
        float share = SwingTiming.CycleShareAt(speed, gait);
        SwingForecast.Prediction whole = SwingForecast.Predict(new SwingForecast.Request((speed, speed, share, gait.maxSwingDuration, 0f), Gaits));
        SwingForecast.Prediction rest = SwingForecast.Predict(new SwingForecast.Request((speed, speed, share, gait.maxSwingDuration, 0.5f), Gaits));
        Assert.That(rest.Duration, Is.EqualTo(whole.Duration / 2f).Within(0.02f));
    }
}
