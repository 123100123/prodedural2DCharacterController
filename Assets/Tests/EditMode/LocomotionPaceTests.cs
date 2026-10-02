using NUnit.Framework;
using UnityEngine;

public class LocomotionPaceTests {
    private static readonly (GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) Gaits =
        (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default);

    [Test]
    public void MovingReachesOneAtWalkingSpeedAndStaysThere() {
        Assert.That(LocomotionPace.MovingAt(0f, Gaits.walk), Is.EqualTo(0f));
        Assert.That(LocomotionPace.MovingAt(Gaits.walk.speed / 2f, Gaits.walk), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(LocomotionPace.MovingAt(Gaits.walk.speed, Gaits.walk), Is.EqualTo(1f));
        Assert.That(LocomotionPace.MovingAt(-Gaits.run.speed, Gaits.walk), Is.EqualTo(1f), "either direction, and never past 1");
    }

    [Test]
    public void FullWalkIsTheWalkProfileAndFullRunIsTheRunProfile() {
        float walking = LocomotionPace.RunningAt(Gaits.walk.speed, Gaits);
        float running = LocomotionPace.RunningAt(Gaits.run.speed, Gaits);
        Assert.That(walking, Is.EqualTo(0f));
        Assert.That(running, Is.EqualTo(1f));
        Assert.That(GaitProfile.Blend((Gaits.walk, Gaits.run), walking).dutyFactor, Is.EqualTo(Gaits.walk.dutyFactor));
        Assert.That(GaitProfile.Blend((Gaits.walk, Gaits.run), running).dutyFactor, Is.EqualTo(Gaits.run.dutyFactor));
    }

    // The gait only turns into a run as the body actually speeds up: no step back on the way, whatever the input.
    [Test]
    public void RunningRisesSteadilyWithSpeed() {
        float previous = 0f;
        for (float speed = 0f; speed <= Gaits.run.speed; speed += 0.05f) {
            float running = LocomotionPace.RunningAt(speed, Gaits);
            Assert.That(running, Is.GreaterThanOrEqualTo(previous - 0.0001f), $"at speed {speed:F2}");
            Assert.That(running - previous, Is.LessThan(0.05f), $"no jump at speed {speed:F2}");
            previous = running;
        }
    }

    [Test]
    public void StaysAWalkJustAboveWalkingSpeed() {
        float justAbove = Gaits.walk.speed + 0.05f * (Gaits.run.speed - Gaits.walk.speed);
        Assert.That(LocomotionPace.RunningAt(justAbove, Gaits), Is.EqualTo(0f));
    }

    [Test]
    public void PaceIsClampedToShares() {
        LocomotionPace pace = new LocomotionPace((2f, -1f));
        Assert.That(pace.Moving, Is.EqualTo(1f));
        Assert.That(pace.Running, Is.EqualTo(0f));
    }
}
