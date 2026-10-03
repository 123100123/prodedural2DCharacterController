using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Drives the real character through grounded locomotion scenarios and checks the gait stays even: legs alternate,
// opposite contacts stay evenly spaced through walk/run changes, the shared phase is never reset, and the posture
// leans forward more running than walking. Each test writes its measurements to Temp/LocomotionScenarios.
public class LocomotionScenarioTests : ScenarioFixture {
    // Toggling the run every step or so changes the speed between consecutive steps, so their durations genuinely differ.
    private const float RapidToggleAsymmetry = 0.35f;

    [UnityTest]
    public IEnumerator WalkRunTransitionsKeepContactsEven([Values(0f, 0.24f, 0.48f, 0.72f)] float runPressDelay) {
        yield return Hold(1.5f);
        Stage.Press(Key.D);
        yield return Hold(3f + runPressDelay);
        GaitMetrics startAndWalk = Measure("idle -> walk", 1.5f, 3.2f);
        GaitMetrics walk = Measure("steady walk", 3.2f, Recorder.Now);

        float runStart = Recorder.Now;
        Stage.Press(Key.LeftShift);
        yield return Hold(3.5f);
        GaitMetrics walkToRun = Measure("walk -> run", runStart, runStart + 1.5f);
        GaitMetrics run = Measure("steady run", runStart + 1.5f, Recorder.Now);

        float walkStart = Recorder.Now;
        Stage.Release(Key.LeftShift);
        yield return Hold(3.5f);
        GaitMetrics runToWalk = Measure("run -> walk", walkStart, walkStart + 1.5f);
        Measure("walk again", walkStart + 1.5f, Recorder.Now);

        AssertEvenGait((walk, SteadyAsymmetry));
        AssertEvenGait((run, SteadyAsymmetry));
        AssertEvenGait((startAndWalk, TransitionAsymmetry));
        AssertEvenGait((walkToRun, TransitionAsymmetry));
        AssertEvenGait((runToWalk, TransitionAsymmetry));
        Assert.That(run.MeanLean, Is.GreaterThan(walk.MeanLean + 3f), "running leans further forward than walking");
        Assert.That(walk.MeanLean, Is.GreaterThan(1f), "walking leans forward");
    }

    [UnityTest]
    public IEnumerator RepeatedRunTogglesKeepTheLegsAlternating() {
        Stage.Press(Key.D);
        yield return Hold(2.5f);
        float togglesStart = Recorder.Now;
        for (int toggle = 0; toggle < 8; toggle++) {
            if (toggle % 2 == 0) {
                Stage.Press(Key.LeftShift);
            }
            else {
                Stage.Release(Key.LeftShift);
            }
            yield return Hold(0.45f);
        }
        Stage.Release(Key.LeftShift);
        yield return Hold(1.5f);
        AssertEvenGait((Measure("run toggles", togglesStart, Recorder.Now), RapidToggleAsymmetry));
    }

    [UnityTest]
    public IEnumerator StartsAndStopsStepWithAlternatingLegs() {
        for (int repeat = 0; repeat < 3; repeat++) {
            float start = Recorder.Now;
            Stage.Press(Key.D);
            yield return Hold(1.2f);
            Stage.Release(Key.D);
            yield return Hold(1.2f);
            GaitMetrics startStop = Measure($"start/stop {repeat}", start, Recorder.Now);
            Assert.That(startStop.PhaseJumps, Is.Zero, "no phase resets while moving");
        }

        float runStart = Recorder.Now;
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2.5f);
        Stage.ReleaseAll();
        yield return Hold(2f);
        GaitMetrics runStop = Measure("stop from full run", runStart, Recorder.Now);
        Assert.That(runStop.RepeatedLegLifts, Is.Zero, "the legs alternate into the stop");
        Assert.That(runStop.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance), "planted feet stay on their footholds");
    }

    [UnityTest]
    public IEnumerator ReversingDirectionKeepsTheGaitEven() {
        Stage.Press(Key.D);
        yield return Hold(2.5f);
        float walkReverse = Recorder.Now;
        Stage.Release(Key.D);
        Stage.Press(Key.A);
        yield return Hold(2.5f);
        GaitMetrics fromWalk = Measure("reverse from walk", walkReverse, Recorder.Now);

        Stage.Press(Key.LeftShift);
        yield return Hold(2.5f);
        float runReverse = Recorder.Now;
        Stage.Release(Key.A);
        Stage.Press(Key.D);
        yield return Hold(3f);
        GaitMetrics fromRun = Measure("reverse from run", runReverse, Recorder.Now);

        Assert.That(fromWalk.PhaseJumps, Is.LessThanOrEqualTo(1), "at most the restart after the stop");
        Assert.That(fromRun.PhaseJumps, Is.LessThanOrEqualTo(1), "at most the restart after the stop");
        Assert.That(fromWalk.RepeatedLegLifts + fromRun.RepeatedLegLifts, Is.LessThanOrEqualTo(2), "at most one repeated leg per turn");
    }

    [UnityTest]
    public IEnumerator FullSpeedRunLooksPropelledNotCrouched() {
        yield return Hold(1.5f);
        GaitMetrics standing = Measure("standing", 0.5f, Recorder.Now);
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(4f);
        GaitMetrics run = Measure("full run", Recorder.Now - 2f, Recorder.Now);

        Assert.That(run.FlightShare, Is.GreaterThan(0.05f), "running has a flight phase");
        Assert.That(standing.MeanBodyAboveStance - run.MeanBodyAboveStance, Is.LessThan(0.3f), "the run pelvis is not crouched");
        Assert.That(run.MeanLean, Is.GreaterThan(standing.MeanLean + 6f), "the run leans forward");
        Assert.That(run.HeadAngleDeviation, Is.LessThan(2.5f), "the head stays steady while running");
        Assert.That(run.WorstPlantedFootGap, Is.LessThan(PlantedFootTolerance), "planted feet stay on their footholds");
        AssertEvenGait((run, SteadyAsymmetry));
    }

}
