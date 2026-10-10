using System.Collections.Generic;
using NUnit.Framework;

// The draw and holster cycle on its own (plan §12-13): Holstered -> Drawing -> Equipped -> Holstering -> Holstered at the
// set times, the grip (attach) at the grip share and the insert (detach) at the insert share, and every interruption: a
// fire press buffered once and dropped with the aim, release before and after the grip, aim again before and after the
// insert, the quick holster when the state takes the arms and the redraw after it, and the support hand's weight.
public class DrawCycleTests {
    private const float Frame = 1f / 60f;
    private const float Tolerance = 1e-4f;

    private static PistolHandlingSettings Settings => new PistolHandlingSettings();

    private static (bool aim, bool fire, bool allowsActions) Aim => (true, false, true);
    private static (bool aim, bool fire, bool allowsActions) Release => (false, false, true);
    private static (bool aim, bool fire, bool allowsActions) Fire => (true, true, true);
    private static (bool aim, bool fire, bool allowsActions) AimInARoll => (true, false, false);

    // Ticks until the condition holds (or a limit), returning every step taken.
    private static List<DrawStep> TickUntil(DrawCycle cycle, (bool aim, bool fire, bool allowsActions) intent, System.Func<bool> done, int limit = 600) {
        List<DrawStep> steps = new List<DrawStep>();
        for (int frame = 0; frame < limit && !done(); frame++) {
            steps.Add(cycle.Tick(intent, Frame));
        }
        return steps;
    }

    private static List<DrawStep> TickFrames(DrawCycle cycle, (bool aim, bool fire, bool allowsActions) intent, int frames) {
        List<DrawStep> steps = new List<DrawStep>();
        for (int frame = 0; frame < frames; frame++) {
            steps.Add(cycle.Tick(intent, Frame));
        }
        return steps;
    }

    private static int Count(List<DrawStep> steps, System.Func<DrawStep, bool> which) {
        int count = 0;
        foreach (DrawStep step in steps) {
            count += which(step) ? 1 : 0;
        }
        return count;
    }

    private static DrawCycle Equipped(PistolHandlingSettings settings) {
        DrawCycle cycle = new DrawCycle(settings);
        TickUntil(cycle, Aim, () => cycle.State == DrawState.Equipped);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
        return cycle;
    }

    // ---- The progression -------------------------------------------------------------------------------------------

    [Test]
    public void ItStartsHolsteredAndGoesThroughTheWholeCycleAtTheSetTimes() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = new DrawCycle(settings);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(cycle.AimShare, Is.EqualTo(0f));
        Assert.That(cycle.ShouldBeInHand, Is.False);
        Assert.That(cycle.CanFire, Is.False);

