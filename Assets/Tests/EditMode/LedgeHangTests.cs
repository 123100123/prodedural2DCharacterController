using NUnit.Framework;
using UnityEngine;
using Choice = LedgeHangPolicy.Choice;

public class LedgeHangTests {
    private static LedgeHangSettings Settings() => new LedgeHangSettings();

    [Test]
    public void NoInputHolds() {
        Assert.That(LedgeHangPolicy.Decide((0f, 0f, false), Settings()), Is.EqualTo(Choice.Hold));
        Assert.That(LedgeHangPolicy.Decide((0.3f, -0.3f, false), Settings()), Is.EqualTo(Choice.Hold), "a light push is not a choice");
    }

    [Test]
    public void UpTowardOrJumpClimbs() {
        Assert.That(LedgeHangPolicy.Decide((0f, 1f, false), Settings()), Is.EqualTo(Choice.Climb), "up");
        Assert.That(LedgeHangPolicy.Decide((1f, 0f, false), Settings()), Is.EqualTo(Choice.Climb), "toward the ledge");
        Assert.That(LedgeHangPolicy.Decide((0f, 0f, true), Settings()), Is.EqualTo(Choice.Climb), "jump pulls up");
        Assert.That(LedgeHangPolicy.Decide((-1f, -1f, true), Settings()), Is.EqualTo(Choice.Climb), "jump wins over everything");
    }

    [Test]
    public void DownOrAwayLetsGo() {
        Assert.That(LedgeHangPolicy.Decide((0f, -1f, false), Settings()), Is.EqualTo(Choice.Drop), "down");
        Assert.That(LedgeHangPolicy.Decide((-1f, 0f, false), Settings()), Is.EqualTo(Choice.Drop), "away from the ledge");
        Assert.That(LedgeHangPolicy.Decide((1f, -1f, false), Settings()), Is.EqualTo(Choice.Drop), "down wins over toward");
    }

    [Test]
    public void AHangThatIsNotHoldableAlwaysClimbs() {
        LedgeHangSettings settings = Settings();
        settings.holdable = false;
        Assert.That(LedgeHangPolicy.Decide((-1f, -1f, false), settings), Is.EqualTo(Choice.Climb));
        Assert.That(LedgeHangPolicy.OnGrab(0f, settings), Is.EqualTo(Choice.Climb));
    }

    [Test]
    public void HoldingTowardTheLedgeAsItIsGrabbedClimbsStraightOn() {
        Assert.That(LedgeHangPolicy.OnGrab(1f, Settings()), Is.EqualTo(Choice.Climb));
        Assert.That(LedgeHangPolicy.OnGrab(0f, Settings()), Is.EqualTo(Choice.Hold));
        Assert.That(LedgeHangPolicy.OnGrab(-1f, Settings()), Is.EqualTo(Choice.Hold), "pushing away at the grab still hangs");
    }

    [Test]
    public void TheLatestChoiceIsRememberedUntilTheHang() {
        Assert.That(LedgeHangPolicy.Remember(Choice.Climb, Choice.Hold), Is.EqualTo(Choice.Climb), "releasing the stick keeps the climb");
        Assert.That(LedgeHangPolicy.Remember(Choice.Climb, Choice.Drop), Is.EqualTo(Choice.Drop), "changing the mind lets go");
        Assert.That(LedgeHangPolicy.Remember(Choice.Hold, Choice.Climb), Is.EqualTo(Choice.Climb));
    }

    [Test]
    public void TheIdleSwayStartsStillAndNeverLeansIntoTheWall() {
        LedgeHangSettings settings = Settings();
        Assert.That(LedgeHangPolicy.IdleSway((0f, -1), settings), Is.EqualTo(Vector2.zero), "no jump when the hang begins");
        float furthest = 0f;
        for (float time = 0f; time < 10f; time += 0.05f) {
            Vector2 sway = LedgeHangPolicy.IdleSway((time, -1), settings);
            Assert.That(sway.x, Is.LessThanOrEqualTo(0f), "away from a wall on the right");
            Assert.That(Mathf.Abs(sway.y), Is.LessThanOrEqualTo(settings.breathAmplitude + 1e-5f));
            furthest = Mathf.Max(furthest, -sway.x);
        }
        Assert.That(furthest, Is.EqualTo(settings.swayAmplitude).Within(0.005f), "it does sway");
        Assert.That(LedgeHangPolicy.IdleSway((1f, 1), settings).x, Is.GreaterThanOrEqualTo(0f), "away from a wall on the left");
    }

    [Test]
    public void TheSameLedgeIsLockedOutForAMomentAfterLettingGo() {
        LedgeRegrabLockout lockout = new LedgeRegrabLockout();
        Vector2 corner = new Vector2(3f, 2f);
        Assert.That(lockout.Blocks(corner, 0f), Is.False, "nothing locked yet");

        lockout.Lock(corner, 10.3f);
        Assert.That(lockout.Blocks(corner, 10.1f), Is.True);
        Assert.That(lockout.Blocks(corner + new Vector2(0.1f, -0.05f), 10.1f), Is.True, "the same ledge found a little apart");
        Assert.That(lockout.Blocks(corner + new Vector2(0f, -2f), 10.1f), Is.False, "a lower ledge can be grabbed");
        Assert.That(lockout.Blocks(corner, 10.3f), Is.False, "free again after the lockout");
    }
}
