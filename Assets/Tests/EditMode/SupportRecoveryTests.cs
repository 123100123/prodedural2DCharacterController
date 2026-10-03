using NUnit.Framework;

public class SupportRecoveryTests {
    private static readonly BalanceSettings Balance = BalanceSettings.Default;

    [Test]
    public void FeetKeepingUpNeedNothing() {
        SupportRecovery.Response response = SupportRecovery.Assess((Balance.catchUpMargin, float.NegativeInfinity, 10f), Balance);
        Assert.That(response.CatchUp, Is.EqualTo(1f));
        Assert.That(response.NeedsBalanceStep, Is.False);
    }

    [Test]
    public void TrailingABitQuickensTheCadenceWithinItsLimit() {
        float slightly = SupportRecovery.Assess((Balance.catchUpMargin + 0.1f, float.NegativeInfinity, 10f), Balance).CatchUp;
        float more = SupportRecovery.Assess((Balance.catchUpMargin + 0.2f, float.NegativeInfinity, 10f), Balance).CatchUp;
        float far = SupportRecovery.Assess((Balance.catchUpMargin + 0.55f, float.NegativeInfinity, 10f), Balance).CatchUp;
        Assert.That(slightly, Is.GreaterThan(1f));
        Assert.That(more, Is.GreaterThan(slightly));
        Assert.That(far, Is.EqualTo(Balance.maxCatchUp));
    }

    [Test]
    public void OnlyAFootFarBehindTakesABalanceStep() {
        Assert.That(SupportRecovery.Assess((Balance.emergencyMargin - 0.01f, float.NegativeInfinity, 10f), Balance).NeedsBalanceStep, Is.False);
        Assert.That(SupportRecovery.Assess((Balance.emergencyMargin + 0.01f, float.NegativeInfinity, 10f), Balance).NeedsBalanceStep, Is.True);
    }

    [Test]
    public void AStanceRunningLongQuickensTheCadenceButTakesNoBalanceStep() {
        SupportRecovery.Response response = SupportRecovery.Assess((0f, Balance.emergencyMargin + 0.5f, 10f), Balance);
        Assert.That(response.CatchUp, Is.EqualTo(Balance.maxCatchUp));
        Assert.That(response.NeedsBalanceStep, Is.False);
    }

    [Test]
    public void NoSecondBalanceStepWithinTheCooldown() {
        float justAfter = Balance.emergencyCooldown - 0.01f;
        Assert.That(SupportRecovery.Assess((Balance.emergencyMargin + 1f, float.NegativeInfinity, justAfter), Balance).NeedsBalanceStep, Is.False);
    }

    [Test]
    public void AnOutOfTurnCueRephasesTheClockOnlyWhenTheLegLifts() {
        StepScheduler scheduler = new StepScheduler();
        GaitTransitionSettings transitions = GaitTransitionSettings.Default;
        scheduler.Advance((1f, 2f, 0.4f, 0.1f, true), transitions);
        scheduler.Advance((1f, 2f, 0.4f, 0.2f, true), transitions);
        float phaseBeforeCue = scheduler.Phase;

        scheduler.CueOutOfTurn(false);
        Assert.That(scheduler.Phase, Is.EqualTo(phaseBeforeCue), "cueing does not move the clock");
        Assert.That(scheduler.RecordLift(false), Is.True, "the out-of-turn lift re-phases the clock");
        Assert.That(scheduler.Phase, Is.EqualTo(GaitCycle.BackLegLiftPhase));
        Assert.That(scheduler.RecordLift(false), Is.False, "only once");
    }
}
