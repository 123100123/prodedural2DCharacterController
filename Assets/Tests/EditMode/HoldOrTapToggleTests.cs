using NUnit.Framework;

public class HoldOrTapToggleTests {
    private const float TapMax = 0.25f;

    [Test]
    public void IsOnOnlyWhileHeldForLongPresses() {
        HoldOrTapToggle mode = new HoldOrTapToggle(TapMax);

        Assert.IsTrue(mode.Update((true, false, true), 0f));
        Assert.IsTrue(mode.Update((false, false, true), 1f));
        Assert.IsFalse(mode.Update((false, true, false), 1.5f), "a long press does not latch");
    }

    [Test]
    public void InstantTapWithinOneFrameLatchesOn() {
        HoldOrTapToggle mode = new HoldOrTapToggle(TapMax);

        Assert.IsTrue(mode.Update((true, true, false), 0f), "touchpad right-click: down and up in the same frame");
        Assert.IsTrue(mode.Update((false, false, false), 2f));
    }

    [Test]
    public void CancelingALatchedTapStandsDownAndTheNextTapLatchesAgain() {
        HoldOrTapToggle mode = new HoldOrTapToggle(TapMax);
        mode.Update((true, false, true), 0f);
        Assert.IsTrue(mode.Update((false, true, false), 0.1f), "a tap latches");

        mode.CancelLatch();
        Assert.IsFalse(mode.Update((false, false, false), 0.2f), "the latch is gone");
        mode.Update((true, false, true), 3f);
        Assert.IsTrue(mode.Update((false, true, false), 3.1f), "the next normal tap latches again");
    }

    [Test]
    public void CancelingWhileHeldKeepsTheHoldButItsQuickReleaseDoesNotLatch() {
        HoldOrTapToggle mode = new HoldOrTapToggle(TapMax);
        mode.Update((true, false, true), 0f);

        mode.CancelLatch();
        Assert.IsTrue(mode.Update((false, false, true), 0.1f), "still held");
        Assert.IsFalse(mode.Update((false, true, false), 0.15f), "released within the tap time: no latch");
        mode.Update((true, false, true), 3f);
        Assert.IsTrue(mode.Update((false, true, false), 3.1f), "the next tap latches");
    }

    [Test]
    public void SecondTapLatchesOff() {
        HoldOrTapToggle mode = new HoldOrTapToggle(TapMax);
        mode.Update((true, false, true), 0f);
        mode.Update((false, true, false), 0.1f);

        mode.Update((true, false, true), 3f);
        Assert.IsFalse(mode.Update((false, true, false), 3.1f));
    }
}
