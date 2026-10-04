using NUnit.Framework;

public class CrouchIntentTests {
    [Test]
    public void CrouchesWhenAskedTo() {
        Assert.That(CrouchIntent.Resolve((true, false, false), true), Is.True);
        Assert.That(CrouchIntent.Resolve((false, false, false), true), Is.False);
    }

    [Test]
    public void StaysCrouchedUnderACeilingTooLowToStandUnder() {
        Assert.That(CrouchIntent.Resolve((false, true, true), true), Is.True, "released, but no room");
        Assert.That(CrouchIntent.Resolve((false, true, false), true), Is.False, "released and room: stands");
    }

    [Test]
    public void ACeilingOnlyHoldsACharacterThatIsAlreadyCrouched() {
        Assert.That(CrouchIntent.Resolve((false, false, true), true), Is.False, "standing under it: nothing makes it crouch");
    }

    [Test]
    public void StatesThatCannotCrouchNeverDo() {
        Assert.That(CrouchIntent.Resolve((true, true, true), false), Is.False);
    }
}
