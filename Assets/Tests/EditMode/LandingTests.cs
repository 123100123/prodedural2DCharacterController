using NUnit.Framework;
using UnityEngine;

public class LandingTests {
    private static readonly LandingSettings Landing = new LandingSettings();
    private static readonly GaitSet Gaits = new GaitSet((GaitProfile.Walk, GaitProfile.Run, GaitProfile.Crouch), GaitTransitionSettings.Default, 0f);

    [Test]
    public void TheTimeToImpactMatchesAStepByStepFall() {
        const float gravity = 48f;
        foreach ((float distance, float speed) fall in new[] { (3f, 0f), (3f, 12f), (0.4f, 20f), (10f, 2f) }) {
            float simulated = 0f;
            float distance = 0f;
            float speed = fall.speed;
            const float step = 0.0001f;
            while (distance < fall.distance) {
                speed += gravity * step;
                distance += speed * step;
                simulated += step;
            }
            Assert.That(LandingMath.TimeToImpact((fall.distance, fall.speed), gravity), Is.EqualTo(simulated).Within(0.002f), $"{fall.distance} u at {fall.speed} u/s");
        }
    }

    [Test]
    public void TheTimeToImpactIsZeroAtTheGroundAndHandlesNoGravity() {
        Assert.That(LandingMath.TimeToImpact((0f, 5f), 48f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(LandingMath.TimeToImpact((-1f, 5f), 48f), Is.EqualTo(0f).Within(0.0001f), "already through it");
        Assert.That(LandingMath.TimeToImpact((10f, 5f), 0f), Is.EqualTo(2f).Within(0.0001f));
        Assert.That(float.IsPositiveInfinity(LandingMath.TimeToImpact((10f, 0f), 0f)), Is.True);
    }

    [Test]
    public void ThePrepRisesFromNothingToOneAsTheGroundArrives() {
        Assert.That(LandingMath.Prep(1f, Landing.prepTime), Is.EqualTo(0f));
        Assert.That(LandingMath.Prep(Landing.prepTime, Landing.prepTime), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(LandingMath.Prep(0f, Landing.prepTime), Is.EqualTo(1f).Within(0.0001f));
        float previous = 0f;
        for (float time = Landing.prepTime; time >= 0f; time -= 0.005f) {
            float prep = LandingMath.Prep(time, Landing.prepTime);
            Assert.That(prep, Is.GreaterThanOrEqualTo(previous - 0.0001f));
            Assert.That(prep - previous, Is.LessThan(0.1f));
            previous = prep;
        }
    }

    [Test]
    public void TheGroundIsLookedForFurtherTheFasterTheFall() {
        Assert.That(LandingMath.LookAhead((0f, 0f), Landing), Is.EqualTo(Landing.lookAheadMin));
        Assert.That(LandingMath.LookAhead((40f, 0f), Landing), Is.EqualTo(40f * Landing.prepTime).Within(0.0001f));
        Assert.That(LandingMath.LookAhead((-5f, 48f), Landing), Is.GreaterThanOrEqualTo(Landing.lookAheadMin), "rising");
    }

    // The reach must start from nothing: at the distance where the ground first comes into view, the time to impact is
    // the prep time, so the prep is 0 there (no jump from 0 to a share in one frame).
    [Test]
    public void TheReachStartsFromNothingWhereTheGroundFirstComesIntoView() {
        const float gravity = 48f;
        foreach (float speed in new[] { 0f, 3f, 7f, 20f, 40f }) {
            float seen = LandingMath.LookAhead((speed, gravity), Landing);
            if (seen <= Landing.lookAheadMin + 0.0001f) {
                continue;
            }
            float timeToImpact = LandingMath.TimeToImpact((seen, speed), gravity);
            Assert.That(LandingMath.Prep(timeToImpact, Landing.prepTime), Is.LessThan(0.02f), $"at {speed} u/s");
        }
    }

    [Test]
    public void TheImpactShareRunsFromLightToHard() {
        Assert.That(LandingMath.ImpactShare(0f, Landing), Is.EqualTo(0f));
        Assert.That(LandingMath.ImpactShare(Landing.lightImpactSpeed, Landing), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(LandingMath.ImpactShare(Landing.hardImpactSpeed, Landing), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(LandingMath.ImpactShare(100f, Landing), Is.EqualTo(1f));
        float previous = 0f;
        for (float speed = 0f; speed <= 30f; speed += 0.25f) {
            float share = LandingMath.ImpactShare(speed, Landing);
            Assert.That(share, Is.GreaterThanOrEqualTo(previous));
            previous = share;
        }
    }

    [Test]
    public void AHarderLandingSquashesMoreCostsMoreSpeedAndHoldsThePelvisLower() {
        Assert.That(LandingMath.Sink(0f, Landing), Is.EqualTo(Landing.sinkLight).Within(0.0001f));
        Assert.That(LandingMath.Sink(1f, Landing), Is.EqualTo(Landing.sinkHard).Within(0.0001f));
        Assert.That(LandingMath.Sink(1f, Landing), Is.GreaterThan(LandingMath.Sink(0f, Landing)));
        Assert.That(LandingMath.SpeedAfter(9f, 0f, Landing), Is.EqualTo(9f), "a light landing costs nothing");
        Assert.That(LandingMath.SpeedAfter(9f, 1f, Landing), Is.EqualTo(9f * (1f - Landing.speedLossHard)).Within(0.0001f));
        Assert.That(LandingMath.SpeedAfter(-9f, 1f, Landing), Is.EqualTo(-9f * (1f - Landing.speedLossHard)).Within(0.0001f));
        Assert.That(LandingMath.Compression(1f, 0f, Landing), Is.EqualTo(Landing.compressionHard).Within(0.0001f));
        Assert.That(LandingMath.Compression(1f, 1f, Landing), Is.LessThan(LandingMath.Compression(1f, 0f, Landing)), "already low");
        Assert.That(LandingMath.Compression(0f, 0f, Landing), Is.EqualTo(0f));
    }

    [Test]
    public void TheCarriedFallSpeedIsCapped() {
        Assert.That(LandingMath.CarriedFallSpeed(10f, Landing), Is.EqualTo(10f * Landing.speedCarry).Within(0.0001f));
        Assert.That(LandingMath.CarriedFallSpeed(100f, Landing), Is.EqualTo(Landing.maxCarriedSpeed));
        Assert.That(LandingMath.CarriedFallSpeed(-3f, Landing), Is.EqualTo(0f));
    }

    [Test]
    public void StandingLandingsTakeTheStanceEitherSideOfTheBody() {
        LandingStance.Columns facingRight = LandingStance.Of((10f, 1, 0f), (0.22f, Gaits, Landing.movingLandingShare));
        Assert.That(facingRight.IsMoving, Is.False);
        Assert.That(facingRight.Lead, Is.EqualTo(10.22f).Within(0.0001f));
        Assert.That(facingRight.Trail, Is.EqualTo(9.78f).Within(0.0001f));

        LandingStance.Columns facingLeft = LandingStance.Of((10f, -1, 0f), (0.22f, Gaits, Landing.movingLandingShare));
        Assert.That(facingLeft.Lead, Is.EqualTo(9.78f).Within(0.0001f), "the lead foot is the one ahead");
    }

    [Test]
    public void ARunningLandingPutsOneFootAheadAndOneFarBehindLikeAContact() {
        LandingStance.Columns running = LandingStance.Of((10f, 1, 9f), (0.22f, Gaits, Landing.movingLandingShare));
        GaitProfile gait = Gaits.SettledAt(9f);
        Assert.That(running.IsMoving, Is.True);
        Assert.That(running.Lead, Is.EqualTo(10f + GaitTiming.ContactLead(9f, gait)).Within(0.0001f));
        Assert.That(running.Trail, Is.EqualTo(10f - GaitTiming.ToeOffLag(9f, gait)).Within(0.0001f));
        Assert.That(running.Lead - running.Trail, Is.GreaterThan(1.5f), "a long stride apart");
        Assert.That(running.Lead, Is.GreaterThan(10f));
        Assert.That(running.Trail, Is.LessThan(10f));
    }

    [Test]
    public void ALandingMovingLeftMirrorsOneMovingRight() {
        LandingStance.Columns right = LandingStance.Of((0f, 1, 6f), (0.22f, Gaits, Landing.movingLandingShare));
        LandingStance.Columns left = LandingStance.Of((0f, 1, -6f), (0.22f, Gaits, Landing.movingLandingShare));
        Assert.That(left.Lead, Is.EqualTo(-right.Lead).Within(0.0001f));
        Assert.That(left.Trail, Is.EqualTo(-right.Trail).Within(0.0001f));
    }

    [Test]
    public void OnlyAFastEnoughLandingCarriesTheGaitOn() {
        float walk = GaitProfile.Walk.speed;
        Assert.That(LandingStance.Of((0f, 1, walk * Landing.movingLandingShare * 0.9f), (0.22f, Gaits, Landing.movingLandingShare)).IsMoving, Is.False);
        Assert.That(LandingStance.Of((0f, 1, walk * Landing.movingLandingShare * 1.1f), (0.22f, Gaits, Landing.movingLandingShare)).IsMoving, Is.True);
    }

    [Test]
    public void ContinuingTheRhythmMakesTheChosenLegStepFirst([Values(true, false)] bool frontStepsFirst) {
        StepScheduler scheduler = new StepScheduler();
        scheduler.RestartFromStance();
        scheduler.ContinueRhythm(frontStepsFirst);

        // The leg that trails by position is the other one, so without ContinueRhythm the other leg would be chosen.
        bool frontLegTrails = !frontStepsFirst;
        bool cued = false;
        for (int tick = 0; tick < 180 && !cued; tick++) {
            scheduler.Advance((7f, 2.5f, 0.4f, 1f / 60f, frontLegTrails), GaitTransitionSettings.Default);
            cued = scheduler.IsCued(true) || scheduler.IsCued(false);
        }
        Assert.That(cued, Is.True);
        Assert.That(scheduler.IsCued(frontStepsFirst), Is.True, "the chosen leg lifts first");
        Assert.That(scheduler.IsCued(!frontStepsFirst), Is.False);
    }

    [Test]
    public void CopyFromCopiesEveryField() {
        LandingSettings target = new LandingSettings();
        LandingSettings source = new LandingSettings();
        foreach (System.Reflection.FieldInfo field in typeof(LandingSettings).GetFields()) {
            if (field.FieldType == typeof(float)) {
                field.SetValue(source, (float)field.GetValue(source) + 0.37f);
            }
            else if (field.FieldType == typeof(SpringSettings)) {
                field.SetValue(source, new SpringSettings(1.5f, 0.6f, 0.2f));
            }
        }
        target.CopyFrom(source);
        foreach (System.Reflection.FieldInfo field in typeof(LandingSettings).GetFields()) {
            Assert.That(field.GetValue(target), Is.EqualTo(field.GetValue(source)), field.Name);
        }
    }
}
