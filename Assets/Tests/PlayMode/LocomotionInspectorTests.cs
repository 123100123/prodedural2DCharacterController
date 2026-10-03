using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// The runtime debug surface on the real character: the panel toggles with its key, the gait preview drives the gait
// without input, and tuning takes effect while playing.
public class LocomotionInspectorTests {
    private ScenarioStage stage;

    [UnitySetUp]
    public IEnumerator LoadStage() {
        stage = new ScenarioStage();
        yield return stage.Load();
    }

    [TearDown]
    public void Unload() => stage.Unload();

    [UnityTest]
    public IEnumerator ThePanelOpensAndClosesWithItsKey() {
        VisualElement panel = FindPanel();
        Assert.That(panel, Is.Not.Null, "the scene has a debug panel");
        Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "hidden until asked for");

        yield return Tap(Key.F1);
        Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        yield return Tap(Key.F1);
        Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
    }

    [UnityTest]
    public IEnumerator ThePreviewRunsWalksAndStopsTheCharacterWithoutInput() {
        LocomotionInspector inspector = stage.Character.Inspector;
        inspector.Preview = new GaitPreview((GaitPreview.Mode.Run, null));
        yield return Frames(150);
        Assert.That(Mathf.Abs(stage.Stride.GroundSpeed), Is.EqualTo(stage.Stride.Run.speed).Within(0.01f));
        Assert.That(inspector.ReadTelemetry().Classification, Is.EqualTo(LocomotionTelemetry.Mode.Run));

        inspector.Preview = new GaitPreview((GaitPreview.Mode.Walk, 5f));
        yield return Frames(120);
        Assert.That(Mathf.Abs(stage.Stride.GroundSpeed), Is.EqualTo(5f).Within(0.01f), "a chosen speed in the transition region");

        inspector.Preview = new GaitPreview((GaitPreview.Mode.Idle, null));
        yield return Frames(90);
        Assert.That(stage.Stride.GroundSpeed, Is.EqualTo(0f));
        inspector.Preview = GaitPreview.Off;
    }

    [UnityTest]
    public IEnumerator TuningTakesEffectWhilePlaying() {
        LocomotionInspector inspector = stage.Character.Inspector;
        LocomotionPreset tuned = inspector.Tuning.Capture();
        tuned.run.lean = 24f;
        tuned.posture.maxTotalLean = 30f;
        inspector.Tuning.Apply(tuned);
        inspector.Preview = new GaitPreview((GaitPreview.Mode.Run, null));
        yield return Frames(180);

        Assert.That(inspector.ReadTelemetry().PostureTargetLean, Is.EqualTo(24f).Within(1.5f));
        Assert.That(inspector.Tuning.Capture().ToJson(), Is.EqualTo(tuned.ToJson()), "what is applied reads back");
        inspector.Preview = GaitPreview.Off;
        inspector.Tuning.Apply(inspector.Defaults);
    }

    private static VisualElement FindPanel() {
        foreach (UIDocument document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) {
            VisualElement panel = document.rootVisualElement?.Q(className: "loco-panel");
            if (panel != null) {
                return panel;
            }
        }
        return null;
    }

    private IEnumerator Tap(Key key) {
        stage.Press(key);
        yield return null;
        stage.Release(key);
        yield return Frames(2);
    }

    private static IEnumerator Frames(int count) {
        for (int frame = 0; frame < count; frame++) {
            yield return null;
        }
    }
}
