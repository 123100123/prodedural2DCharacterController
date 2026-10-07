using NUnit.Framework;
using UnityEngine;

// The legs' reach on a slope (SlopeReach) against the inverseRobo rig: on flat ground it is the existing flat geometry and
// reproduces the flat stance; uphill the trailing foot and downhill the landing foot reach less, and the gait's stance is
// bounded so that a push-off on a 45° slope needs no more than the pelvis's reach reserve. The release on extension
// (SupportRecovery.ReleasesOnExtension) fires only for the trailing foot next to step, past the threshold, with the other
// foot carrying the body, and never in a steady flat walk or run.
public class SlopeReachTests {
    // Measured on the rig (StrideReachTests): thigh plus shin, and the hips' depth below the body pivot.
    private const float LegLength = 2.337f;
    private const float HipBelowPivot = 0.27f;
    private const float Tolerance = 0.03f;
    private static readonly BodyMassSettings Body = BodyMassSettings.Default;
    private static readonly SlopeSettings Slope = new SlopeSettings();
    private static float Reach => LegLength * Body.legExtension;

    // The rig's legs standing at the default height, uncrouched, with the default reach reserve and crouch depth.
    public static LegReach RigLegs => new LegReach((StrideController.DefaultBodyHeight - HipBelowPivot, LegLength * BodyMassSettings.Default.legExtension),
        (BodyMassSettings.Default.maxReachDrop, CrouchSettings.Default.depth));

    private static float Gradient(float degrees) => Mathf.Tan(degrees * Mathf.Deg2Rad);

    [Test]
    public void OnFlatGroundTheReachIsTheFlatGeometry() {
        foreach (float hipHeight in new[] { 1.6f, 1.86f, 2.0f }) {
            Assert.That(SlopeReach.MaxLag(hipHeight, 0f, Reach), Is.EqualTo(Mathf.Sqrt(Reach * Reach - hipHeight * hipHeight)).Within(1e-4f));
            Assert.That(SlopeReach.MaxLead(hipHeight, 0f, Reach), Is.EqualTo(SlopeReach.MaxLag(hipHeight, 0f, Reach)));
            // The pelvis ceiling over a foot at that distance (GaitPose) is exactly the hip height.
            float pelvis = GaitPose.HighestPelvisY((Vector2.zero, -SlopeReach.MaxLag(hipHeight, 0f, Reach)), (new Vector2(0f, -HipBelowPivot), Reach));
            Assert.That(pelvis - HipBelowPivot, Is.EqualTo(hipHeight).Within(1e-3f));
        }
    }

    // While the foot at the limit is below the hip (a foot above the hip is no stance): from 30° up the other way to 50°
    // down, the steepest walkable slope.
    [Test]
    public void TheReachShrinksAsTheFootSideGetsLower() {
        float previous = float.PositiveInfinity;
        for (float degrees = -30f; degrees <= 50f; degrees += 5f) {
            float lag = SlopeReach.MaxLag(1.86f, Gradient(degrees), Reach);
            Assert.That(lag, Is.LessThan(previous), $"at {degrees}°");
            previous = lag;
        }
        Assert.That(SlopeReach.MaxLag(5f, 0f, Reach), Is.Zero, "a hip higher than the reach reaches nothing");
    }

