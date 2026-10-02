using NUnit.Framework;
using UnityEngine;

public class BreathingTests {
    [Test]
    public void LungsFillThenEmptyOnceEveryBreath() {
        Assert.That(Breathing.Fullness(0f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Breathing.Fullness(Breathing.InhaleShare), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(Breathing.Fullness(1f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Breathing.Fullness(1.2f), Is.EqualTo(Breathing.Fullness(0.2f)).Within(0.0001f), "repeats every breath");
    }

    [Test]
    public void BreathingInIsQuickerThanBreathingOutAndNeverJumps() {
        Assert.That(Breathing.InhaleShare, Is.LessThan(0.5f));
        float previous = Breathing.Fullness(0f);
        for (float phase = 0.01f; phase <= 2f; phase += 0.01f) {
            float fullness = Breathing.Fullness(phase);
            Assert.That(Mathf.Abs(fullness - previous), Is.LessThan(0.05f));
            previous = fullness;
        }
    }
}
