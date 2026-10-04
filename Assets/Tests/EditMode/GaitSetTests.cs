using NUnit.Framework;

public class GaitSetTests {
    private static GaitSet Gaits(float crouchShare) {
        return new GaitSet((GaitProfile.Walk, GaitProfile.Run, GaitProfile.Crouch), GaitTransitionSettings.Default, crouchShare);
    }

    [Test]
    public void StandingUpItBehavesExactlyAsTheWalkRunPairDid() {
        GaitSet standing = Gaits(0f);
        GaitSet pair = (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default);
        for (float speed = 0f; speed <= 9f; speed += 0.25f) {
            Assert.That(standing.CrouchWeightAt(speed), Is.EqualTo(0f), $"no crouch gait at {speed}");
            Assert.That(standing.MovingAt(speed), Is.EqualTo(LocomotionPace.MovingAt(speed, GaitProfile.Walk)).Within(0.0001f));
            Assert.That(standing.RunningAt(speed), Is.EqualTo(LocomotionPace.RunningAt(speed, (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default))).Within(0.0001f));
            Assert.That(standing.SettledAt(speed).stepLengthPerRootSpeed, Is.EqualTo(pair.SettledAt(speed).stepLengthPerRootSpeed).Within(0.0001f));
            Assert.That(standing.SettledAt(speed).lean, Is.EqualTo(pair.SettledAt(speed).lean).Within(0.0001f));
        }
    }

    [Test]
    public void TheCrouchGaitAppliesInFullAtTheCrouchSpeedAndFadesOutByWalkingSpeed() {
        GaitSet crouched = Gaits(1f);
        Assert.That(crouched.CrouchWeightAt(0f), Is.EqualTo(1f), "standing still crouched");
        Assert.That(crouched.CrouchWeightAt(GaitProfile.Crouch.speed), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(crouched.CrouchWeightAt(GaitProfile.Walk.speed), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(crouched.CrouchWeightAt(GaitProfile.Run.speed), Is.EqualTo(0f), "a crouched runner is still running until it slows");
        Assert.That(crouched.CrouchWeightAt(-GaitProfile.Crouch.speed), Is.EqualTo(1f).Within(0.0001f), "either direction");
    }

    [Test]
    public void TheCrouchWeightFallsSmoothlyWithSpeedAndRisesWithTheShare() {
        float previous = 1f;
        for (float speed = GaitProfile.Crouch.speed; speed <= GaitProfile.Walk.speed; speed += 0.05f) {
            float weight = Gaits(1f).CrouchWeightAt(speed);
            Assert.That(weight, Is.LessThanOrEqualTo(previous + 0.0001f), $"monotone at {speed}");
            previous = weight;
        }
        Assert.That(Gaits(0.5f).CrouchWeightAt(1f), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void ACrouchWalkMovesTheBodyAsMuchAsAWalkDoes() {
        GaitSet crouched = Gaits(1f);
        Assert.That(crouched.MovingAt(GaitProfile.Crouch.speed), Is.EqualTo(1f).Within(0.0001f), "full motion at the crouch speed");
        Assert.That(crouched.MovingAt(GaitProfile.Crouch.speed / 2f), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(crouched.ReferenceSpeedAt(GaitProfile.Walk.speed), Is.EqualTo(GaitProfile.Walk.speed).Within(0.0001f), "walking speed measures against the walk");
        for (float speed = 0f; speed <= GaitProfile.Run.speed; speed += 0.05f) {
            float moving = crouched.MovingAt(speed);
            Assert.That(moving, Is.InRange(0f, 1f));
        }
    }

    [Test]
    public void TheTargetSpeedBlendsFromTheWalkOrRunSpeedToTheCrouchSpeed() {
        Assert.That(Gaits(0f).TargetSpeed((1f, false)), Is.EqualTo(GaitProfile.Walk.speed));
        Assert.That(Gaits(0f).TargetSpeed((-1f, true)), Is.EqualTo(-GaitProfile.Run.speed));
        Assert.That(Gaits(1f).TargetSpeed((1f, true)), Is.EqualTo(GaitProfile.Crouch.speed).Within(0.0001f), "the run key is ignored crouched");
        Assert.That(Gaits(1f).TargetSpeed((-1f, false)), Is.EqualTo(-GaitProfile.Crouch.speed).Within(0.0001f));
        Assert.That(Gaits(1f).TargetSpeed((0f, true)), Is.EqualTo(0f));
        Assert.That(Gaits(0.5f).TargetSpeed((1f, true)), Is.EqualTo((GaitProfile.Run.speed + GaitProfile.Crouch.speed) / 2f).Within(0.0001f));
    }

    [Test]
    public void TheCrouchGaitTakesShortSlowStepsLowerThanAWalk() {
        GaitProfile crouchWalk = Gaits(1f).SettledAt(GaitProfile.Crouch.speed);
        GaitProfile walk = Gaits(0f).SettledAt(GaitProfile.Crouch.speed);
        Assert.That(crouchWalk.dutyFactor, Is.GreaterThan(walk.dutyFactor), "feet stay down longer");
        Assert.That(GaitTiming.StepLength(GaitProfile.Crouch.speed, crouchWalk), Is.LessThan(GaitTiming.StepLength(GaitProfile.Crouch.speed, walk)));
        Assert.That(crouchWalk.liftHeight, Is.LessThan(walk.liftHeight));
        Assert.That(crouchWalk.lean, Is.GreaterThan(walk.lean), "the torso folds forward");
    }

    [Test]
    public void TheCrouchGaitIsTheWalkRunBlendWhileRunning() {
        GaitProfile atRunSpeed = Gaits(1f).SettledAt(GaitProfile.Run.speed);
        Assert.That(atRunSpeed.dutyFactor, Is.EqualTo(GaitProfile.Run.dutyFactor).Within(0.0001f));
    }

    [Test]
    public void TheForecastSeesTheCrouchGait() {
        GaitProfile forecast = SwingForecast.GaitAt(GaitProfile.Crouch.speed, Gaits(1f));
        Assert.That(forecast.dutyFactor, Is.EqualTo(GaitProfile.Crouch.dutyFactor).Within(0.0001f));
    }
}