    // Going uphill the trailing foot is lower and the landing foot higher; going downhill the other way round. Whichever
    // side is lower by the same gradient reaches the same distance, and a foot at that distance is exactly at reach.
    [Test]
    public void UphillTheTrailingFootAndDownhillTheLandingFootReachLess([Values(15f, 30f, 45f)] float degrees, [Values(1f, -1f)] float direction) {
        const float HipHeight = 1.86f;
        float flat = SlopeReach.MaxLag(HipHeight, 0f, Reach);
        float slope = Gradient(degrees);
        (float trailing, float leading) uphill = SlopeReach.SideGradients(slope * direction, direction);
        (float trailing, float leading) downhill = SlopeReach.SideGradients(-slope * direction, direction);
        Assert.That(uphill.trailing, Is.GreaterThan(0f), "uphill the trailing foot is lower");
        Assert.That(downhill.leading, Is.GreaterThan(0f), "downhill the landing foot is lower");

        Assert.That(SlopeReach.MaxLag(HipHeight, uphill.trailing, Reach), Is.LessThan(flat));
        Assert.That(SlopeReach.MaxLead(HipHeight, uphill.leading, Reach), Is.GreaterThan(flat));
        Assert.That(SlopeReach.MaxLead(HipHeight, downhill.leading, Reach), Is.LessThan(flat));
        Assert.That(SlopeReach.MaxLag(HipHeight, downhill.trailing, Reach), Is.GreaterThan(flat));
        Assert.That(SlopeReach.MaxLead(HipHeight, downhill.leading, Reach), Is.EqualTo(SlopeReach.MaxLag(HipHeight, uphill.trailing, Reach)).Within(1e-5f), "the same geometry either side");

        // The foot at the reach limit stands on the slope line through the ground under the hip, exactly a reach away.
        float lag = SlopeReach.MaxLag(HipHeight, uphill.trailing, Reach);
        Vector2 hip = new Vector2(0f, HipHeight);
        Vector2 foot = new Vector2(-lag, -uphill.trailing * lag);
        Assert.That(Vector2.Distance(hip, foot), Is.EqualTo(Reach).Within(1e-4f));
        // Along the surface that is the horizontal reach over the slope's cosine.
        Assert.That(foot.magnitude, Is.EqualTo(lag / Mathf.Cos(degrees * Mathf.Deg2Rad)).Within(1e-4f));
        Assert.That(SlopeReach.Sink((lag, uphill.trailing * lag), (HipHeight, Reach)), Is.EqualTo(0f).Within(1e-4f), "no sink at the limit");
        float further = lag + 0.2f;
        float sink = SlopeReach.Sink((further, uphill.trailing * further), (HipHeight, Reach));
        Assert.That(sink, Is.GreaterThan(0f), "further back the hip must sink");
        Assert.That(Vector2.Distance(new Vector2(0f, HipHeight - sink), new Vector2(-further, -uphill.trailing * further)), Is.EqualTo(Reach).Within(1e-4f), "just far enough");
    }

    // On flat ground the reach behind the hip at push-off (with the pelvis's reach reserve) is the flat gait's own push-off:
    // the flat run is designed at that limit, within 3%, and the fitted stance leaves the flat steps as they are.
    [Test]
    public void OnFlatGroundTheBoundReproducesTheFlatStance([Values(false, true)] bool running) {
        GaitProfile gait = running ? GaitProfile.Run : GaitProfile.Walk;
        (float pushOff, float contact) heights = SlopeReach.StanceHipHeights(gait, RigLegs);
        float maxLag = SlopeReach.MaxLag(heights.pushOff, 0f, Reach);
        float toeOffLag = GaitTiming.ToeOffLag(gait.speed, gait);
        if (running) {
            Assert.That(maxLag, Is.EqualTo(toeOffLag).Within(Tolerance * toeOffLag), "the flat run pushes off at the reach limit");
        }
        Assert.That(maxLag, Is.GreaterThanOrEqualTo(toeOffLag), "the flat push-off is within reach");
        Assert.That(SlopeReach.MaxLead(heights.contact, 0f, Reach), Is.GreaterThan(GaitTiming.ContactLead(gait.speed, gait)));

        SlopeStance stance = SlopeReach.Fit((gait, gait.speed), 0f, RigLegs);
        Assert.That(stance.Step, Is.EqualTo(GaitTiming.StepLength(gait.speed, gait)), "flat steps are kept");
        Assert.That(stance.Drop, Is.Zero, "the flat pelvis rides at its own height");
        Assert.That(stance.IsBounded, Is.False);
    }

