using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

// The LIVE tab's readout: a list of named values formatted from the locomotion telemetry, and flags that light up for
// a moment after a balance step, a phase resync or a failed foothold so timing problems are visible as they happen.
public sealed class LiveTelemetryView {
    // A flag stays lit this long after its event.
    private const float FlagSeconds = 1.5f;

    private readonly List<(Label value, Func<LocomotionTelemetry, string> format)> readouts = new List<(Label, Func<LocomotionTelemetry, string>)>();
    private readonly List<(Label flag, Func<LocomotionTelemetry, (int count, float secondsAgo)> read, string name)> flags =
        new List<(Label, Func<LocomotionTelemetry, (int, float)>, string)>();

    public LiveTelemetryView() {
        Root = new VisualElement();
        AddFlags();
        AddSection(("Gait", GaitReadouts()));
        AddSection(("Posture", PostureReadouts()));
        AddSection(("Body and legs", BodyReadouts()));
    }

    public VisualElement Root { get; }

    public void Show(LocomotionTelemetry telemetry) {
        foreach ((Label value, Func<LocomotionTelemetry, string> format) readout in readouts) {
            readout.value.text = readout.format(telemetry);
        }
        foreach ((Label flag, Func<LocomotionTelemetry, (int count, float secondsAgo)> read, string name) flag in flags) {
            (int count, float secondsAgo) happened = flag.read(telemetry);
            flag.flag.text = $"{flag.name} ({happened.count})";
            flag.flag.EnableInClassList("loco-flag--active", happened.secondsAgo < FlagSeconds);
        }
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> GaitReadouts() {
        yield return ("Classification", t => t.IsDrivingGait ? t.Classification.ToString().ToUpperInvariant() : "NOT GROUNDED");
        yield return ("Preview override", t => t.Preview.IsActive ? $"{t.Preview.PreviewMode} {(t.Preview.Speed.HasValue ? t.Preview.Speed.Value.ToString("0.00") + " u/s" : "")}" : "off (player input)");
        yield return ("Move axis / run held", t => $"{t.MoveAxis:0.00} / {(t.RunHeld ? "yes" : "no")}");
        yield return ("Ground / intended speed", t => $"{t.GroundSpeed:0.00} / {t.IntendedSpeed:0.00} u/s");
        yield return ("Pace moving / run blend", t => $"{t.Pace.Moving:0.00} / {t.Pace.Running:0.00}");
        yield return ("Gait phase", t => $"{t.GaitPhase:0.000}");
        yield return ("Cycle period / steps per second", t => $"{t.CyclePeriod:0.000} s / {t.StepsPerSecond:0.00}");
        yield return ("Swing share / catch-up", t => $"{t.SwingShare:0.000} / {t.CatchUp:0.00}×");
        yield return ("Duty factor / lift height", t => $"{t.Gait.dutyFactor:0.000} / {t.Gait.liftHeight:0.00} u");
        yield return ("Pelvis drop / bob", t => $"{t.Gait.pelvisDrop:0.00} / {t.Gait.bobAmplitude:0.00} u");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> PostureReadouts() {
        yield return ("Lean target / actual", t => $"{t.PostureTargetLean:0.0}° / {t.PostureLean:0.0}°");
        yield return ("Acceleration lean", t => $"{t.AccelerationLean:0.0}°");
        yield return ("Chest secondary pitch", t => $"{t.ChestPitch:0.00}°");
        yield return ("Head stabilization weight", t => $"{t.HeadLevelWeight:0.00}");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> BodyReadouts() {
        yield return ("Body Y / target Y", t => $"{t.BodyPosition.y:0.000} / {t.BodyTarget.y:0.000}");
        yield return ("Front leg", t => LegText(t.Front));
        yield return ("Back leg", t => LegText(t.Back));
        yield return ("Last planted", t => t.LastPlantWasFront ? "front" : "back");
        yield return ("Contact front→back / back→front", t => $"{t.FrontToBackContact:0.000} s / {t.BackToFrontContact:0.000} s");
    }

    private static string LegText(LocomotionTelemetry.Leg leg) {
        string state = leg.IsGrounded ? "planted" : $"swing {leg.StepProgress:0.00}";
        return $"{state}, planted {leg.SecondsSincePlant:0.00} s ago";
    }

    private void AddFlags() {
        VisualElement row = new VisualElement();
        row.AddToClassList("loco-flags");
        AddFlag((row, "BALANCE STEP", t => t.BalanceSteps));
        AddFlag((row, "PHASE RESYNC", t => t.PhaseResyncs));
        AddFlag((row, "FOOTHOLD FAILED", t => t.FootholdFailures));
        Root.Add(row);
    }

    private void AddFlag((VisualElement row, string name, Func<LocomotionTelemetry, (int count, float secondsAgo)> read) flag) {
        Label label = new Label(flag.name);
        label.AddToClassList("loco-flag");
        flag.row.Add(label);
        flags.Add((label, flag.read, flag.name));
    }

    private void AddSection((string title, IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> entries) section) {
        Label heading = new Label(section.title);
        heading.AddToClassList("loco-heading");
        Root.Add(heading);
        foreach ((string label, Func<LocomotionTelemetry, string> format) entry in section.entries) {
            VisualElement row = new VisualElement();
            row.AddToClassList("loco-live-row");
            Label name = new Label(entry.label);
            name.AddToClassList("loco-live-row__label");
            Label value = new Label();
            value.AddToClassList("loco-live-row__value");
            row.Add(name);
            row.Add(value);
            Root.Add(row);
            readouts.Add((value, entry.format));
        }
    }
}
