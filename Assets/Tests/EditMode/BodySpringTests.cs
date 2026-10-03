using NUnit.Framework;
using UnityEngine;

// The body's vertical spring against the gait it carries: away from resonance with the step rate, and with the bob
// aimed ahead by the spring's own lag so the body's low and high points land where the gait puts them.
public class BodySpringTests {
    private const float Frame = 1f / 120f;
    private const float SettleSeconds = 4f;
    private static readonly SpringSettings Vertical = BodyMassSettings.Default.verticalSpring;

    // The bob happens twice per stride, once per step.
    private static float StepRate(GaitProfile gait) => gait.speed / GaitTiming.StepLength(gait.speed, gait);

    // Drives the spring with a bob of amplitude 1 aimed ahead by leadCycles and measures the last bob cycle: its
    // amplitude and how far its peak trails the unaimed bob's peak, as a share of the cycle.
    private static (float gain, float lagShare) FollowBob((float bobRate, float leadCycles) drive) {
        SecondOrderDynamics spring = new SecondOrderDynamics(0f);
        float period = 1f / drive.bobRate;
        (float peak, float peakTime) followed = (float.NegativeInfinity, 0f);
        for (float time = 0f; time < SettleSeconds + period; time += Frame) {
            float target = Mathf.Sin(2f * Mathf.PI * (drive.bobRate * time + drive.leadCycles));
            float value = spring.Update((target, Frame), Vertical);
            if (time >= SettleSeconds && value > followed.peak) {
                followed = (value, time);
            }
        }
        float unaimedPeakTime = SettleSeconds + Mathf.Repeat(period / 4f - SettleSeconds, period);
        float lag = Mathf.Repeat(followed.peakTime - unaimedPeakTime + period / 2f, period) - period / 2f;
        return (followed.peak, lag / period);
    }

    [Test]
    public void ThePredictedPhaseLagMatchesTheSpring([Values(1.5f, 2.5f, 3.6f)] float bobRate) {
        (float gain, float lagShare) unaimed = FollowBob((bobRate, 0f));
        Assert.That(unaimed.lagShare, Is.EqualTo(Vertical.PhaseLagCycles(bobRate)).Within(0.02f));
    }

    [Test]
    public void AimedByItsLagTheBodyBobsInTimeWithTheRun() {
        float bobRate = StepRate(GaitProfile.Run);
        (float gain, float lagShare) aimed = FollowBob((bobRate, Vertical.PhaseLagCycles(bobRate)));
        Assert.That(aimed.lagShare, Is.InRange(-0.03f, 0.03f), "peaks with the gait's bob");
        Assert.That(aimed.gain, Is.InRange(0.7f, 1.1f), "no resonant amplification, and most of the bob gets through");
    }

    [Test]
    public void AimedByItsLagTheBodyBobsInTimeWithTheWalk() {
        float bobRate = StepRate(GaitProfile.Walk);
        (float gain, float lagShare) aimed = FollowBob((bobRate, Vertical.PhaseLagCycles(bobRate)));
        Assert.That(aimed.lagShare, Is.InRange(-0.03f, 0.03f));
        Assert.That(aimed.gain, Is.InRange(0.85f, 1.1f));
    }

    [Test]
    public void ItsNaturalFrequencyIsAboveTheRunningStepRate() {
        Assert.That(Vertical.frequency, Is.GreaterThan(1.5f * StepRate(GaitProfile.Run)));
    }
}