    // At 45°, up or down, walking or running, the fitted stance pushes off and lands within reach of the pelvis at its gait
    // height (the push-off with the reach reserve): the trailing leg's extension stays within the release extension.
    [Test]
    public void At45DegreesTheBoundKeepsTheLegsWithinTheReleaseExtension([Values(false, true)] bool running, [Values(1f, -1f)] float uphill) {
        GaitProfile flatGait = running ? GaitProfile.Run : GaitProfile.Walk;
        float gradient = uphill * Gradient(45f);
        GaitSet gaits = new GaitSet((GaitProfile.Walk, GaitProfile.Run, GaitProfile.Crouch), GaitTransitionSettings.Default, 0f);
        SlopeGait slope = new SlopeGait(gradient, Slope).Reaching(RigLegs);
        float speed = flatGait.speed * slope.SpeedScale(1f);
        GaitProfile gait = gaits.OnSlope(slope).At(speed, running ? 1f : 0f);
        SlopeStance stance = gaits.OnSlope(slope).StanceAt(speed, running ? 1f : 0f);
        float toeOffLag = GaitTiming.ToeOffLag(speed, gait);
        float contactLead = GaitTiming.ContactLead(speed, gait);
        Assert.That(toeOffLag, Is.LessThanOrEqualTo(stance.MaxLag + 1e-4f));
        Assert.That(contactLead, Is.LessThanOrEqualTo(stance.MaxLead + 1e-4f));
        Assert.That(stance.Drop, Is.InRange(0f, CrouchSettings.Default.depth));

        (float pushOff, float contact) heights = SlopeReach.StanceHipHeights(gait, RigLegs);
        (float trailing, float leading) sides = SlopeReach.SideGradients(gradient, 1f);
        float pushOffHip = heights.pushOff - stance.Drop;
        float trailingExtension = Mathf.Sqrt(toeOffLag * toeOffLag + Mathf.Pow(pushOffHip + sides.trailing * toeOffLag, 2f)) / LegLength;
        float contactHip = heights.contact - stance.Drop;
        float landingExtension = Mathf.Sqrt(contactLead * contactLead + Mathf.Pow(contactHip + sides.leading * contactLead, 2f)) / LegLength;
        Assert.That(trailingExtension, Is.LessThanOrEqualTo(Slope.releaseExtension), "the push-off leg");
        Assert.That(trailingExtension, Is.LessThanOrEqualTo(Body.legExtension + 1e-3f), "within the reach the pelvis keeps");
        Assert.That(landingExtension, Is.LessThanOrEqualTo(Body.legExtension + 1e-3f), "the landing leg");
        Assert.That(GaitTiming.StepLength(speed, gait), Is.LessThan(GaitTiming.StepLength(speed, flatGait)), "shorter steps than on flat ground");
    }

    // Steeper ground shortens the steps more and quickens the cadence; the walk down a 45° slope keeps a step its swing can
    // be timed for and rides the pelvis lower to reach it instead.
    [Test]
    public void SteeperGroundShortensTheStepsAndLowersThePelvisOnlyWhenTheSwingNeedsIt() {
        GaitProfile run = GaitProfile.Run;
        float previous = float.PositiveInfinity;
        foreach (float degrees in new[] { 0f, 15f, 30f, 45f }) {
            SlopeStance stance = SlopeReach.Fit((run, 5f), Gradient(degrees), RigLegs);
            Assert.That(stance.Step, Is.LessThanOrEqualTo(previous), $"at {degrees}°");
            previous = stance.Step;
        }

        GaitProfile walk = GaitProfile.Walk;
        float downhillSpeed = walk.speed * new SlopeGait(-1f, Slope).SpeedScale(1f);
        SlopeStance steepDown = SlopeReach.Fit((walk, downhillSpeed), -1f, RigLegs);
        float shortestTimed = downhillSpeed * walk.minSwingDuration / (2f * (1f - walk.dutyFactor));
        Assert.That(steepDown.Drop, Is.GreaterThan(0f), "the pelvis rides lower");
        Assert.That(steepDown.Step, Is.EqualTo(shortestTimed).Within(1e-3f), "the shortest step the swing allows");
        Assert.That(SlopeReach.StepBound((steepDown.MaxLag, steepDown.MaxLead), (walk.dutyFactor, walk.contactLeadShare)), Is.GreaterThanOrEqualTo(steepDown.Step - 1e-3f), "reached at that drop");
        Assert.That(SlopeReach.Fit((walk, 1.86f), 1f, RigLegs).Drop, Is.Zero, "the walk up keeps the pelvis height");
    }

