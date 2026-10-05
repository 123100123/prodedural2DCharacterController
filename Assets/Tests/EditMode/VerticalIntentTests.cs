using NUnit.Framework;

public class VerticalIntentTests {
    [Test]
    public void TheDeadzoneSwallowsARestingStickAndRescalesTheRest() {
        Assert.That(VerticalIntent.ApplyDeadzone(0.1f, 0.2f), Is.EqualTo(0f));
        Assert.That(VerticalIntent.ApplyDeadzone(-0.2f, 0.2f), Is.EqualTo(0f));
        Assert.That(VerticalIntent.ApplyDeadzone(0.6f, 0.2f), Is.EqualTo(0.5f).Within(1e-5f), "starts from zero past the deadzone");
        Assert.That(VerticalIntent.ApplyDeadzone(-1f, 0.2f), Is.EqualTo(-1f).Within(1e-5f), "keys still give a full push");
        Assert.That(VerticalIntent.ApplyDeadzone(1.3f, 0.2f), Is.EqualTo(1f).Within(1e-5f), "never past full");
    }

    [Test]
    public void HoldingADirectionIsOnePress() {
        VerticalIntent intent = new VerticalIntent();
        (float axis, bool upPressed, bool downPressed) first = intent.Update(1f);
        (float axis, bool upPressed, bool downPressed) second = intent.Update(1f);

        Assert.That(first.axis, Is.EqualTo(1f));
        Assert.That(first.upPressed, Is.True);
        Assert.That(first.downPressed, Is.False);
        Assert.That(second.upPressed, Is.False, "still held, not pressed again");
    }

    [Test]
    public void ReleasingAndPushingAgainOrFlippingPressesAgain() {
        VerticalIntent intent = new VerticalIntent();
        intent.Update(-1f);
        intent.Update(0f);
        Assert.That(intent.Update(-1f).downPressed, Is.True, "down again after a release");
        (float axis, bool upPressed, bool downPressed) flipped = intent.Update(1f);
        Assert.That(flipped.upPressed, Is.True, "straight from down to up");
        Assert.That(flipped.downPressed, Is.False);
    }

    [Test]
    public void AStickPartWayIntoItsTravelIsNotYetAPress() {
        VerticalIntent intent = new VerticalIntent();
        (float axis, bool upPressed, bool downPressed) nudge = intent.Update(0.4f);
        Assert.That(nudge.axis, Is.GreaterThan(0f));
        Assert.That(nudge.upPressed, Is.False);
        Assert.That(new PlayerCommands { VerticalAxis = nudge.axis }.UpHeld, Is.False);
        Assert.That(new PlayerCommands { VerticalAxis = -0.8f }.DownHeld, Is.True);
    }
}
