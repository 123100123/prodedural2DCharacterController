using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GaitCycleTests {
    private static readonly GaitProfile Walk = GaitProfile.Walk;

    private static List<(float time, bool front)> SimulateLifts((float speed, float seconds) walk) {
        GaitCycle cycle = new GaitCycle();
        cycle.PrimeToLift(true);
        List<(float time, bool front)> lifts = new List<(float time, bool front)>();
        const float frame = 1f / 120f;
        for (float time = 0f; time < walk.seconds; time += frame) {
            float strides = walk.speed * frame / GaitTiming.StrideLength(walk.speed, Walk);
            (bool front, bool back) cues = cycle.Advance(strides);
            if (cues.front) {
                lifts.Add((time, true));
            }
            if (cues.back) {
                lifts.Add((time, false));
            }
        }
        return lifts;
    }

    [Test]
    public void LegsAlternateHalfACycleApart() {
        List<(float time, bool front)> lifts = SimulateLifts((Walk.speed, 5f));
        float halfPeriod = GaitTiming.CyclePeriod(Walk.speed, Walk) / 2f;

        Assert.That(lifts.Count, Is.GreaterThan(6));
        for (int index = 1; index < lifts.Count; index++) {
            Assert.That(lifts[index].front, Is.Not.EqualTo(lifts[index - 1].front), "legs take turns");
            Assert.That(lifts[index].time - lifts[index - 1].time, Is.EqualTo(halfPeriod).Within(0.02f), "evenly spaced steps");
        }
    }

    [Test]
    public void PrimedLegLiftsFirst() {
        GaitCycle cycle = new GaitCycle();
        cycle.PrimeToLift(false);
        (bool front, bool back) cues = cycle.Advance(0.01f);
        Assert.IsTrue(cues.back);
        Assert.IsFalse(cues.front);
    }

    [Test]
    public void FootprintsAreEvenlySpacedAtSteadySpeed() {
        // Each foot lands where the hip will be at touchdown plus the landing lead.
        float speed = Walk.speed;
        float swing = GaitTiming.SwingDuration(speed, Walk);
        float lead = GaitTiming.ContactLead(speed, Walk);
        List<float> footprints = new List<float>();
        foreach ((float time, bool _) lift in SimulateLifts((speed, 5f))) {
            footprints.Add(speed * (lift.time + swing) + lead);
        }

        float expected = GaitTiming.StepLength(speed, Walk);
        for (int index = 1; index < footprints.Count; index++) {
            Assert.That(footprints[index] - footprints[index - 1], Is.EqualTo(expected).Within(0.05f));
        }
    }

    [Test]
    public void TheStanceSplitsIntoTheContactLeadAheadAndTheToeOffLagBehind() {
        GaitProfile run = GaitProfile.Run;
        float travel = GaitTiming.StanceTravel(run.speed, run);
        Assert.That(GaitTiming.ContactLead(run.speed, run) + GaitTiming.ToeOffLag(run.speed, run), Is.EqualTo(travel).Within(0.0001f));
        Assert.That(GaitTiming.ContactLead(run.speed, run), Is.LessThan(GaitTiming.ToeOffLag(run.speed, run)), "a runner lands closer than it pushes off");
        Assert.That(GaitTiming.ContactLead(Walk.speed, Walk), Is.EqualTo(GaitTiming.ToeOffLag(Walk.speed, Walk)).Within(0.0001f), "a walker centres its stance");
    }

    [Test]
    public void StepLandsWhereTheHipWillBeAtTouchdownPlusTheLead() {
        GaitProfile run = GaitProfile.Run;
        float swing = GaitTiming.SwingDuration(run.speed, run);
        float reach = GaitTiming.LandingReach(run.speed, run);
        Assert.That(reach, Is.EqualTo(run.speed * swing + GaitTiming.ContactLead(run.speed, run)).Within(0.0001f));
        Assert.That(GaitTiming.LandingReach(-run.speed, run), Is.EqualTo(reach).Within(0.0001f), "the same either way");
    }

    [Test]
    public void StepsLengthenWithTheSquareRootOfSpeedWithinLimits() {
        float slow = GaitTiming.StepLength(1f, Walk);
        float fast = GaitTiming.StepLength(4f, Walk);
        Assert.That(fast / slow, Is.EqualTo(2f).Within(0.01f), "four times the speed, twice the step");
        Assert.That(GaitTiming.StepLength(0f, Walk), Is.EqualTo(Walk.minStepLength));
        Assert.That(GaitTiming.StepLength(1000f, Walk), Is.EqualTo(Walk.maxStepLength));
    }

    [Test]
    public void FullSpeedTimingIsHumanLike() {
        float walkCadence = Walk.speed / GaitTiming.StepLength(Walk.speed, Walk);
        float runCadence = GaitProfile.Run.speed / GaitTiming.StepLength(GaitProfile.Run.speed, GaitProfile.Run);
        Assert.That(walkCadence, Is.InRange(1.7f, 2.3f), "about two steps per second walking");
        Assert.That(runCadence, Is.InRange(2.5f, 3.8f), "three to four quick steps per second running, the feet kept under the body");
        Assert.That(GaitTiming.SwingDuration(Walk.speed, Walk), Is.InRange(0.3f, 0.45f));
    }

    [Test]
    public void WalkingBodyIsLowestJustAfterContactAndRunningBodyAtMidStance() {
        float walkLow = GaitWaves.LowestPhase((Walk.dutyFactor, Walk.lowAfterContact));
        float contact = GaitWaves.FrontLegContact(Walk.dutyFactor);
        Assert.That(walkLow - contact, Is.InRange(0f, 0.1f));
        Assert.That(GaitWaves.BodyBob((walkLow, walkLow)), Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(GaitWaves.BodyBob((walkLow + 0.25f, walkLow)), Is.EqualTo(1f).Within(0.0001f), "highest a quarter cycle later, over the support leg");

        // Running, the body is lowest as the hip passes over the foot, a contact-lead share into the stance.
        GaitProfile run = GaitProfile.Run;
        float runLow = GaitWaves.LowestPhase((run.dutyFactor, run.lowAfterContact));
        float hipOverFoot = GaitWaves.FrontLegContact(run.dutyFactor) + run.dutyFactor * run.contactLeadShare;
        Assert.That(runLow, Is.EqualTo(Mathf.Repeat(hipOverFoot, 1f)).Within(0.01f));
    }

    [Test]
    public void TorsoFlexesAtTheLowPointAndArmsPeakAfterTheOppositeLegLands() {
        float low = GaitWaves.LowestPhase((Walk.dutyFactor, Walk.lowAfterContact));
        Assert.That(GaitWaves.TorsoPitch((low, low)), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(GaitWaves.TorsoPitch((low + 0.25f, low)), Is.EqualTo(-1f).Within(0.0001f));

        float contact = GaitWaves.FrontLegContact(Walk.dutyFactor);
        Assert.That(GaitWaves.OppositeArmSwing((contact + 0.06f, contact, 0.06f)), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(GaitWaves.OppositeArmSwing((contact + 0.56f, contact, 0.06f)), Is.EqualTo(-1f).Within(0.0001f));
    }
}
