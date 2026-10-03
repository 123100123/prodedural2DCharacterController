using NUnit.Framework;
using UnityEngine;

public class PostureLeanTests {
    private static readonly PostureSettings Posture = PostureSettings.Default;
    private static readonly (GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) Gaits =
        (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default);

    private static float BaseLeanAt(float speed) {
        GaitProfile gait = SwingForecast.GaitAt(speed, Gaits);
        return PostureLean.BaseLean((Posture.idleLean, gait.lean, LocomotionPace.MovingAt(speed, Gaits.walk)));
    }

    [Test]
    public void RunningLeansFurtherThanWalkingWhichLeansForward() {
        float idle = BaseLeanAt(0f);
        float walking = BaseLeanAt(Gaits.walk.speed);
        float running = BaseLeanAt(Gaits.run.speed);
        Assert.That(walking, Is.GreaterThan(idle + 2f), "walking visibly leans forward");
        Assert.That(running, Is.GreaterThan(walking + 8f), "running leans into one forward body line");
    }

    // The base lean depends on speed alone, so it moves smoothly through idle, walk and run and back.
    [Test]
    public void BaseLeanRisesSmoothlyWithSpeed() {
        float previous = BaseLeanAt(0f);
        for (float speed = 0.05f; speed <= Gaits.run.speed; speed += 0.05f) {
            float lean = BaseLeanAt(speed);
            Assert.That(lean, Is.GreaterThanOrEqualTo(previous - 0.0001f), $"at speed {speed:F2}");
            Assert.That(lean - previous, Is.LessThan(0.3f), $"no jump at speed {speed:F2}");
            previous = lean;
        }
    }

    [Test]
    public void AccelerationLeansIntoItAndBrakingRocksBackWithinLimits() {
        Assert.That(PostureLean.AccelerationLean(4f, Posture), Is.GreaterThan(0f));
        Assert.That(PostureLean.AccelerationLean(-4f, Posture), Is.LessThan(0f));
        Assert.That(PostureLean.AccelerationLean(1000f, Posture), Is.EqualTo(Posture.maxAccelerationLean));
    }

    [Test]
    public void TheWholeLeanIsCapped() {
        Assert.That(PostureLean.Total((Posture.maxTotalLean, Posture.maxAccelerationLean), Posture), Is.EqualTo(Posture.maxTotalLean));
    }
}
