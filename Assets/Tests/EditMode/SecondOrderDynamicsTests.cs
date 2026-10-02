using NUnit.Framework;

public class SecondOrderDynamicsTests {
    private const float Frame = 1f / 60f;

    private static (float peak, float final, float lowest) RunStep(SpringSettings settings, (float seconds, float deltaTime) run) {
        SecondOrderDynamics spring = new SecondOrderDynamics(0f);
        float peak = float.NegativeInfinity;
        float lowest = float.PositiveInfinity;
        for (float time = 0f; time < run.seconds; time += run.deltaTime) {
            float value = spring.Update((1f, run.deltaTime), settings);
            peak = UnityEngine.Mathf.Max(peak, value);
            lowest = UnityEngine.Mathf.Min(lowest, value);
        }
        return (peak, spring.Value, lowest);
    }

    [Test]
    public void SettlesOnTheTarget() {
        (float _, float final, float _) = RunStep(new SpringSettings(2f, 0.6f, 0f), (4f, Frame));
        Assert.That(final, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void UnderdampedOvershootsAndCriticallyDampedDoesNot() {
        (float underdampedPeak, float _, float _) = RunStep(new SpringSettings(2f, 0.4f, 0f), (4f, Frame));
        (float criticalPeak, float _, float _) = RunStep(new SpringSettings(2f, 1f, 0f), (4f, Frame));

        Assert.That(underdampedPeak, Is.GreaterThan(1.05f));
        Assert.That(criticalPeak, Is.LessThanOrEqualTo(1.001f));
    }

    [Test]
    public void StaysStableWithLongFrames() {
        (float peak, float final, float lowest) = RunStep(new SpringSettings(8f, 0.3f, 0f), (30f, 0.33f));

        Assert.That(peak, Is.LessThan(3f));
        Assert.That(lowest, Is.GreaterThan(-2f));
        Assert.That(final, Is.EqualTo(1f).Within(0.1f));
    }

    [Test]
    public void NegativeResponseAnticipates() {
        SecondOrderDynamics spring = new SecondOrderDynamics(0f);
        SpringSettings anticipating = new SpringSettings(2f, 1f, -1f);
        float lowest = 0f;
        for (int frame = 0; frame < 30; frame++) {
            float target = frame * Frame * 2f;
            lowest = UnityEngine.Mathf.Min(lowest, spring.Update((target, Frame), anticipating));
        }
        Assert.That(lowest, Is.LessThan(0f), "moves away from a moving target first");
    }

    [Test]
    public void ImpulseDisplacesThenReturns() {
        SecondOrderDynamics spring = new SecondOrderDynamics(1f);
        spring.AddVelocity(-5f);
        float lowest = 1f;
        for (int frame = 0; frame < 240; frame++) {
            lowest = UnityEngine.Mathf.Min(lowest, spring.Update((1f, Frame), new SpringSettings(3f, 0.5f, 0f)));
        }
        Assert.That(lowest, Is.LessThan(0.9f));
        Assert.That(spring.Value, Is.EqualTo(1f).Within(0.01f));
    }
}
