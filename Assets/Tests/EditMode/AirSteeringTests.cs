using NUnit.Framework;

public class AirSteeringTests {
    private const float Tick = 1f / 60f;
    private static readonly AirborneSettings Air = new AirborneSettings();

    private static float Step(float speedX, float axis) => AirSteering.Step((speedX, axis, Tick), Air);

    private static float Simulate(float speedX, float axis, float seconds) {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Tick) {
            speedX = Step(speedX, axis);
        }
        return speedX;
    }

    [Test]
    public void InputAcceleratesUpToTheAirSpeed() {
        Assert.That(Simulate(0f, 1f, 1f), Is.EqualTo(Air.airSpeed).Within(0.001f));
        Assert.That(Simulate(0f, -1f, 1f), Is.EqualTo(-Air.airSpeed).Within(0.001f));
        Assert.That(Step(0f, 1f), Is.EqualTo(Air.airAcceleration * Tick).Within(0.0001f));
    }

    [Test]
    public void ASmallerStickSteersToAProportionalSpeed() {
        Assert.That(Simulate(0f, 0.5f, 1f), Is.EqualTo(Air.airSpeed * 0.5f).Within(0.001f));
    }

    [Test]
    public void SpeedCarriedPastTheAirSpeedIsNotTrimmedBySteeringWithIt() {
        Assert.That(Step(9f, 1f), Is.EqualTo(9f), "holding forward keeps a run's speed");
        Assert.That(Simulate(9f, 1f, 1f), Is.EqualTo(9f).Within(0.0001f));
        Assert.That(Step(-9f, -1f), Is.EqualTo(-9f));
    }

    [Test]
    public void WithNoInputOnlyAGentleDragActs() {
        Assert.That(Step(9f, 0f), Is.EqualTo(9f - Air.neutralDrag * Tick).Within(0.0001f));
        float afterAJump = Simulate(9f, 0f, 0.8f);
        Assert.That(afterAJump, Is.GreaterThan(9f - Air.neutralDrag * 0.9f), "a jump keeps most of its momentum");
        Assert.That(Simulate(3.5f, 0f, 3f), Is.EqualTo(0f).Within(0.0001f), "and the drag does stop it eventually");
    }

    [Test]
    public void OppositeInputBrakesAtFullAccelerationAndReverses() {
        Assert.That(Step(9f, -1f), Is.EqualTo(9f - Air.airAcceleration * Tick).Within(0.0001f));
        float speed = Simulate(9f, -1f, 0.2f);
        Assert.That(speed, Is.EqualTo(9f - Air.airAcceleration * 0.2f).Within(0.5f));
        Assert.That(Simulate(9f, -1f, 1f), Is.EqualTo(-Air.airSpeed).Within(0.001f), "ends up heading the other way at the air speed");
    }

    [Test]
    public void SteeringIsContinuousAcrossTheAirSpeed() {
        float below = Step(Air.airSpeed - 0.001f, 1f);
        float above = Step(Air.airSpeed + 0.001f, 1f);
        Assert.That(System.Math.Abs(below - above), Is.LessThan(0.02f));
    }

    [Test]
    public void ADragOfZeroKeepsTheMomentumCompletely() {
        AirborneSettings noDrag = new AirborneSettings { neutralDrag = 0f };
        Assert.That(AirSteering.Step((9f, 0f, Tick), noDrag), Is.EqualTo(9f));
    }
}
