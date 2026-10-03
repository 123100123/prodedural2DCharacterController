using NUnit.Framework;
using UnityEngine;

// Support leg selection and the pelvis's motion over its support foot.
public class PelvisSupportTests {
    private const float Travel = 1.6f;
    private static readonly (float coupling, float maxOffset) Limits = (0.6f, 0.3f);

    private static SupportStance Stance((float footX, float direction, float progress) stance) {
        return new SupportStance((SupportStance.Leg.Front, true), (stance.footX, stance.direction, Travel, stance.progress));
    }

    // ---- Support leg ----------------------------------------------------------------------------------------------

    [Test]
    public void TheOnlyPlantedFootCarriesTheBody() {
        SupportLegResolver resolver = new SupportLegResolver();
        resolver.Track((true, false), (1f, 0f), (0f, 1f, Travel));
        Assert.That(resolver.Current.SupportLeg, Is.EqualTo(SupportStance.Leg.Front));
        Assert.That(resolver.Current.IsPlanted, Is.True);
    }

    [Test]
    public void WithBothFeetDownTheFootThatLandedLastCarriesTheBody() {
        SupportLegResolver resolver = new SupportLegResolver();
        resolver.Track((true, false), (1f, 0f), (0f, 1f, Travel));
        resolver.Track((true, true), (1f, 2.5f), (0.8f, 1f, Travel));
        Assert.That(resolver.Current.SupportLeg, Is.EqualTo(SupportStance.Leg.Back));
        Assert.That(resolver.Current.FootX, Is.EqualTo(2.5f));
    }

    // Double support does not hand the weight back and forth: it moves on only when a foot lands or the support lifts.
    [Test]
    public void TheSupportDoesNotSwitchWhileBothFeetStayDown() {
        SupportLegResolver resolver = new SupportLegResolver();
        resolver.Track((true, false), (1f, 0f), (0f, 1f, Travel));
        resolver.Track((true, true), (1f, 2.5f), (0.8f, 1f, Travel));
        for (int tick = 0; tick < 10; tick++) {
            resolver.Track((true, true), (1f, 2.5f), (0.8f + 0.05f * tick, 1f, Travel));
            Assert.That(resolver.Current.SupportLeg, Is.EqualTo(SupportStance.Leg.Back));
        }
        resolver.Track((true, false), (1f, 2.5f), (1.4f, 1f, Travel));
        Assert.That(resolver.Current.SupportLeg, Is.EqualTo(SupportStance.Leg.Front), "the back foot lifted, the front one still down");
    }

    [Test]
    public void InFlightTheLastSupportCarriesOn() {
        SupportLegResolver resolver = new SupportLegResolver();
        resolver.Track((true, false), (1f, 0f), (0f, 1f, Travel));
        resolver.Track((false, false), (1f, 0f), (1.7f, 1f, Travel));
        Assert.That(resolver.Current.SupportLeg, Is.EqualTo(SupportStance.Leg.Front));
        Assert.That(resolver.Current.IsPlanted, Is.False);
        Assert.That(resolver.Current.Progress, Is.GreaterThan(1f), "past toe-off");
    }

    [Test]
    public void StanceProgressIsTheBodysTravelOverTheFoot([Values(1f, -1f)] float direction) {
        SupportLegResolver resolver = new SupportLegResolver();
        resolver.Track((true, false), (direction * 0.8f, 0f), (0f, direction, Travel));
        resolver.Track((true, false), (direction * 0.8f, 0f), (direction * 0.8f, direction, Travel));
        Assert.That(resolver.Current.Progress, Is.EqualTo(0.5f).Within(0.0001f), "mid-stance after half the travel, either way");
    }

    // ---- Pelvis over the support --------------------------------------------------------------------------------

    [Test]
    public void ACentredStanceRunsFromBehindTheFootThroughOverItToAheadOfIt() {
        float foot = 5f;
        Assert.That(PelvisSupportMotion.CentredHipX(Stance((foot, 1f, 0f))), Is.EqualTo(foot - Travel / 2f), "contact: behind the foot");
        Assert.That(PelvisSupportMotion.CentredHipX(Stance((foot, 1f, 0.5f))), Is.EqualTo(foot), "mid-stance: over it");
        Assert.That(PelvisSupportMotion.CentredHipX(Stance((foot, 1f, 1f))), Is.EqualTo(foot + Travel / 2f), "toe-off: ahead of it");
    }

