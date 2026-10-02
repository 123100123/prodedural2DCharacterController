using NUnit.Framework;
using UnityEngine;

// The body's vertical spring against the gait it carries: well away from resonance with the step rate.
public class BodySpringTests {
    private const float Frame = 1f / 120f;
    private static readonly SpringSettings Vertical = BodyMassSettings.Default.verticalSpring;

    // The bob happens twice per stride, once per step.
    private static float StepRate(GaitProfile gait) => gait.speed / GaitTiming.StepLength(gait.speed, gait);

    // Drives the spring with a bob of amplitude 1 for a few seconds and measures the last bob cycle: its amplitude and
    // how far its peak trails the target's peak, as a share of the cycle.
    private static (float gain, float lagShare) FollowBob(float bobRate) {
        SecondOrderDynamics spring = new SecondOrderDynamics(0f);
        float period = 1f / bobRate;
        (float peak, float peakTime) followed = (float.NegativeInfinity, 0f);
        for (float time = 0f; time < 4f + period; time += Frame) {
            float value = spring.Update((Mathf.Sin(2f * Mathf.PI * bobRate * time), Frame), Vertical);
            if (time >= 4f && value > followed.peak) {
                followed = (value, time);
            }
        }
        float targetPeakTime = 4f + Mathf.Repeat(period / 4f - 4f, period);
        float lag = Mathf.Repeat(followed.peakTime - targetPeakTime + period / 2f, period) - period / 2f;
        return (followed.peak, lag / period);
    }

    [Test]
    public void FollowsTheRunningBobWithoutResonating() {
        (float gain, float lagShare) run = FollowBob(StepRate(GaitProfile.Run));
        Assert.That(run.gain, Is.InRange(0.85f, 1.2f), "close to the bob, no resonant amplification");
        Assert.That(run.lagShare, Is.InRange(-0.02f, 0.12f), "peaks with the bob, not a quarter cycle late");
    }

    [Test]
    public void FollowsTheWalkingBobClosely() {
        (float gain, float lagShare) walk = FollowBob(StepRate(GaitProfile.Walk));
        Assert.That(walk.gain, Is.InRange(0.9f, 1.1f));
        Assert.That(walk.lagShare, Is.InRange(-0.02f, 0.08f));
    }

    [Test]
    public void ItsNaturalFrequencyIsWellAboveTheRunningStepRate() {
        Assert.That(Vertical.frequency, Is.GreaterThan(1.5f * StepRate(GaitProfile.Run)));
    }
}
