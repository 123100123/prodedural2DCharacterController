using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Drives the real character through grounded locomotion scenarios and checks the gait stays even: legs alternate,
// opposite contacts stay evenly spaced through walk/run changes, the shared phase is never reset, and the posture
// leans forward more running than walking. Each test writes its measurements to Temp/LocomotionScenarios.
public class LocomotionScenarioTests {
    // Contact intervals may differ this much (share of their mean) at steady speed and through a speed change.
    private const float SteadyAsymmetry = 0.15f;
    private const float TransitionAsymmetry = 0.3f;

    private ScenarioStage stage;
    private GaitRecorder recorder;
    private readonly List<string> report = new List<string>();

    [UnitySetUp]
    public IEnumerator LoadStage() {
        stage = new ScenarioStage();
        yield return stage.Load();
        recorder = new GaitRecorder((stage.Character, stage.Stride));
        stage.Sampler.Recorder = recorder;
        report.Clear();
    }

    [TearDown]
    public void WriteReportAndUnload() {
        stage.Unload();
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, Sanitize(TestContext.CurrentContext.Test.Name) + ".txt"), report);
    }

    [UnityTest]
    public IEnumerator WalkRunTransitionsKeepContactsEven([Values(0f, 0.24f, 0.48f, 0.72f)] float runPressDelay) {
        yield return Hold(1.5f);
        stage.Press(Key.D);
        yield return Hold(3f + runPressDelay);
        GaitMetrics startAndWalk = Measure("idle -> walk", 1.5f, 3.2f);
        GaitMetrics walk = Measure("steady walk", 3.2f, recorder.Now);

        float runStart = recorder.Now;
        stage.Press(Key.LeftShift);
        yield return Hold(3.5f);
        GaitMetrics walkToRun = Measure("walk -> run", runStart, runStart + 1.5f);
        GaitMetrics run = Measure("steady run", runStart + 1.5f, recorder.Now);

        float walkStart = recorder.Now;
        stage.Release(Key.LeftShift);
        yield return Hold(3.5f);
        GaitMetrics runToWalk = Measure("run -> walk", walkStart, walkStart + 1.5f);
        Measure("walk again", walkStart + 1.5f, recorder.Now);

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
        stage.Press(Key.D);
        yield return Hold(2.5f);
        float togglesStart = recorder.Now;
        for (int toggle = 0; toggle < 8; toggle++) {
            if (toggle % 2 == 0) {
                stage.Press(Key.LeftShift);
            }
            else {
                stage.Release(Key.LeftShift);
            }
            yield return Hold(0.45f);
        }
        stage.Release(Key.LeftShift);
        yield return Hold(1.5f);
        AssertEvenGait((Measure("run toggles", togglesStart, recorder.Now), TransitionAsymmetry));
    }

    [UnityTest]
    public IEnumerator StartsAndStopsStepWithAlternatingLegs() {
        for (int repeat = 0; repeat < 3; repeat++) {
            float start = recorder.Now;
            stage.Press(Key.D);
            yield return Hold(1.2f);
            stage.Release(Key.D);
            yield return Hold(1.2f);
            GaitMetrics startStop = Measure($"start/stop {repeat}", start, recorder.Now);
            Assert.That(startStop.PhaseJumps, Is.Zero, "no phase resets while moving");
        }

        float runStart = recorder.Now;
        stage.Press(Key.D, Key.LeftShift);
        yield return Hold(2.5f);
        stage.ReleaseAll();
        yield return Hold(2f);
        GaitMetrics runStop = Measure("stop from full run", runStart, recorder.Now);
        Assert.That(runStop.RepeatedLegLifts, Is.Zero, "the legs alternate into the stop");
        Assert.That(runStop.WorstPlantedFootGap, Is.LessThan(0.1f), "planted feet stay on their footholds");
    }

    [UnityTest]
    public IEnumerator ReversingDirectionKeepsTheGaitEven() {
        stage.Press(Key.D);
        yield return Hold(2.5f);
        float walkReverse = recorder.Now;
        stage.Release(Key.D);
        stage.Press(Key.A);
        yield return Hold(2.5f);
        GaitMetrics fromWalk = Measure("reverse from walk", walkReverse, recorder.Now);

        stage.Press(Key.LeftShift);
        yield return Hold(2.5f);
        float runReverse = recorder.Now;
        stage.Release(Key.A);
        stage.Press(Key.D);
        yield return Hold(3f);
        GaitMetrics fromRun = Measure("reverse from run", runReverse, recorder.Now);

        Assert.That(fromWalk.PhaseJumps, Is.LessThanOrEqualTo(1), "at most the restart after the stop");
        Assert.That(fromRun.PhaseJumps, Is.LessThanOrEqualTo(1), "at most the restart after the stop");
        Assert.That(fromWalk.RepeatedLegLifts + fromRun.RepeatedLegLifts, Is.LessThanOrEqualTo(2), "at most one repeated leg per turn");
    }

    [UnityTest]
    public IEnumerator FullSpeedRunLooksPropelledNotCrouched() {
        yield return Hold(1.5f);
        GaitMetrics standing = Measure("standing", 0.5f, recorder.Now);
        stage.Press(Key.D, Key.LeftShift);
        yield return Hold(4f);
        GaitMetrics run = Measure("full run", recorder.Now - 2f, recorder.Now);

        Assert.That(run.FlightShare, Is.GreaterThan(0.05f), "running has a flight phase");
        Assert.That(standing.MeanBodyAboveStance - run.MeanBodyAboveStance, Is.LessThan(0.3f), "the run pelvis is not crouched");
        Assert.That(run.MeanLean, Is.GreaterThan(standing.MeanLean + 6f), "the run leans forward");
        Assert.That(run.HeadAngleDeviation, Is.LessThan(2.5f), "the head stays steady while running");
        Assert.That(run.WorstPlantedFootGap, Is.LessThan(0.1f), "planted feet stay on their footholds");
        AssertEvenGait((run, SteadyAsymmetry));
    }

    private IEnumerator Hold(float seconds) {
        int frames = Mathf.RoundToInt(seconds / ScenarioStage.FrameTime);
        for (int frame = 0; frame < frames; frame++) {
            yield return null;
        }
    }

    private GaitMetrics Measure(string label, float start, float end) {
        GaitMetrics metrics = new GaitMetrics(recorder, (start, end));
        report.Add(metrics.Describe(label));
        return metrics;
    }

    private static void AssertEvenGait((GaitMetrics metrics, float maxAsymmetry) check) {
        string label = $"[{check.metrics.Window.start:F2}, {check.metrics.Window.end:F2}]";
        Assert.That(check.metrics.RepeatedLegLifts, Is.Zero, $"legs alternate in {label}");
        Assert.That(check.metrics.PhaseJumps, Is.Zero, $"no phase resets in {label}");
        Assert.That(check.metrics.WorstContactAsymmetry, Is.LessThan(check.maxAsymmetry), $"even contacts in {label}");
    }

    private static string Sanitize(string testName) {
        foreach (char invalid in Path.GetInvalidFileNameChars()) {
            testName = testName.Replace(invalid, '_');
        }
        return testName;
    }
}
