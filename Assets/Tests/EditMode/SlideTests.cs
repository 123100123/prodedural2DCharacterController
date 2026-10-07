using NUnit.Framework;

// The pure running slide rules: when a crouch press starts a slide, the start speed, the friction and the stop, the
// cooldown, and why a slide ends.
public class SlideTests {
    private const float RunSpeed = 9f;

    private static SlideSettings Settings() => new SlideSettings();

    private static SlidePolicy.Entry Entry((bool crouchPressed, bool runIntent) input, float groundSpeed, (bool grounded, bool ready) state) {
        return new SlidePolicy.Entry(input, (groundSpeed, RunSpeed), state);
    }

    // Running flat out with a fresh crouch press, on the ground, cooled down.
    private static SlidePolicy.Entry Running() => Entry((true, true), RunSpeed, (true, true));

    private static SlidePolicy.Frame Sliding((bool jump, bool crouch) input, (bool groundLost, bool blocked, float speed) motion, bool roomToStand) {
        return new SlidePolicy.Frame(input, motion, roomToStand);
    }

    [Test]
    public void ACrouchPressWhileRunningStartsASlide() {
        Assert.That(SlidePolicy.CanStart(Running(), Settings()), Is.True);
        Assert.That(SlidePolicy.CanStart(Entry((true, true), -RunSpeed, (true, true)), Settings()), Is.True, "either way");
    }

    [Test]
    public void WithoutTheRunInputItSlidesOnlyNearTheRunSpeed() {
        SlideSettings settings = Settings();
        float threshold = settings.entrySpeedShare * RunSpeed;
        Assert.That(SlidePolicy.CanStart(Entry((true, false), threshold + 0.1f, (true, true)), settings), Is.True, "still fast after letting go of run");
        Assert.That(SlidePolicy.CanStart(Entry((true, false), threshold - 0.1f, (true, true)), settings), Is.False, "a walk only crouches");
        Assert.That(SlidePolicy.CanStart(Entry((true, false), 3.5f, (true, true)), settings), Is.False, "a walk only crouches");
    }

    [Test]
    public void RunningInputAtACrawlDoesNotSlide() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.CanStart(Entry((true, true), settings.minSpeed * 0.5f, (true, true)), settings), Is.False, "just setting off");
        Assert.That(SlidePolicy.CanStart(Entry((true, true), 0f, (true, true)), settings), Is.False, "standing with run held");
    }

    [Test]
    public void EachMissingConditionKeepsTheRun() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.CanStart(Entry((false, true), RunSpeed, (true, true)), settings), Is.False, "crouch held or latched, not pressed");
        Assert.That(SlidePolicy.CanStart(Entry((true, true), RunSpeed, (false, true)), settings), Is.False, "not on the ground");
        Assert.That(SlidePolicy.CanStart(Entry((true, true), RunSpeed, (true, false)), settings), Is.False, "cooling down");

        settings.enabled = false;
        Assert.That(SlidePolicy.CanStart(Running(), settings), Is.False, "switched off");
    }

    [Test]
    public void TheRunIntentNeedsTheRunInputAMoveAndNoBackpedalWhileAiming() {
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, false, 1)), Is.True);
        Assert.That(SlidePolicy.IsRunIntent((true, -1f, false, 1)), Is.True);
        Assert.That(SlidePolicy.IsRunIntent((false, 1f, false, 1)), Is.False, "no run input");
        Assert.That(SlidePolicy.IsRunIntent((true, SlidePolicy.InputDeadZone * 0.5f, false, 1)), Is.False, "no move");
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, true, 1)), Is.True, "aiming toward the side it runs");
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, true, -1)), Is.False, "backpedalling while aiming is a walk");
    }

    [Test]
    public void TheSlideStartsAtTheBoostOrFasterAndKeepsAFasterSpeed() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.StartSpeed(5f, settings), Is.EqualTo(settings.boostSpeed));
        Assert.That(SlidePolicy.StartSpeed(-5f, settings), Is.EqualTo(settings.boostSpeed), "unsigned");
        Assert.That(SlidePolicy.StartSpeed(settings.boostSpeed + 3f, settings), Is.EqualTo(settings.boostSpeed + 3f));
    }

    [Test]
    public void FrictionTakesAConstantSpeedPerSecondDownToAStop() {
        SlideSettings settings = Settings();
        settings.friction = 8f;
        Assert.That(SlidePolicy.NextSpeed(10f, 0.25f, settings), Is.EqualTo(8f).Within(1e-5f));
        Assert.That(SlidePolicy.NextSpeed(1f, 0.5f, settings), Is.EqualTo(0f), "never backward");

        float speed = 10f;
        int frames = 0;
        while (!SlidePolicy.IsTooSlow(speed, settings) && frames < 1000) {
            speed = SlidePolicy.NextSpeed(speed, 1f / 60f, settings);
            frames++;
        }
        float expectedSeconds = (10f - settings.minSpeed) / settings.friction;
        Assert.That(frames / 60f, Is.EqualTo(expectedSeconds).Within(2f / 60f), "stops after the time friction takes to reach the end speed");
    }

    [Test]
    public void ItIsTooSlowToSlideAtTheEndSpeed() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.IsTooSlow(settings.minSpeed, settings), Is.True);
        Assert.That(SlidePolicy.IsTooSlow(settings.minSpeed + 0.01f, settings), Is.False);
    }

    [Test]
    public void TheCooldownRunsFromTheEndOfTheLastSlide() {
        SlideSettings settings = Settings();
        float endedAt = 3f;
        float readyAt = endedAt + settings.cooldown;
        Assert.That(SlidePolicy.IsReady(endedAt + settings.cooldown * 0.5f, readyAt), Is.False);
        Assert.That(SlidePolicy.IsReady(readyAt, readyAt), Is.True);
        Assert.That(SlidePolicy.IsReady(0f, float.NegativeInfinity), Is.True, "never slid");
    }

    [Test]
    public void AJumpWinsOverEveryOtherWayOut() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.Continue(Sliding((true, false), (true, true, 0f), true), settings), Is.EqualTo(SlidePolicy.Outcome.Jump));
        Assert.That(SlidePolicy.Continue(Sliding((false, false), (true, true, 0f), true), settings), Is.EqualTo(SlidePolicy.Outcome.GroundLost));
        Assert.That(SlidePolicy.Continue(Sliding((false, false), (false, true, 0f), true), settings), Is.EqualTo(SlidePolicy.Outcome.HitWall));
        Assert.That(SlidePolicy.Continue(Sliding((false, true), (false, false, settings.minSpeed), true), settings), Is.EqualTo(SlidePolicy.Outcome.Stopped));
        Assert.That(SlidePolicy.Continue(Sliding((false, true), (false, false, 6f), true), settings), Is.EqualTo(SlidePolicy.Outcome.Slide));
    }

    [Test]
    public void ReleasingCrouchEndsTheSlideOnlyWhereThereIsRoomToStand() {
        SlideSettings settings = Settings();
        Assert.That(SlidePolicy.Continue(Sliding((false, false), (false, false, 6f), true), settings), Is.EqualTo(SlidePolicy.Outcome.Released));
        Assert.That(SlidePolicy.Continue(Sliding((false, false), (false, false, 6f), false), settings), Is.EqualTo(SlidePolicy.Outcome.Slide), "slides on under a low ceiling");
        Assert.That(SlidePolicy.Continue(Sliding((false, false), (false, false, settings.minSpeed * 0.5f), false), settings), Is.EqualTo(SlidePolicy.Outcome.Stopped), "until it stops");
    }
}
