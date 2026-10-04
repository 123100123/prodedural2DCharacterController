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
        LocomotionInspector inspector = stage.Inspector;
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
    public IEnumerator TheTelemetryReportsTheSupportHeightAndLegsOfARun() {
        LocomotionInspector inspector = stage.Inspector;
        inspector.Preview = new GaitPreview((GaitPreview.Mode.Run, null));
        yield return Frames(120);

        (bool planted, bool flight) seen = (false, false);
        for (int frame = 0; frame < 60; frame++) {
            yield return null;
            LocomotionTelemetry t = inspector.ReadTelemetry();
            seen = (seen.planted || t.Support.IsPlanted, seen.flight || (t.Support.HasSupport && !t.Support.IsPlanted));
            Assert.That(t.BodyHeights.ceiling, Is.GreaterThanOrEqualTo(t.BodyTarget.y - 0.001f), "the target never exceeds the reach ceiling");
            Assert.That(t.BodyHeights.gait - t.BodyHeights.baseline, Is.InRange(-t.Gait.bobAmplitude - 0.001f, t.Gait.bobAmplitude + 0.001f), "the bob stays within its amplitude");
            Assert.That(t.Front.Length, Is.EqualTo(stage.Stride.FrontLeg.LegLength), "the leg length is the measured one");
            if (t.Support.IsPlanted) {
                LocomotionTelemetry.Leg carrying = t.Support.SupportLeg == SupportStance.Leg.Front ? t.Front : t.Back;
                Assert.That(carrying.IsGrounded, Is.True, "the support leg is a planted one");
                Assert.That(carrying.Extension, Is.InRange(0.5f, 1.05f));
            }
        }
        Assert.That(seen.planted && seen.flight, Is.True, "a run shows both support and flight");
        inspector.Preview = GaitPreview.Off;
    }

    [UnityTest]
    public IEnumerator TuningTakesEffectWhilePlaying() {
        LocomotionInspector inspector = stage.Inspector;
        MovementPreset tuned = inspector.Tuning.Capture();
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

    // The locomotion works the same without the panel: nothing in the gait depends on it, open, closed or gone.
    [UnityTest]
    public IEnumerator TheGaitIsTheSameWithThePanelHiddenOpenOrGone() {
        Key[] walkRight = { Key.D };
        stage.Press(walkRight);
        yield return Frames(180);
        (float speed, float interval) hidden = MeasureWalk();

        yield return Tap(Key.F1);
        yield return Frames(180);
        (float speed, float interval) open = MeasureWalk();

        foreach (LocomotionDebugPanel panel in Object.FindObjectsByType<LocomotionDebugPanel>(FindObjectsSortMode.None)) {
            Object.Destroy(panel.gameObject);
        }
        yield return Frames(180);
        (float speed, float interval) gone = MeasureWalk();

        Assert.That(open.speed, Is.EqualTo(hidden.speed).Within(0.05f));
        Assert.That(gone.speed, Is.EqualTo(hidden.speed).Within(0.05f));
        Assert.That(gone.interval, Is.EqualTo(hidden.interval).Within(hidden.interval * 0.05f), "the same cadence with no panel");
        Assert.That(open.interval, Is.EqualTo(hidden.interval).Within(hidden.interval * 0.05f), "and with it open");
    }

    private (float speed, float interval) MeasureWalk() {
        float speed = Mathf.Abs(stage.Stride.GroundSpeed);
        return (speed, stage.Stride.CyclePeriod);
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
