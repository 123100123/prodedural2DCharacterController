using NUnit.Framework;
using UnityEngine;

public class AirPoseTests {
    private const float RunSpeed = 9f;
    private static readonly AirPoseSettings Settings = new AirPoseSettings();

    private static AirPose Solve(float vertical, float forward, float timeSinceLaunch = 1f) {
        return AirbornePoseSolver.Solve(new AirbornePoseSolver.Motion((vertical, forward), RunSpeed, timeSinceLaunch), Settings);
    }

    [Test]
    public void ThePhaseWeightsAlwaysAddUpToOne() {
        for (float speed = -20f; speed <= 20f; speed += 0.25f) {
            AirPhase phase = AirPhase.From(speed, Settings.apexBand);
            Assert.That(phase.Ascent + phase.Apex + phase.Descent, Is.EqualTo(1f).Within(0.0001f), $"at {speed}");
            Assert.That(phase.Ascent, Is.InRange(0f, 1f));
            Assert.That(phase.Apex, Is.InRange(0f, 1f));
            Assert.That(phase.Descent, Is.InRange(0f, 1f));
        }
    }

    [Test]
    public void ItIsApexAtTheTopRisingWhileRisingFastAndFallingWhileFallingFast() {
        Assert.That(AirPhase.From(0f, 5f).Apex, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(AirPhase.From(12f, 5f).Ascent, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(AirPhase.From(-12f, 5f).Descent, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void TheWeightsAreSymmetricAboutTheApexAndMoveSmoothly() {
        AirPhase up = AirPhase.From(2f, 5f);
        AirPhase down = AirPhase.From(-2f, 5f);
        Assert.That(up.Ascent, Is.EqualTo(down.Descent).Within(0.0001f));
        Assert.That(up.Apex, Is.EqualTo(down.Apex).Within(0.0001f));

        float previous = AirPhase.From(-20f, 5f).Ascent;
        for (float speed = -20f; speed <= 20f; speed += 0.1f) {
            float ascent = AirPhase.From(speed, 5f).Ascent;
            Assert.That(ascent, Is.GreaterThanOrEqualTo(previous - 0.0001f), $"ascent grows with speed at {speed}");
            Assert.That(ascent - previous, Is.LessThan(0.05f), $"no jump at {speed}");
            previous = ascent;
        }
    }

    [Test]
    public void TheRisingPoseDrivesTheLeadKneeUpAndTheArmsUpAndTheDescentReachesDown() {
        AirPose rising = Solve(12f, 0f);
        AirPose falling = Solve(-12f, 0f);
        Assert.That(rising.leadFoot.y, Is.GreaterThan(falling.leadFoot.y), "the lead knee is higher rising");
        Assert.That(rising.armFront, Is.GreaterThan(falling.armFront), "arms are thrown up rising and settle falling");
        Assert.That(rising.chestPitch, Is.LessThan(falling.chestPitch), "the chest extends rising and folds falling");
    }

    [Test]
    public void TheHangTucksTheLegsMoreThanTheFall() {
        AirPose apex = Solve(0f, 0f);
        AirPose falling = Solve(-12f, 0f);
        Assert.That(apex.trailFoot.y, Is.GreaterThan(falling.trailFoot.y), "the trailing leg is drawn up");
    }

    [Test]
    public void ARunningFlightLeansForwardMoreAndCarriesTheArmsApart() {
        AirPose standingRise = Solve(12f, 0f);
        AirPose runningRise = Solve(12f, RunSpeed);
        Assert.That(runningRise.lean, Is.GreaterThan(standingRise.lean));
        Assert.That(Mathf.Abs(runningRise.armFront - runningRise.armBack), Is.GreaterThan(Mathf.Abs(standingRise.armFront - standingRise.armBack)));
        Assert.That(Solve(-12f, RunSpeed).lean, Is.GreaterThan(Solve(-12f, 0f).lean));
    }

    [Test]
    public void TheRunningPoseFadesInSmoothlyWithForwardSpeed() {
        float previous = AirbornePoseSolver.RunningShare(new AirbornePoseSolver.Motion((0f, 0f), RunSpeed, 1f), Settings);
        Assert.That(previous, Is.EqualTo(0f));
        for (float speed = 0.05f; speed <= RunSpeed; speed += 0.05f) {
            float share = AirbornePoseSolver.RunningShare(new AirbornePoseSolver.Motion((0f, speed), RunSpeed, 1f), Settings);
            Assert.That(share, Is.GreaterThanOrEqualTo(previous - 0.0001f));
            previous = share;
        }
        Assert.That(previous, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(AirbornePoseSolver.RunningShare(new AirbornePoseSolver.Motion((0f, -RunSpeed), RunSpeed, 1f), Settings), Is.EqualTo(1f).Within(0.0001f), "either direction");
    }

    [Test]
    public void ThePushOffKeepsTheTrailingLegBackAndDownJustAfterTakeOffAndFadesAway() {
        AirPose atLaunch = Solve(12f, 0f, 0f);
        AirPose later = Solve(12f, 0f, 1f);
        Assert.That(atLaunch.trailFoot.y, Is.LessThan(later.trailFoot.y), "reaches further down at take-off");
        Assert.That(atLaunch.trailFoot.x, Is.LessThan(later.trailFoot.x), "and further back");
        Assert.That(AirbornePoseSolver.PushOff(0f, Settings), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(AirbornePoseSolver.PushOff(Settings.pushOffDuration, Settings), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(AirbornePoseSolver.PushOff(10f, Settings), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void AFallHasNoPushOff() {
        AirPose fallingAtOnce = Solve(-12f, 0f, 0f);
        AirPose fallingLater = Solve(-12f, 0f, 2f);
        Assert.That(fallingAtOnce.trailFoot, Is.EqualTo(fallingLater.trailFoot), "the push-off belongs to the rise only");
    }

    [Test]
    public void ThePoseChangesContinuouslyAcrossTheWholeFlight() {
        AirPose previous = Solve(14f, 5f, 0.5f);
        for (float speed = 14f; speed >= -20f; speed -= 0.05f) {
            AirPose pose = Solve(speed, 5f, 0.5f);
            Assert.That(Vector2.Distance(pose.leadFoot, previous.leadFoot), Is.LessThan(0.05f), $"lead foot at {speed}");
            Assert.That(Vector2.Distance(pose.trailFoot, previous.trailFoot), Is.LessThan(0.05f), $"trail foot at {speed}");
            Assert.That(Mathf.Abs(pose.armFront - previous.armFront), Is.LessThan(2f), $"arm at {speed}");
            previous = pose;
        }
    }

    [Test]
    public void TheFeetNeverReachBelowTheLegLengthInTheDefaultPoses() {
        const float legLength = 2.337f;
        const float hipBelowPivot = 0.27f;
        AirPose[] poses = { Settings.ascentStanding, Settings.ascentRunning, Settings.apex, Settings.descentStanding, Settings.descentRunning };
        foreach (AirPose pose in poses) {
            foreach (Vector2 foot in new[] { pose.leadFoot, pose.trailFoot }) {
                Assert.That(Vector2.Distance(foot, new Vector2(0f, -hipBelowPivot)), Is.LessThan(legLength), $"foot {foot}");
            }
        }
    }

    [Test]
    public void ACopyOfTheSettingsIsIndependentAndCopiesEveryField() {
        AirPoseSettings clone = Settings.Clone();
        clone.apexBand = 99f;
        Assert.That(Settings.apexBand, Is.EqualTo(new AirPoseSettings().apexBand));

        AirPoseSettings target = new AirPoseSettings();
        AirPoseSettings source = new AirPoseSettings { apexBand = 7f, runningBand = new Vector2(0.4f, 0.6f), pushOffDuration = 0.4f, pushOffReach = new Vector2(1f, 2f) };
        source.apex.lean = 12f;
        source.descentRunning.armFront = 99f;
        target.CopyFrom(source);
        Assert.That(target.apexBand, Is.EqualTo(7f));
        Assert.That(target.runningBand, Is.EqualTo(new Vector2(0.4f, 0.6f)));
        Assert.That(target.pushOffDuration, Is.EqualTo(0.4f));
        Assert.That(target.pushOffReach, Is.EqualTo(new Vector2(1f, 2f)));
        Assert.That(target.apex.lean, Is.EqualTo(12f));
        Assert.That(target.descentRunning.armFront, Is.EqualTo(99f));
    }
}
