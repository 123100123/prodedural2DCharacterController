using NUnit.Framework;

public class GroundSpeedRampTests {
    private static readonly GaitProfile Walk = GaitProfile.Walk;

    [Test]
    public void SpeedsUpAtTheAccelerationAndBrakesAtTheDeceleration() {
        Assert.That(GroundSpeedRamp.Step((0f, Walk.speed, Walk), 0.1f), Is.EqualTo(Walk.acceleration * 0.1f).Within(0.0001f));
        Assert.That(GroundSpeedRamp.Step((Walk.speed, 0f, Walk), 0.1f), Is.EqualTo(Walk.speed - Walk.deceleration * 0.1f).Within(0.0001f));
    }

    [Test]
    public void TurningAroundBrakesFirst() {
        float speed = GroundSpeedRamp.Step((2f, -Walk.speed, Walk), 0.05f);
        Assert.That(speed, Is.EqualTo(2f - Walk.deceleration * 0.05f).Within(0.0001f));
    }

    [Test]
    public void NeverOvershootsTheTarget() {
        Assert.That(GroundSpeedRamp.Step((3f, 3.2f, Walk), 1f), Is.EqualTo(3.2f));
    }
}
