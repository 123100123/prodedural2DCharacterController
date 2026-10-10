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
        AddSection(("Run intent", RunIntentReadouts()));
        AddSection(("Roll", RollReadouts()));
        AddSection(("Weapon", WeaponReadouts()));
        AddSection(("Slope gait", SlopeGaitReadouts()));
        AddSection(("Crouch", CrouchReadouts()));
        AddSection(("Airborne", AirborneReadouts()));
        AddSection(("Landing", LandingReadouts()));
        AddSection(("Lighting", LightingReadouts()));
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

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> RunIntentReadouts() {
        yield return ("Run held / run intent", t => $"{(t.RunHeld ? "yes" : "no")} / {(t.RunIntent ? "yes" : "no")}");
        yield return ("Run intent pending", t => t.RunIntentPending ? $"yes (waiting: {t.Readiness.Reason})" : "no");
        yield return ("Current state", t => t.StateName);
        yield return ("Can transition to run", t => t.Readiness.CanRun ? "yes" : $"no ({t.Readiness.Reason})");
        yield return ("Last transition", t => $"{t.PreviousStateName} → {t.StateName}, {t.SecondsSinceStateChange:0.00} s ago (#{t.StateChanges})");
        yield return ("Velocity carried into walk/run", t => $"{t.CarriedSpeed:0.00} u/s");
    }

    // Where the pistol is: in the holster or the hand, and its muzzle in the world.
    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> WeaponReadouts() {
        yield return ("Parent", t => t.Sidearm == null ? "no pistol" : t.Weapon.InHand ? PistolView.PivotName : PistolView.HolsterName);
        yield return ("In hand / holstered", t => t.Sidearm == null ? "-" : t.Weapon.InHand ? "in hand" : "holstered");
        yield return ("Muzzle world position", t => t.Sidearm == null ? "-" : $"({t.Weapon.Muzzle.x:0.00}, {t.Weapon.Muzzle.y:0.00})");
    }

    // The current roll's momentum, or the last roll's after it ended: how it was launched, how it decays, what it handed on.
    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> RollReadouts() {
        yield return ("Phase", t => t.Roll.IsRolling ? t.Roll.Phase.ToString().ToUpperInvariant() : "not rolling (last roll shown)");
        yield return ("Entry speed u_in / run share", t => $"{t.Roll.Launch.Incoming:0.00} u/s / {t.Roll.Launch.RunShare:0.00}");
        yield return ("Inherited part", t => $"{t.Roll.Launch.Inherited:0.00} u/s");
        yield return ("Throw impulse → entry speed", t => $"{t.Roll.Launch.Throw:0.00} u/s → {t.Roll.Launch.Target:0.00} u/s");
        yield return ("Current speed", t => $"{t.Roll.Speed:0.00} u/s");
        yield return ("Current deceleration", t => t.Roll.IsRolling ? $"{t.Roll.Deceleration:0.00} u/s²{(t.Roll.Deceleration < 0f ? "  (throw)" : "")}" : "-");
        yield return ("Speed at recovery start", t => $"{t.Roll.RecoverySpeed:0.00} u/s");
        yield return ("Handed back / carried into walk", t => $"{t.Roll.HandedBack:0.00} / {t.CarriedSpeed:0.00} u/s");
    }

    // How the ground's slope bounds the gait (SlopeReach): the slope, the legs' extension, the reach behind and ahead of
    // the hip and the stride it allows (along the surface), where the last step aimed and landed, the pelvis and how far
    // the planted legs' reach sinks it, and how the gait recovered.
    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> SlopeGaitReadouts() {
        yield return ("Slope angle / normal / tangent", t => $"{t.SlopeGait.Angle:+0.0;-0.0}° / ({t.SlopeGait.Normal.x:0.00}, {t.SlopeGait.Normal.y:0.00}) / ({t.SlopeGait.Tangent.x:0.00}, {t.SlopeGait.Tangent.y:0.00})");
        yield return ("Support foot / extension", t => t.SlopeGait.SupportLeg == SupportStance.Leg.None ? "none" : $"{t.SlopeGait.SupportLeg.ToString().ToLowerInvariant()} / {ExtensionText(t.SlopeGait.SupportExtension)}");
        yield return ("Trailing-leg extension", t => ExtensionText(t.SlopeGait.TrailingExtension));
        yield return ("MaxLag / MaxLead", t => $"{ReachText(t.SlopeGait.Stance.MaxLag)} / {ReachText(t.SlopeGait.Stance.MaxLead)}");
        yield return ("Stride along surface: now / reach-bounded", t => $"{t.SlopeGait.StrideAlongSurface:0.00} / {(t.SlopeGait.Stance.IsBounded ? t.SlopeGait.BoundedStrideAlongSurface.ToString("0.00") + " u (bounded)" : "not bounded")}");
        yield return ("Planned foot X / foothold displacement", t => $"{t.SlopeGait.PlannedX:0.00} / {t.SlopeGait.FootholdDisplacementAlongSurface:+0.00;-0.00} u along the surface");
        yield return ("Pelvis target Y / reach-ceiling sink", t => $"{t.SlopeGait.PelvisTargetY:0.000} / {t.SlopeGait.CeilingSink:0.000} u");
        yield return ("Pelvis drop for reach", t => $"{t.SlopeGait.Stance.Drop:0.000} u");
        yield return ("Catch-up", t => $"{t.SlopeGait.CatchUp:0.00}×");
        yield return ("Balance steps / resyncs / extension releases", t => $"{t.SlopeGait.BalanceSteps} / {t.SlopeGait.PhaseResyncs} / {t.SlopeGait.ExtensionReleases}");
    }

    private static string ExtensionText(float extension) => float.IsNaN(extension) ? "-" : extension.ToString("0.000");

    private static string ReachText(float reach) => float.IsInfinity(reach) ? "-" : $"{reach:0.00} u";

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

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> LightingReadouts() {
        yield return ("Player light intensity", t => t.Lighting != null ? $"{t.PlayerLightIntensity:0.00}" : "no lighting rig");
        yield return ("Muzzle flash intensity", t => t.Lighting != null ? $"{t.MuzzleLightIntensity:0.00}" : "no lighting rig");
        yield return ("Sun intensity", t => t.Lighting != null ? $"{t.SunLightIntensity:0.00}{(t.Lighting.SunCastsShadows ? "  (casting shadows)" : "")}" : "no lighting rig");
        yield return ("Lamps / shadow casters", t => t.Lighting != null ? $"{t.LampCount} / {t.ShadowCasterCount}" : "no lighting rig");
    }

    private static IEnumerable<(string label, Func<LocomotionTelemetry, string> format)> LandingReadouts() {
        yield return ("Reaching for the ground", t => t.Air.IsAirborne ? $"{t.Air.LandingPrep:0.00}" : "-");
        yield return ("Predicted ground", t => t.Air.IsAirborne && t.Air.HasPredictedGround ? $"{t.Air.PredictedGround.x:0.00}, {t.Air.PredictedGround.y:0.00}" : "-");
        yield return ("Last impact speed", t => $"{t.Air.LastImpactSpeed:0.0} u/s");
        yield return ("Landing compression held", t => $"{t.LandingCompression:0.000} u");
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
        AddFlag((row, "EXTENSION RELEASE", t => t.ExtensionReleases));
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
