using NUnit.Framework;
using UnityEngine;

public class LedgeGrabPolicyTests {
    private static readonly ClimbSettings Settings = new ClimbSettings();

    [Test]
    public void StandingWindowStartsAboveStepHeight() {
        (float minTop, float maxTop) window = LedgeGrabPolicy.StandingGrabWindow((stanceY: -5f, maxStepUp: 1f), Settings);

        Assert.IsFalse(LedgeGrabPolicy.IsInWindow(-4.2f, window), "a step is walked, not climbed");
        Assert.IsTrue(LedgeGrabPolicy.IsInWindow(-2f, window));
        Assert.IsFalse(LedgeGrabPolicy.IsInWindow(-5f + Settings.maxGrabRise + 0.1f, window), "too high to reach");
    }

    [Test]
    public void AirWindowIsAroundTheShoulders() {
        (float minTop, float maxTop) window = LedgeGrabPolicy.AirGrabWindow((shoulderY: 0f, armReach: 1.7f), Settings);

        Assert.IsTrue(LedgeGrabPolicy.IsInWindow(0.5f, window));
        Assert.IsFalse(LedgeGrabPolicy.IsInWindow(-Settings.airGrabBelowShoulder - 0.1f, window));
        Assert.IsFalse(LedgeGrabPolicy.IsInWindow(1.7f, window));
    }

    [Test]
    public void HangNeverMovesAHigherBodyDown() {
        Ledge ledge = new Ledge(new Vector2(0f, 0f), 1);
        Vector2 bodyAboveHang = new Vector2(-0.5f, -1f);

        (Vector2 hang, Vector2 pulled, Vector2 mounted, Vector2 standing) keyframes = LedgeGrabPolicy.BodyKeyframes((ledge, bodyAboveHang, 2.4f), Settings);

        Assert.That(keyframes.hang.y, Is.EqualTo(bodyAboveHang.y));
        Assert.That(keyframes.hang.x, Is.EqualTo(-Settings.hangInset));
    }

    [Test]
    public void ClimbEndsStandingOnTopPastTheEdge() {
        Ledge ledge = new Ledge(new Vector2(3f, 1f), -1);

        (Vector2 hang, Vector2 pulled, Vector2 mounted, Vector2 standing) keyframes = LedgeGrabPolicy.BodyKeyframes((ledge, new Vector2(3.5f, -2f), 2.4f), Settings);

        Assert.That(keyframes.standing.y, Is.EqualTo(1f + 2.4f));
        Assert.That(keyframes.standing.x, Is.LessThan(3f), "stands on the ledge side of the edge");
        Assert.That(keyframes.pulled.y, Is.GreaterThan(1f));
    }
}