    [Test]
    public void TheReleaseFiresOnlyForTheTrailingFootNextToStepPastTheThresholdWithTheOtherFootDown() {
        const float Threshold = 1f;
        Assert.That(SupportRecovery.ReleasesOnExtension((1.01f, Threshold), (true, true, true)), Is.True);
        Assert.That(SupportRecovery.ReleasesOnExtension((1f, Threshold), (true, true, true)), Is.True, "at the threshold");
        Assert.That(SupportRecovery.ReleasesOnExtension((0.99f, Threshold), (true, true, true)), Is.False, "below it");
        Assert.That(SupportRecovery.ReleasesOnExtension((1.2f, Threshold), (true, true, false)), Is.False, "the other foot is not down nor far enough through its swing");
        Assert.That(SupportRecovery.ReleasesOnExtension((1.2f, Threshold), (false, true, true)), Is.False, "a foot ahead of the hip");
        Assert.That(SupportRecovery.ReleasesOnExtension((1.2f, Threshold), (true, false, true)), Is.False, "the other leg steps next");
    }

    // The flat gait's planned push-off, with the pelvis at its gait height less the reach reserve (how the gait measures
    // the release), stays below the release extension, and the foot would have to trail at least half a frame of full-speed
    // travel (60 fps) further before it got there: a steady flat walk or run never releases a foot early. (The flat
    // regression scenario counts the releases of the real gait.)
    [Test]
    public void TheReleaseNeverFiresInASteadyFlatGait([Values(false, true)] bool running) {
        GaitProfile gait = running ? GaitProfile.Run : GaitProfile.Walk;
        float lag = GaitTiming.ToeOffLag(gait.speed, gait);
        float hip = SlopeReach.StanceHipHeights(gait, RigLegs).pushOff;
        float extension = Mathf.Sqrt(lag * lag + hip * hip) / LegLength;
        float releaseLength = Slope.releaseExtension * LegLength;
        float lagAtRelease = Mathf.Sqrt(releaseLength * releaseLength - hip * hip);
        Assert.That(extension, Is.LessThan(Slope.releaseExtension));
        Assert.That(lagAtRelease - lag, Is.GreaterThan(0.5f * gait.speed / 60f), "a margin of half a frame of travel");
        Assert.That(SupportRecovery.ReleasesOnExtension((extension, Slope.releaseExtension), (true, true, true)), Is.False);
    }

    // A leg released ahead of its cue answers that cue: when the clock reaches its lift phase it is not cued again, and the
    // other leg is still cued half a cycle later.
    [Test]
    public void AStepAheadOfTheCueUsesTheCueUp() {
        StepScheduler scheduler = new StepScheduler();
        GaitTransitionSettings transitions = GaitTransitionSettings.Default;
        const float Tick = 1f / 60f;
        // Start moving; the trailing (back) leg is primed to step first.
        scheduler.Advance((3f, 3f, 0.4f, Tick, false), transitions);
        for (int frame = 0; frame < 600 && !scheduler.IsCued(false); frame++) {
            scheduler.Advance((3f, 3f, 0.4f, Tick, false), transitions);
        }
        Assert.That(scheduler.IsCued(false), Is.True, "the first cue");
        scheduler.ConsumeCue(false);
        scheduler.RecordLift(false);

        // Before the front leg's cue comes, it steps ahead of it.
        Assert.That(scheduler.IsNextToStep(true), Is.True);
        scheduler.StepAheadOfCue(true);
        scheduler.RecordLift(true);
        Assert.That(scheduler.IsNextToStep(true), Is.False, "its cue is already answered");
        bool frontCued = false;
        bool backCued = false;
        for (int frame = 0; frame < 120 && !backCued; frame++) {
            scheduler.Advance((3f, 3f, 0.4f, Tick, false), transitions);
            frontCued |= scheduler.IsCued(true);
            backCued = scheduler.IsCued(false);
        }
        Assert.That(frontCued, Is.False, "the front cue was used up by the early step");
        Assert.That(backCued, Is.True, "the back leg is cued on its own phase");
    }
}
