using NUnit.Framework;

public class MotionLatchTests {
    private static readonly GaitTransitionSettings Thresholds = GaitTransitionSettings.Default;

    [Test]
    public void StartsAboveTheStartSpeedAndStopsBelowTheStopSpeed() {
        MotionLatch latch = new MotionLatch();
        Assert.That(latch.Track(Thresholds.startSpeed - 0.01f, Thresholds), Is.EqualTo(MotionLatch.Change.None));
        Assert.That(latch.Track(Thresholds.startSpeed, Thresholds), Is.EqualTo(MotionLatch.Change.Started));
        Assert.That(latch.Track(Thresholds.stopSpeed, Thresholds), Is.EqualTo(MotionLatch.Change.None), "still moving at the stop speed");
        Assert.That(latch.Track(Thresholds.stopSpeed - 0.01f, Thresholds), Is.EqualTo(MotionLatch.Change.Stopped));
    }

    [Test]
    public void SpeedHoveringAroundEitherThresholdChangesStateOnce() {
        MotionLatch latch = new MotionLatch();
        int changes = 0;
        float[] hovering = { 0.3f, 0.36f, 0.3f, 0.36f, 0.2f, 0.36f, 0.16f, 0.36f, 0.14f, 0.16f, 0.14f, 0.16f };
        foreach (float speed in hovering) {
            changes += latch.Track(speed, Thresholds) == MotionLatch.Change.None ? 0 : 1;
        }
        Assert.That(changes, Is.EqualTo(2), "one start and one stop");
    }

    [Test]
    public void EitherDirectionCounts() {
        MotionLatch latch = new MotionLatch();
        Assert.That(latch.Track(-1f, Thresholds), Is.EqualTo(MotionLatch.Change.Started));
        Assert.That(latch.IsMoving, Is.True);
    }
}
