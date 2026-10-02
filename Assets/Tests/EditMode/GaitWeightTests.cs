using NUnit.Framework;
using UnityEngine;

public class GaitWeightTests {
    private static readonly (Vector2 hipOffset, float reach) Leg = (new Vector2(0f, -0.26f), 2.25f);

    [Test]
    public void PelvisIsHighestOverItsSupportFoot() {
        float overFoot = GaitPose.HighestPelvisY((Vector2.zero, 0f), Leg);
        float besideFoot = GaitPose.HighestPelvisY((Vector2.zero, 1.2f), Leg);

        Assert.That(overFoot, Is.EqualTo(2.25f + 0.26f).Within(0.001f));
        Assert.That(besideFoot, Is.LessThan(overFoot - 0.2f), "spread feet lower the pelvis");
    }

    [Test]
    public void FootOutOfReachKeepsThePelvisAtFootLevel() {
        float y = GaitPose.HighestPelvisY((Vector2.zero, 10f), Leg);
        Assert.That(y, Is.EqualTo(0.26f).Within(0.001f));
    }

    [Test]
    public void StepTravelsForwardAndEndsOnTheFoothold() {
        StepArcShape shape = new StepArcShape();
        (Vector2 start, Vector2 end) step = (Vector2.zero, new Vector2(2f, 0f));
        float previousX = -1f;
        for (float t = 0f; t <= 1.0001f; t += 0.05f) {
            float x = StepArc.Evaluate((step.start, step.end, t), shape).x;
            Assert.That(x, Is.GreaterThanOrEqualTo(previousX - 0.0001f), "never slides backward");
            previousX = x;
        }
        Assert.That(Vector2.Distance(StepArc.Evaluate((step.start, step.end, 1f), shape), step.end), Is.LessThan(0.001f));
    }

    [Test]
    public void StepClearsTheHigherEndAndPlantsFirmly() {
        StepArcShape shape = new StepArcShape();
        (Vector2 start, Vector2 end) stepUp = (Vector2.zero, new Vector2(1.5f, 0.8f));

        Vector2 apex = StepArc.Evaluate((stepUp.start, stepUp.end, 0.4f), shape);
        Assert.That(apex.y, Is.GreaterThanOrEqualTo(0.8f + shape.liftHeight - 0.05f));

        float nearEnd = StepArc.Evaluate((stepUp.start, stepUp.end, 0.95f), shape).y;
        float atEnd = StepArc.Evaluate((stepUp.start, stepUp.end, 1f), shape).y;
        Assert.That((nearEnd - atEnd) / 0.05f, Is.GreaterThan(1f), "the foot is still coming down fast as it plants");
    }

    [Test]
    public void StepDurationFollowsDistanceWithinLimits() {
        StepArcShape shape = new StepArcShape();
        Assert.That(shape.DurationFor(0.1f), Is.EqualTo(shape.minStepDuration));
        Assert.That(shape.DurationFor(100f), Is.EqualTo(shape.maxStepDuration));
    }
}
