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
        AddSection(("Crouch", CrouchReadouts()));
        AddSection(("Airborne", AirborneReadouts()));
        AddSection(("Timing", TimingReadouts()));
        AddSection(("Support", SupportReadouts()));
        AddSection(("Height", HeightReadouts()));
        AddSection(("Legs", LegReadouts()));
        AddSection(("Posture", PostureReadouts()));
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
        yield return ("Classification", ClassificationText);
        yield return ("Preview override", t => t.Preview.IsActive ? $"{t.Preview.PreviewMode} {(t.Preview.Speed.HasValue ? t.Preview.Speed.Value.ToString("0.00") + " u/s" : "")}" : "off (player input)");
        yield return ("Move axis / run held", t => $"{t.MoveAxis:0.00} / {(t.RunHeld ? "yes" : "no")}");
        yield return ("Ground / intended speed", t => $"{t.GroundSpeed:0.00} / {t.IntendedSpeed:0.00} u/s");
        yield return ("Pace moving / run blend", t => $"{t.Pace.Moving:0.00} / {t.Pace.Running:0.00}");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> CrouchReadouts() {
        yield return ("Crouch share", t => $"{t.CrouchShare:0.00}{(t.IsHeadroomBlocked ? "  (held by a low ceiling)" : t.IsCrouching ? "  (crouching)" : "")}");
        yield return ("Pelvis sunk / full crouch depth", t => $"{t.CrouchDrop:0.000} / {t.CrouchDepth:0.000} u");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> AirborneReadouts() {
        yield return ("In the air", t => t.Air.IsAirborne ? (t.Air.LaunchedByJump ? "jump" : "fall") : "no");
        yield return ("Velocity X / Y", t => t.Air.IsAirborne ? $"{t.Air.Velocity.x:0.00} / {t.Air.Velocity.y:0.00} u/s" : "-");
        yield return ("Launch momentum X / Y", t => t.Air.IsAirborne ? $"{t.Air.LaunchVelocity.x:0.00} / {t.Air.LaunchVelocity.y:0.00} u/s" : "-");
        yield return ("Time airborne / falling", t => t.Air.IsAirborne ? $"{t.Air.TimeAirborne:0.00} / {t.Air.TimeFalling:0.00} s" : "-");
        yield return ("Time since launch", t => t.Air.IsAirborne ? $"{t.Air.TimeSinceLaunch:0.00} s" : "-");
        yield return ("Phase", AirPhaseText);
        yield return ("Pose blend: rising / hang / falling", t => t.Air.IsAirborne ? $"{t.Air.Ascent:0.00} / {t.Air.Apex:0.00} / {t.Air.Descent:0.00}" : "-");
        yield return ("Running pose share", t => t.Air.IsAirborne ? $"{t.Air.RunningPoseShare:0.00}" : "-");
        yield return ("Fall flail strength", t => t.Air.IsAirborne ? $"{t.Air.FlailStrength:0.00}" : "-");
    }

    private static string AirPhaseText(LocomotionTelemetry t) {
        if (!t.Air.IsAirborne) {
            return "-";
        }
        if (t.Air.Ascent >= t.Air.Apex && t.Air.Ascent >= t.Air.Descent) {
            return "ASCENT";
        }
        return t.Air.Descent >= t.Air.Apex ? "DESCENT" : "APEX";
    }

    private static string ClassificationText(LocomotionTelemetry t) {
        if (!t.IsDrivingGait) {
            return "NOT GROUNDED";
        }
        string mode = t.Classification.ToString().ToUpperInvariant();
        return t.IsCrouched ? $"CROUCH {mode}" : mode;
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> TimingReadouts() {
        yield return ("Gait phase", t => $"{t.GaitPhase:0.000}");
        yield return ("Cycle period / steps per second", t => $"{t.CyclePeriod:0.000} s / {t.StepsPerSecond:0.00}");
        yield return ("Duty factor / swing share", t => $"{t.Gait.dutyFactor:0.000} / {t.SwingShare:0.000}");
        yield return ("Catch-up", t => $"{t.CatchUp:0.00}×");
        yield return ("Last planted", t => t.LastPlantWasFront ? "front" : "back");
        yield return ("Contact front→back / back→front", t => $"{t.FrontToBackContact:0.000} s / {t.BackToFrontContact:0.000} s");
        yield return ("Balance steps / phase resyncs", t => $"{t.BalanceSteps.count} / {t.PhaseResyncs.count}");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> SupportReadouts() {
        yield return ("Support leg", SupportText);
        yield return ("Support foot X / stance progress", t => t.Support.HasSupport ? $"{t.Support.FootX:0.000} / {t.Support.Progress:0.00}" : "-");
        yield return ("Contact lead share", t => $"{t.Gait.contactLeadShare:0.00}");
        yield return ("Pelvis X / target X / anchor X", t => $"{t.BodyPosition.x:0.000} / {t.BodyTarget.x:0.000} / {t.StrideAnchorX:0.000}");
        yield return ("Support offset / surge", t => $"{t.PelvisOffsets.support:+0.000;-0.000} / {t.PelvisOffsets.surge:+0.000;-0.000}");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> HeightReadouts() {
        yield return ("Pelvis Y / target Y", t => $"{t.BodyPosition.y:0.000} / {t.BodyTarget.y:0.000}");
        yield return ("Baseline / bob", t => $"{t.BodyHeights.baseline:0.000} / {t.BodyHeights.gait - t.BodyHeights.baseline:+0.000;-0.000}");
        yield return ("Reach ceiling", t => $"{t.BodyHeights.ceiling:0.000}{(t.BodyHeights.ceiling < t.BodyHeights.gait ? "  (limiting)" : "")}");
        yield return ("Pelvis drop / bob amplitude", t => $"{t.Gait.pelvisDrop:0.00} / {t.Gait.bobAmplitude:0.00} u");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> LegReadouts() {
        yield return ("Measured leg length", t => $"{t.Front.Length:0.000} u");
        yield return ("Front leg", t => LegText(t.Front));
        yield return ("Back leg", t => LegText(t.Back));
        yield return ("Lift height", t => $"{t.Gait.liftHeight:0.00} u");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> PostureReadouts() {
        yield return ("Gait lean / acceleration lean", t => $"{t.Gait.lean:0.0}° / {t.AccelerationLean:0.0}°");
        yield return ("Total lean target / actual", t => $"{t.PostureTargetLean:0.0}° / {t.PostureLean:0.0}°");
        yield return ("Chest secondary pitch", t => $"{t.ChestPitch:0.00}°");
        yield return ("Head stabilization weight", t => $"{t.HeadLevelWeight:0.00}");
    }

    private static string SupportText(LocomotionTelemetry t) {
        if (t.BothFeetPlanted) {
            return $"both feet (carrying: {t.Support.SupportLeg.ToString().ToLowerInvariant()})";
        }
        if (!t.Support.HasSupport) {
            return "none";
        }
        return t.Support.IsPlanted ? t.Support.SupportLeg.ToString().ToLowerInvariant() : "flight";
    }

    private static string LegText(LocomotionTelemetry.Leg leg) {
        string state = leg.IsGrounded ? $"planted {leg.SecondsSincePlant:0.00} s" : $"swing {leg.StepProgress:0.00}";
        return $"{state}, hip→foot {leg.HipToFoot:0.000} u, extension {leg.Extension:0.00}";
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
