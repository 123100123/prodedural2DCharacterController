using NUnit.Framework;
using UnityEngine;

public class TwoBoneIkTests {
    private const float Tolerance = 0.0005f;
    private static readonly (float upper, float lower) Lengths = (0.8f, 0.9f);

    [Test]
    public void ReachableTargetPutsTheTipOnTheTarget() {
        Vector2 target = new Vector2(1.0f, 0.6f);
        (Vector2 joint, Vector2 tip) = TwoBoneIk.Solve((Vector2.zero, target, Vector2.down), Lengths);

        Assert.That(Vector2.Distance(tip, target), Is.LessThan(Tolerance));
        Assert.That(joint.magnitude, Is.EqualTo(Lengths.upper).Within(Tolerance));
        Assert.That(Vector2.Distance(joint, tip), Is.EqualTo(Lengths.lower).Within(Tolerance));
    }

    [Test]
    public void OutOfReachTargetStretchesTheChainTowardIt() {
        Vector2 target = new Vector2(5f, 0f);
        (Vector2 _, Vector2 tip) = TwoBoneIk.Solve((Vector2.zero, target, Vector2.down), Lengths);

        Assert.That(tip.x, Is.EqualTo(Lengths.upper + Lengths.lower).Within(0.001f));
        Assert.That(tip.y, Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void TheJointBendsTowardTheHint() {
        Vector2 target = new Vector2(1.2f, 0f);
        (Vector2 jointDown, Vector2 _) = TwoBoneIk.Solve((Vector2.zero, target, Vector2.down), Lengths);
        (Vector2 jointUp, Vector2 _) = TwoBoneIk.Solve((Vector2.zero, target, Vector2.up), Lengths);

        Assert.That(jointDown.y, Is.LessThan(0f));
        Assert.That(jointUp.y, Is.GreaterThan(0f));
    }
}