        cycle.Tick(Aim, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(cycle.Progress, Is.EqualTo(Frame / settings.drawDuration).Within(Tolerance));
        int drawFrames = 1 + TickUntil(cycle, Aim, () => cycle.State != DrawState.Drawing).Count;
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
        Assert.That(drawFrames * Frame, Is.EqualTo(settings.drawDuration).Within(1.5f * Frame), "the draw time");
        Assert.That(cycle.AimShare, Is.EqualTo(1f));
        Assert.That(cycle.CanFire, Is.True);

        TickFrames(cycle, Aim, 30);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped), "stays at the aim while aim is held");

        cycle.Tick(Release, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstering));
        Assert.That(cycle.CanFire, Is.False);
        int holsterFrames = 1 + TickUntil(cycle, Release, () => cycle.State != DrawState.Holstering).Count;
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(holsterFrames * Frame, Is.EqualTo(settings.holsterDuration).Within(1.5f * Frame), "the holster time");
        Assert.That(cycle.AimShare, Is.EqualTo(0f));
    }

    [Test]
    public void TheAimShareRisesThroughTheDrawAndFallsThroughTheHolsterWithoutAStep() {
        DrawCycle cycle = new DrawCycle(Settings);
        float last = 0f;
        float biggestStep = 0f;
        while (cycle.State != DrawState.Equipped) {
            cycle.Tick(Aim, Frame);
            Assert.That(cycle.AimShare, Is.GreaterThanOrEqualTo(last), "rising");
            biggestStep = UnityEngine.Mathf.Max(biggestStep, cycle.AimShare - last);
            last = cycle.AimShare;
        }
        while (cycle.State != DrawState.Holstered) {
            cycle.Tick(Release, Frame);
            Assert.That(cycle.AimShare, Is.LessThanOrEqualTo(last), "falling");
            biggestStep = UnityEngine.Mathf.Max(biggestStep, last - cycle.AimShare);
            last = cycle.AimShare;
        }
        Assert.That(biggestStep, Is.LessThan(0.1f));
    }

    [Test]
    public void ThePistolIsGrippedOnceAtTheGripShareAndInsertedOnceAtTheInsertShare() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = new DrawCycle(settings);
        float progressBefore = 0f;
        int attaches = 0;
        while (cycle.State != DrawState.Equipped) {
            progressBefore = cycle.Progress;
            DrawStep step = cycle.Tick(Aim, Frame);
            Assert.That(step.Detached, Is.False);
            if (step.Attached) {
                attaches++;
                Assert.That(cycle.Progress, Is.GreaterThanOrEqualTo(settings.gripShare), "gripped at the grip share");
                Assert.That(progressBefore, Is.LessThan(settings.gripShare), "and not before");
            }
            Assert.That(cycle.ShouldBeInHand, Is.EqualTo(cycle.State == DrawState.Equipped || cycle.Progress >= settings.gripShare));
        }
        Assert.That(attaches, Is.EqualTo(1));

        int detaches = 0;
        while (cycle.State != DrawState.Holstered) {
            progressBefore = cycle.State == DrawState.Holstering ? cycle.Progress : 0f;
            DrawStep step = cycle.Tick(Release, Frame);
            Assert.That(step.Attached, Is.False);
            if (step.Detached) {
                detaches++;
                Assert.That(cycle.Progress, Is.GreaterThanOrEqualTo(settings.insertShare), "inserted at the insert share");
                Assert.That(progressBefore, Is.LessThan(settings.insertShare), "and not before");
            }
        }
        Assert.That(detaches, Is.EqualTo(1));
        Assert.That(cycle.ShouldBeInHand, Is.False);
    }

    // ---- Fire --------------------------------------------------------------------------------------------------------

    [Test]
    public void APressDuringTheDrawIsBufferedAndFiresOnceTheFrameThePistolArrives() {
        DrawCycle cycle = new DrawCycle(Settings);
        DrawStep first = cycle.Tick(Fire, Frame);
        Assert.That(first.FireNow, Is.False, "not before the pistol is at the aim");
        Assert.That(cycle.FireBuffered, Is.True);
        TickFrames(cycle, Aim, 3);
        // More presses do not stack.
        cycle.Tick(Fire, Frame);
        cycle.Tick(Aim, Frame);
        cycle.Tick(Fire, Frame);
        int shots = 0;
        while (cycle.State != DrawState.Equipped) {
            DrawStep step = cycle.Tick(Aim, Frame);
            if (step.FireNow) {
                shots++;
                Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped), "fires the frame it reaches the aim");
            }
        }
        shots += Count(TickFrames(cycle, Aim, 30), step => step.FireNow);
        Assert.That(shots, Is.EqualTo(1));
        Assert.That(cycle.FireBuffered, Is.False);
    }

    [Test]
    public void ABufferedPressIsDroppedWhenAimIsReleasedFirstAndNeverFiresLater() {
        DrawCycle cycle = new DrawCycle(Settings);
        TickFrames(cycle, Aim, 3);
        cycle.Tick(Fire, Frame);
        Assert.That(cycle.FireBuffered, Is.True);
        cycle.Tick(Release, Frame);
        Assert.That(cycle.FireBuffered, Is.False, "dropped with the aim");
        TickUntil(cycle, Release, () => cycle.State == DrawState.Holstered);
        List<DrawStep> redraw = TickUntil(cycle, Aim, () => cycle.State == DrawState.Equipped);
        redraw.AddRange(TickFrames(cycle, Aim, 30));
        Assert.That(Count(redraw, step => step.FireNow), Is.Zero);
    }

    [Test]
    public void EquippedEachPressFiresAtOnceAndNoPressFiresWithoutAimOrInARoll() {
        DrawCycle cycle = Equipped(Settings);
        Assert.That(cycle.Tick(Fire, Frame).FireNow, Is.True);
        Assert.That(cycle.Tick(Aim, Frame).FireNow, Is.False);
        Assert.That(cycle.Tick(Fire, Frame).FireNow, Is.True);

        DrawCycle holstered = new DrawCycle(Settings);
        Assert.That(holstered.Tick((false, true, true), Frame).FireNow, Is.False, "no aim, no shot");
        Assert.That(holstered.FireBuffered, Is.False);

        DrawCycle rolling = Equipped(Settings);
        DrawStep step = rolling.Tick((true, true, false), Frame);
        Assert.That(step.FireNow, Is.False, "a roll takes the arms");
        Assert.That(rolling.FireBuffered, Is.False, "and the press is not kept for later");
    }

    // ---- Release and aim again ---------------------------------------------------------------------------------------

    [Test]
    public void AimReleasedBeforeTheGripGoesStraightBackToHolsteredWithoutEverGripping() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = new DrawCycle(settings);
        List<DrawStep> steps = TickFrames(cycle, Aim, 4);
        Assert.That(cycle.Progress, Is.LessThan(settings.gripShare));
        steps.Add(cycle.Tick(Release, Frame));
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(cycle.Progress, Is.EqualTo(0f));
        Assert.That(Count(steps, step => step.Attached || step.Detached), Is.Zero, "the pistol never left the holster");
    }

    [Test]
    public void AimReleasedAfterTheGripHolstersFromTheMirroredProgressWithTheHandWhereItWas() {
        PistolHandlingSettings settings = Settings;
        foreach (float releaseAt in new[] { 0.35f, 0.5f, 0.8f, 0.97f }) {
            DrawCycle cycle = new DrawCycle(settings);
            TickUntil(cycle, Aim, () => cycle.Progress >= releaseAt);
            float path = cycle.PathPosition;
            float weight = cycle.SupportWeight;
            DrawStep step = cycle.Tick(Release, Frame);
            Assert.That(cycle.State, Is.EqualTo(DrawState.Holstering), $"released at {releaseAt}");
            Assert.That(step.Detached, Is.False, "no restart: the pistol stays in the hand");
            Assert.That(cycle.ShouldBeInHand, Is.True);
            float pathStep = Frame / settings.holsterDuration * (1f - settings.gripShare) / settings.insertShare;
            Assert.That(cycle.PathPosition, Is.EqualTo(path - pathStep).Within(Tolerance), "continues from the same place on the path");
            Assert.That(cycle.SupportWeight, Is.LessThanOrEqualTo(weight + Tolerance));
            // No faster than the weight's own steepest rate along the path (a smoothstep over the join window).
            float steepest = 1.5f * pathStep / (1f - settings.supportJoinShare);
            Assert.That(weight - cycle.SupportWeight, Is.LessThanOrEqualTo(steepest + Tolerance), "the support hand does not snap");
        }
    }

    [Test]
    public void AimPressedDuringTheHolsterBeforeTheInsertDrawsAgainFromTheMirroredProgress() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = Equipped(settings);
        TickUntil(cycle, Release, () => cycle.State == DrawState.Holstering && cycle.Progress >= 0.4f);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstering));
        float path = cycle.PathPosition;
        DrawStep step = cycle.Tick(Aim, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(cycle.Progress, Is.EqualTo(path + Frame / settings.drawDuration).Within(Tolerance), "continues from the same place");
        Assert.That(step.Attached || step.Detached, Is.False, "the pistol stays in the hand");
        TickUntil(cycle, Aim, () => cycle.State == DrawState.Equipped);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
    }

    [Test]
    public void AimPressedAfterTheInsertRestartsTheDrawFromTheHandOnItsWayBack() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = Equipped(settings);
        TickUntil(cycle, Release, () => cycle.State == DrawState.Holstering && cycle.Progress >= 0.85f);
        Assert.That(cycle.ShouldBeInHand, Is.False, "inserted");
        cycle.Tick(Aim, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(cycle.Progress, Is.LessThan(settings.gripShare), "reaching for the holster again");
        Assert.That(cycle.ShouldBeInHand, Is.False);
        // The hand is close to the holster, so the reach back to the grip is short.
        int framesToGrip = TickUntil(cycle, Aim, () => cycle.ShouldBeInHand).Count;
        int fullReach = UnityEngine.Mathf.CeilToInt(settings.gripShare * settings.drawDuration / Frame);
        Assert.That(framesToGrip, Is.LessThan(fullReach));
        List<DrawStep> rest = TickUntil(cycle, Aim, () => cycle.State == DrawState.Equipped);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
        Assert.That(Count(rest, step => step.Detached), Is.Zero);
    }

    // ---- Quick holster -----------------------------------------------------------------------------------------------

    [Test]
    public void AStateThatTakesTheArmsQuickHolstersAnEquippedPistolAndTheDrawResumesAfterItWithAimHeld() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = Equipped(settings);
        cycle.Tick(AimInARoll, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstering));
        Assert.That(cycle.IsQuick, Is.True);
        int frames = 1 + TickUntil(cycle, AimInARoll, () => cycle.State == DrawState.Holstered).Count;
        Assert.That(frames * Frame, Is.EqualTo(settings.quickHolsterDuration).Within(1.5f * Frame), "at the quick time");
        TickFrames(cycle, AimInARoll, 20);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstered), "stays holstered while the state has the arms");
        Assert.That(cycle.IsQuick, Is.False);

        cycle.Tick(Aim, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Drawing), "drawn again once the state lets go, aim still held");
        TickUntil(cycle, Aim, () => cycle.State == DrawState.Equipped);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
    }

    [Test]
    public void AStateThatTakesTheArmsMidDrawQuickHolstersAfterTheGripAndSimplyStopsBeforeIt() {
        PistolHandlingSettings settings = Settings;
        DrawCycle before = new DrawCycle(settings);
        TickFrames(before, Aim, 3);
        Assert.That(before.Progress, Is.LessThan(settings.gripShare));
        DrawStep step = before.Tick(AimInARoll, Frame);
        Assert.That(before.State, Is.EqualTo(DrawState.Holstered), "before the grip: straight to holstered");
        Assert.That(step.Attached || step.Detached, Is.False);

        DrawCycle after = new DrawCycle(settings);
        TickUntil(after, Aim, () => after.Progress >= 0.6f);
        float path = after.PathPosition;
        after.Tick(AimInARoll, Frame);
        Assert.That(after.State, Is.EqualTo(DrawState.Holstering));
        Assert.That(after.IsQuick, Is.True);
        Assert.That(after.PathPosition, Is.LessThan(path).And.GreaterThan(path - 0.15f), "from the mirrored progress");
        TickUntil(after, AimInARoll, () => after.State == DrawState.Holstered);
        Assert.That(after.State, Is.EqualTo(DrawState.Holstered));
    }

    [Test]
    public void AHolsterUnderWaySpeedsUpWhenAStateTakesTheArms() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = Equipped(settings);
        TickFrames(cycle, Release, 5);
        Assert.That(cycle.IsQuick, Is.False);
        float progress = cycle.Progress;
        cycle.Tick((false, false, false), Frame);
        Assert.That(cycle.IsQuick, Is.True);
        Assert.That(cycle.Progress - progress, Is.EqualTo(Frame / settings.quickHolsterDuration).Within(Tolerance));
    }

    // ---- The support hand and odd settings -----------------------------------------------------------------------------

    [Test]
    public void TheSupportHandJoinsLateInTheDrawAndHasLetGoByAboutAThirdOfTheHolster() {
        PistolHandlingSettings settings = Settings;
        DrawCycle cycle = new DrawCycle(settings);
        while (cycle.State != DrawState.Equipped) {
            cycle.Tick(Aim, Frame);
            if (cycle.State == DrawState.Drawing && cycle.Progress <= settings.supportJoinShare) {
                Assert.That(cycle.SupportWeight, Is.EqualTo(0f), "not before the support-join share");
            }
        }
        Assert.That(cycle.SupportWeight, Is.EqualTo(1f));
        TickUntil(cycle, Release, () => cycle.State == DrawState.Holstering && cycle.Progress >= 0.36f);
        Assert.That(cycle.SupportWeight, Is.EqualTo(0f), "let go by about a third of the holster");
    }

    [Test]
    public void ZeroDurationsDrawAndHolsterInOneFrame() {
        PistolHandlingSettings settings = Settings;
        settings.drawDuration = 0f;
        settings.holsterDuration = 0f;
        DrawCycle cycle = new DrawCycle(settings);
        DrawStep draw = cycle.Tick(Fire, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Equipped));
        Assert.That(draw.Attached, Is.True);
        Assert.That(draw.FireNow, Is.True, "the press fires on the same frame");
        DrawStep holster = cycle.Tick(Release, Frame);
        Assert.That(cycle.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(holster.Detached, Is.True);
    }
}