    [Test]
    public void AFootThatLandedWhereThePelvisWillCentreNeedsNoOffset([Values(0f, 0.3f, 0.5f, 0.8f)] float progress) {
        SupportStance stance = Stance((5f, 1f, progress));
        float pelvis = PelvisSupportMotion.CentredHipX(stance);
        Assert.That(PelvisSupportMotion.SupportOffset((stance, pelvis), Limits), Is.EqualTo(0f).Within(0.0001f));
    }

    // A foot that landed further ahead than planned pulls the pelvis forward over it, most at mid-stance.
    [Test]
    public void AFootAheadOfItsPlanDrawsThePelvisForwardMostAtMidStance() {
        float footAhead = 0.2f;
        float atMid = OffsetForFootAhead((footAhead, 0.5f));
        Assert.That(atMid, Is.EqualTo(Limits.coupling * footAhead).Within(0.0001f));
        Assert.That(OffsetForFootAhead((footAhead, 0.25f)), Is.GreaterThan(0f).And.LessThan(atMid));
        Assert.That(OffsetForFootAhead((footAhead, 0f)), Is.EqualTo(0f).Within(0.0001f), "nothing at contact");
        Assert.That(OffsetForFootAhead((footAhead, 1f)), Is.EqualTo(0f).Within(0.0001f), "nothing at toe-off");
    }

    [Test]
    public void TheOffsetIsMirroredForTheOtherDirectionAndBounded() {
        SupportStance right = Stance((5f, 1f, 0.5f));
        SupportStance left = Stance((-5f, -1f, 0.5f));
        float rightOffset = PelvisSupportMotion.SupportOffset((right, 4.8f), Limits);
        float leftOffset = PelvisSupportMotion.SupportOffset((left, -4.8f), Limits);
        Assert.That(leftOffset, Is.EqualTo(-rightOffset).Within(0.0001f));
        Assert.That(PelvisSupportMotion.SupportOffset((right, -50f), Limits), Is.EqualTo(Limits.maxOffset));
    }

    [Test]
    public void NothingCarriesThePelvisWithoutSupport() {
        Assert.That(PelvisSupportMotion.SupportOffset((SupportStance.Unsupported, 3f), Limits), Is.EqualTo(0f));
        Assert.That(PelvisSupportMotion.Surge(SupportStance.Unsupported, 0.1f), Is.EqualTo(0f));
        Assert.That(float.IsNaN(PelvisSupportMotion.CentredHipX(SupportStance.Unsupported)), Is.False);
    }

    // The surge is the body's speed over its support, slowest at mid-stance: a little ahead early in the stance, right
    // over the foot at mid-stance, a little behind late in it, and zero at contact and toe-off so the pelvis target is
    // continuous when the next foot takes over.
    [Test]
    public void TheSurgeLeadsEarlyTrailsLateAndIsZeroAtContactMidStanceAndToeOff([Values(1f, -1f)] float direction) {
        const float amplitude = 0.1f;
        Assert.That(PelvisSupportMotion.Surge(Stance((0f, direction, 0f)), amplitude), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(PelvisSupportMotion.Surge(Stance((0f, direction, 0.25f)), amplitude), Is.EqualTo(direction * amplitude).Within(0.0001f));
        Assert.That(PelvisSupportMotion.Surge(Stance((0f, direction, 0.5f)), amplitude), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(PelvisSupportMotion.Surge(Stance((0f, direction, 0.75f)), amplitude), Is.EqualTo(-direction * amplitude).Within(0.0001f));
        Assert.That(PelvisSupportMotion.Surge(Stance((0f, direction, 0.9999f)), amplitude), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ThePelvisTargetIsContinuousAcrossTheSupportChange() {
        // The old support ends its stance as the new one begins: both shifts are zero there.
        SupportStance ending = Stance((5f, 1f, 0.999f));
        SupportStance starting = Stance((6.6f, 1f, 0f));
        float before = PelvisSupportMotion.SupportOffset((ending, 5.85f), Limits) + PelvisSupportMotion.Surge(ending, 0.1f);
        float after = PelvisSupportMotion.SupportOffset((starting, 5.85f), Limits) + PelvisSupportMotion.Surge(starting, 0.1f);
        Assert.That(after - before, Is.EqualTo(0f).Within(0.01f));
    }

    private static float OffsetForFootAhead((float footAhead, float progress) plan) {
        float foot = 5f;
        SupportStance stance = Stance((foot, 1f, plan.progress));
        // The pelvis rides where it would over a foot at foot - footAhead.
        float pelvis = PelvisSupportMotion.CentredHipX(Stance((foot - plan.footAhead, 1f, plan.progress)));
        return PelvisSupportMotion.SupportOffset((stance, pelvis), Limits);
    }
}
