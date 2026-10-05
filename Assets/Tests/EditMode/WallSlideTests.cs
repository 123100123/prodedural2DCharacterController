using NUnit.Framework;
using UnityEngine;

// The pure wall slide rules: catching a wall, ending a slide, the slide speed ramp, the wall jump kick and the steering
// lockout after it.
public class WallSlideTests {
    private static WallSettings Settings() => new WallSettings();

    // A slide that could start: falling, pressing in, wall from chest to knees, ground far below, no ledge.
    private static WallSlidePolicy.Catch Catchable((float verticalSpeed, bool pressing) motion, (bool chest, bool knee) contact, (float clearance, bool ledge) surroundings) {
        return new WallSlidePolicy.Catch(motion, contact, surroundings);
    }

    private static WallSlidePolicy.Catch Falling() => Catchable((-5f, true), (true, true), (float.PositiveInfinity, false));

    [Test]
    public void TheMoveInputPicksTheWallSidePastTheDeadZone() {
        Assert.That(WallSlidePolicy.PressedSide(1f), Is.EqualTo(1));
        Assert.That(WallSlidePolicy.PressedSide(-0.6f), Is.EqualTo(-1));
        Assert.That(WallSlidePolicy.PressedSide(0f), Is.EqualTo(0));
        Assert.That(WallSlidePolicy.PressedSide(WallSlidePolicy.InputDeadZone * 0.5f), Is.EqualTo(0), "a resting stick does not press");
    }

    [Test]
    public void AFallingCharacterPressingIntoATallWallCatchesIt() {
        Assert.That(WallSlidePolicy.CanCatch(Falling(), Settings()), Is.True);
    }

    [Test]
    public void ItCatchesTheWallJustBeforeTheTopOfARiseButNotWhileRisingFast() {
        WallSettings settings = Settings();
        Assert.That(WallSlidePolicy.CanCatch(Catchable((settings.catchRiseSpeed * 0.5f, true), (true, true), (float.PositiveInfinity, false)), settings), Is.True);
        Assert.That(WallSlidePolicy.CanCatch(Catchable((settings.catchRiseSpeed + 3f, true), (true, true), (float.PositiveInfinity, false)), settings), Is.False);
    }

    [Test]
    public void EachMissingConditionKeepsTheFlight() {
        WallSettings settings = Settings();
        Assert.That(WallSlidePolicy.CanCatch(Catchable((-5f, false), (true, true), (float.PositiveInfinity, false)), settings), Is.False, "not pressing into the wall");
        Assert.That(WallSlidePolicy.CanCatch(Catchable((-5f, true), (false, true), (float.PositiveInfinity, false)), settings), Is.False, "the wall ends below the chest");
        Assert.That(WallSlidePolicy.CanCatch(Catchable((-5f, true), (true, false), (float.PositiveInfinity, false)), settings), Is.False, "the wall ends above the knees");
        Assert.That(WallSlidePolicy.CanCatch(Catchable((-5f, true), (true, true), (settings.minGroundClearance * 0.5f, false)), settings), Is.False, "about to land");
        Assert.That(WallSlidePolicy.CanCatch(Catchable((-5f, true), (true, true), (float.PositiveInfinity, true)), settings), Is.False, "a ledge to grab instead");

        settings.enabled = false;
        Assert.That(WallSlidePolicy.CanCatch(Falling(), settings), Is.False, "switched off");
    }

    [Test]
    public void ALedgeInReachWinsOverEveryOtherWayOut() {
        WallSettings settings = Settings();
        Assert.That(WallSlidePolicy.Continue((true, true, false, 1f), settings), Is.EqualTo(WallSlidePolicy.Outcome.Ledge));
        Assert.That(WallSlidePolicy.Continue((false, true, false, 1f), settings), Is.EqualTo(WallSlidePolicy.Outcome.Ground));
        Assert.That(WallSlidePolicy.Continue((false, false, false, 1f), settings), Is.EqualTo(WallSlidePolicy.Outcome.WallEnded));
        Assert.That(WallSlidePolicy.Continue((false, false, true, 1f), settings), Is.EqualTo(WallSlidePolicy.Outcome.Released));
        Assert.That(WallSlidePolicy.Continue((false, false, true, 0f), settings), Is.EqualTo(WallSlidePolicy.Outcome.Slide));
    }

    [Test]
    public void LettingGoEndsTheSlideOnlyAfterTheGracePeriod() {
        WallSettings settings = Settings();
        const float Frame = 1f / 60f;
        float released = 0f;
        float elapsed = 0f;
        while (WallSlidePolicy.Continue((false, false, true, released), settings) == WallSlidePolicy.Outcome.Slide && elapsed < 1f) {
            released = WallSlidePolicy.ReleasedFor((released, false, Frame));
            elapsed += Frame;
        }
        Assert.That(elapsed, Is.EqualTo(settings.releaseGrace).Within(Frame * 1.5f), "drops off once the grace is over");

        float pressedAgain = WallSlidePolicy.ReleasedFor((settings.releaseGrace * 0.9f, true, Frame));
        Assert.That(pressedAgain, Is.Zero, "pressing in again restarts the grace");
    }

