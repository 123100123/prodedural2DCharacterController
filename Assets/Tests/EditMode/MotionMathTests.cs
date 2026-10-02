using NUnit.Framework;

public class MotionMathTests {
    [Test]
    public void LaunchSpeedReachesTheRequestedApex() {
        const float gravity = 30f;
        const float height = 2.4f;
        float speed = JumpMath.LaunchSpeedForHeight(gravity, height);

        float apex = speed * speed / (2f * gravity);
        Assert.That(apex, Is.EqualTo(height).Within(0.0001f));
    }

    [Test]
    public void NegativeHeightLaunchesNothing() {
        Assert.That(JumpMath.LaunchSpeedForHeight(30f, -1f), Is.EqualTo(0f));
    }

    [Test]
    public void EaseMatchesTheTunedFractionAtSixtyFps() {
        Assert.That(FrameRateEase.FractionForFrame(0.25f, 1f / 60f), Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void TwoHalfFramesEaseAsMuchAsOneFullFrame() {
        float half = FrameRateEase.FractionForFrame(0.25f, 1f / 120f);
        float remainingAfterTwoHalves = (1f - half) * (1f - half);
        float remainingAfterOneFrame = 1f - FrameRateEase.FractionForFrame(0.25f, 1f / 60f);

        Assert.That(remainingAfterTwoHalves, Is.EqualTo(remainingAfterOneFrame).Within(0.0001f));
    }
}
