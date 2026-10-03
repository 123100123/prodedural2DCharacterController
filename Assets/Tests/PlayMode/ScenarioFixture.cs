using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Shared setup for scenario tests: loads the stage, records the gait every frame, and writes each test's measurements
// to Temp/LocomotionScenarios/<test>.txt.
public abstract class ScenarioFixture {
    // Contact intervals may differ this much (share of their mean) at steady speed and through a speed change.
    protected const float SteadyAsymmetry = 0.15f;
    protected const float TransitionAsymmetry = 0.3f;
    // The rig's foot effector sits about 0.06 off its target even standing; more than this is a foot left unreachable.
    protected const float PlantedFootTolerance = 0.12f;

    private readonly List<string> report = new List<string>();

    protected ScenarioStage Stage { get; private set; }
    protected GaitRecorder Recorder { get; private set; }

    [UnitySetUp]
    public IEnumerator LoadStage() {
        Stage = new ScenarioStage();
        yield return Stage.Load();
        Recorder = new GaitRecorder((Stage.Character, Stage.Stride));
        Stage.Sampler.Recorder = Recorder;
        report.Clear();
    }

    [TearDown]
    public void WriteReportAndUnload() {
        Stage.Unload();
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, Sanitize(TestContext.CurrentContext.Test.Name) + ".txt"), report);
    }

    protected static IEnumerator Hold(float seconds) {
        int frames = Mathf.RoundToInt(seconds / ScenarioStage.FrameTime);
        for (int frame = 0; frame < frames; frame++) {
            yield return null;
        }
    }

    protected GaitMetrics Measure(string label, float start, float end) {
        GaitMetrics metrics = new GaitMetrics(Recorder, (start, end));
        report.Add(metrics.Describe(label));
        return metrics;
    }

    protected void Report(string line) => report.Add(line);

    protected static void AssertEvenGait((GaitMetrics metrics, float maxAsymmetry) check) {
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