    [Test]
    public void AFastFallBrakesToTheSlideSpeedAndNeverPassesIt() {
        WallSettings settings = Settings();
        const float Frame = 1f / 60f;
        float speed = -18f;
        float previous = speed;
        for (int frame = 0; frame < 120; frame++) {
            speed = WallSlidePolicy.SlideSpeed((speed, Frame), settings);
            Assert.That(speed, Is.GreaterThanOrEqualTo(previous - 0.0001f), "brakes monotonically");
            Assert.That(speed - previous, Is.LessThanOrEqualTo(settings.slideAcceleration * Frame + 0.0001f), "at the slide acceleration");
            previous = speed;
        }
        Assert.That(speed, Is.EqualTo(-settings.maxSlideSpeed).Within(0.0001f));
    }

    [Test]
    public void AStopAtTheTopOfAJumpRampsUpToTheSlideSpeed() {
        WallSettings settings = Settings();
        const float Frame = 1f / 60f;
        float speed = 0.5f;
        int frames = 0;
        while (speed > -settings.maxSlideSpeed + 0.0001f && frames < 600) {
            float next = WallSlidePolicy.SlideSpeed((speed, Frame), settings);
            Assert.That(next, Is.LessThan(speed), "speeds up downward");
            Assert.That(next, Is.GreaterThanOrEqualTo(-settings.maxSlideSpeed - 0.0001f), "never faster than the cap");
            speed = next;
            frames++;
        }
        float expectedSeconds = (settings.maxSlideSpeed + 0.5f) / settings.slideAcceleration;
        Assert.That(frames * Frame, Is.EqualTo(expectedSeconds).Within(Frame * 1.5f), "a ramp, not a jump to the cap");
    }

    [Test]
    public void AWallJumpKicksAwayFromTheWallAndUpOnEitherSide() {
        WallSettings settings = Settings();
        Vector2 offRightWall = WallSlidePolicy.WallJumpVelocity(1, settings);
        Vector2 offLeftWall = WallSlidePolicy.WallJumpVelocity(-1, settings);

        Assert.That(offRightWall.x, Is.EqualTo(-settings.jumpVelocity.x));
        Assert.That(offLeftWall.x, Is.EqualTo(settings.jumpVelocity.x));
        Assert.That(offRightWall.y, Is.EqualTo(settings.jumpVelocity.y));
        Assert.That(offLeftWall, Is.EqualTo(new Vector2(-offRightWall.x, offRightWall.y)), "mirrored");
    }

    [Test]
    public void TheWallJumpHeightIsTheBallisticRiseOfItsLaunch() {
        WallSettings settings = Settings();
        const float Gravity = 30f;
        float height = WallSlidePolicy.WallJumpHeight(Gravity, settings);
        Assert.That(JumpMath.LaunchSpeedForHeight(Gravity, height), Is.EqualTo(settings.jumpVelocity.y).Within(0.0001f));
    }

    [Test]
    public void TheWallCoyoteWindowFollowsDroppingOffTheWall() {
        WallSettings settings = Settings();
        Assert.That(WallSlidePolicy.IsWithinCoyote((10f + settings.coyoteTime * 0.5f, 10f, 1), settings), Is.True);
        Assert.That(WallSlidePolicy.IsWithinCoyote((10f + settings.coyoteTime * 2f, 10f, 1), settings), Is.False, "too late");
        Assert.That(WallSlidePolicy.IsWithinCoyote((10.05f, 10f, 0), settings), Is.False, "no wall was left");
        Assert.That(WallSlidePolicy.IsWithinCoyote((10.05f, float.NegativeInfinity, 1), settings), Is.False, "never left one");
    }

    // Every field, whatever its type, is changed in the source; the copy must match it in full.
    [Test]
    public void CopyFromCopiesEveryField() {
        WallSettings source = new WallSettings();
        foreach (System.Reflection.FieldInfo field in typeof(WallSettings).GetFields()) {
            object value = field.GetValue(source);
            if (value is float number) {
                field.SetValue(source, number + 1.234f);
            }
            else if (value is bool flag) {
                field.SetValue(source, !flag);
            }
            else if (value is Vector2 vector) {
                field.SetValue(source, vector + new Vector2(1.5f, -2.5f));
            }
            else {
                Assert.Fail($"{field.Name}: a field type this test does not change");
            }
        }
        WallSettings target = new WallSettings();
        target.CopyFrom(source);
        Assert.That(JsonUtility.ToJson(target), Is.EqualTo(JsonUtility.ToJson(source)));
        Assert.That(JsonUtility.ToJson(target), Is.Not.EqualTo(JsonUtility.ToJson(new WallSettings())));
    }

    [Test]
    public void TheSteeringLockoutIgnoresOnlySteeringBackTowardTheWallUntilItRunsOut() {
        const float Frame = 1f / 60f;
        SteeringLockout lockout = new SteeringLockout(1, 0.15f);

        Assert.That(lockout.Filter(1f), Is.Zero, "toward the wall is ignored");
        Assert.That(lockout.Filter(-1f), Is.EqualTo(-1f), "away from the wall steers");
        Assert.That(lockout.Filter(0f), Is.Zero);

        float elapsed = 0f;
        while (lockout.IsActive && elapsed < 1f) {
            lockout = lockout.Tick(Frame);
            elapsed += Frame;
        }
        Assert.That(elapsed, Is.EqualTo(0.15f).Within(Frame * 1.5f));
        Assert.That(lockout.Filter(1f), Is.EqualTo(1f), "steers freely once it has run out");
        Assert.That(SteeringLockout.None.Filter(0.7f), Is.EqualTo(0.7f), "no lockout by default");
        Assert.That(new SteeringLockout(-1, 0f).IsActive, Is.False, "a lockout of no time");
    }
}
