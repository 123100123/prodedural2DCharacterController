using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Transition invariants of the gait timing: whatever point of the stride the walk/run change starts at, the legs keep
// alternating, each leg lifts on its own cue and opposite contacts stay evenly spaced.
public class StepSchedulerTests {
    private static readonly (float direction, bool running) Walking = (1f, false);
    private static readonly (float direction, bool running) Running = (1f, true);
    private static readonly (float direction, bool running) Standing = (0f, false);
    // A lift happens on the tick its cue comes, so its phase is within one tick's advance of the cue.
    private const float CuePhaseTolerance = 0.03f;
    // How far a contact interval may stray from the mean of its neighbours. A walk/run change also changes the cadence
    // within a step or two, which bends the intervals a little; a limp alternates them by half (0.5 and more).
    private const float EvenContacts = 0.2f;
    // Toggling the run every step or so changes the speed between consecutive steps, so their durations genuinely differ.
    private const float EvenContactsUnderRapidToggles = 0.35f;

    [Test]
    public void WalkToRunFromAnyPointOfEitherSwingKeepsTheGaitEven(
        [Values(true, false)] bool frontSwinging, [Values(0.1f, 0.5f, 0.9f)] float swingProgress) {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Walking, 3f);
        gait.HoldUntilSwinging(Walking, (frontSwinging, swingProgress));
        float change = gait.Time;
        gait.Hold(Running, 3f);

        AssertEvenGait((gait, change - 1f), EvenContacts);
    }

    [Test]
    public void RunToWalkFromAnyPointOfEitherSwingKeepsTheGaitEven(
        [Values(true, false)] bool frontSwinging, [Values(0.1f, 0.5f, 0.9f)] float swingProgress) {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Running, 3f);
        gait.HoldUntilSwinging(Running, (frontSwinging, swingProgress));
        float change = gait.Time;
        gait.Hold(Walking, 3f);

        AssertEvenGait((gait, change - 1f), EvenContacts);
    }

    // Toggles slower than a stride. Toggling exactly at the stride rate locks the speed changes to the legs: one leg
    // always swings while the body speeds up and the other while it brakes, so their steps genuinely differ.
    [Test]
    public void RepeatedRunTogglesKeepTheGaitEven() {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Walking, 3f);
        for (int toggle = 0; toggle < 10; toggle++) {
            gait.Hold(toggle % 2 == 0 ? Running : Walking, 0.6f);
        }
        gait.Hold(Walking, 2f);

        AssertEvenGait((gait, 2f), EvenContactsUnderRapidToggles);
    }

    [Test]
    public void StartingFromStandingLiftsTheTrailingFootFirstThenAlternates() {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Standing, 0.5f);
        gait.Hold(Walking, 5f);

        Assert.That(gait.Lifts[0].Front, Is.True, "the trailing leg (the front one here) steps first");
        // The first steps speed up the cadence from rest; the rhythm must be even once under way.
        AssertEvenGait((gait, gait.Plants[2].Time), EvenContacts);
    }

    [Test]
    public void ABriefStopContinuesTheRhythmWithTheOtherLeg() {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Walking, 2f);
        gait.Hold(Standing, 0.3f);
        int liftsBeforeRestart = gait.Lifts.Count;
        gait.Hold(Walking, 1.5f);

        Assert.That(gait.Lifts.Count, Is.GreaterThan(liftsBeforeRestart));
        Assert.That(gait.Lifts[liftsBeforeRestart].Front, Is.Not.EqualTo(gait.Lifts[liftsBeforeRestart - 1].Front), "no leg steps twice in a row");
    }

    [Test]
    public void StoppingFinishesEverySwingWithinItsLongestDuration() {
        GaitTimingSimulation gait = new GaitTimingSimulation();
        gait.Hold(Running, 3f);
        gait.HoldUntilSwinging(Running, (true, 0.2f));
        gait.Hold(Standing, GaitProfile.Run.maxSwingDuration + 0.6f);

        Assert.That(gait.IsFrontSwinging, Is.False);
    }

    [Test]
    public void SwingsOnTheClockLastTheirShareOfTheCycleAtSteadySpeed() {
        GaitProfile walk = GaitProfile.Walk;
        float cycleShare = SwingTiming.CycleShareAt(walk.speed, walk);
        float strideLength = GaitTiming.StrideLength(walk.speed, walk);
        SwingProgress swing = new SwingProgress();
        swing.Begin(SwingTiming.OnClock((walk.maxSwingDuration, 0.8f)));
        float seconds = 0f;
        float clockProgress = walk.speed * GaitTimingSimulation.Tick / strideLength / cycleShare;
        while (!swing.Advance((clockProgress, GaitTimingSimulation.Tick))) {
            seconds += GaitTimingSimulation.Tick;
        }
        Assert.That(seconds, Is.EqualTo(GaitTiming.SwingDuration(walk.speed, walk)).Within(2f * GaitTimingSimulation.Tick));
    }

    private static void AssertEvenGait((GaitTimingSimulation gait, float from) check, float evenContacts) {
        List<GaitTimingSimulation.FootEvent> lifts = check.gait.Lifts.FindAll(lift => lift.Time >= check.from);
        Assert.That(lifts.Count, Is.GreaterThan(4));
        for (int index = 1; index < lifts.Count; index++) {
            Assert.That(lifts[index].Front, Is.Not.EqualTo(lifts[index - 1].Front), $"legs alternate (lift at {lifts[index].Time:F3})");
        }
        foreach (GaitTimingSimulation.FootEvent lift in lifts) {
            float cuePhase = lift.Front ? GaitCycle.FrontLegLiftPhase : GaitCycle.BackLegLiftPhase;
            float lateness = Mathf.Repeat(lift.Phase - cuePhase, 1f);
            Assert.That(lateness, Is.LessThan(CuePhaseTolerance), $"lifts on its cue (lift at {lift.Time:F3}, phase {lift.Phase:F3})");
        }
        AssertEvenContacts(check, evenContacts);
    }

    // A limp makes opposite contact intervals alternate short and long; a cadence change only drifts them. So each
    // interval must match the mean of its neighbours, within a share of it.
    private static void AssertEvenContacts((GaitTimingSimulation gait, float from) check, float evenContacts) {
        List<GaitTimingSimulation.FootEvent> plants = check.gait.Plants.FindAll(plant => plant.Time >= check.from);
        for (int index = 3; index < plants.Count; index++) {
            float before = plants[index - 2].Time - plants[index - 3].Time;
            float current = plants[index - 1].Time - plants[index - 2].Time;
            float after = plants[index].Time - plants[index - 1].Time;
            float neighbours = (before + after) / 2f;
            float limp = Mathf.Abs(current - neighbours) / neighbours;
            Assert.That(limp, Is.LessThan(evenContacts), $"even contacts at {plants[index - 1].Time:F3}: {before:F3}, {current:F3}, {after:F3}");
        }
    }
}
