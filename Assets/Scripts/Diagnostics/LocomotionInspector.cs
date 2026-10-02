using System;
using UnityEngine;

// The debug tools' view of one character's locomotion: live telemetry, the tunable settings, and the gait preview
// override. The locomotion never depends on it; nothing in the gait reads it back.
public sealed class LocomotionInspector {
    private readonly StrideController stride;
    private readonly Func<PlayerCommands> latestCommands;

    public LocomotionInspector((StrideController stride, LocomotionTuning tuning, LocomotionTelemetry telemetry, Func<PlayerCommands> latestCommands) parts) {
        stride = parts.stride;
        Tuning = parts.tuning;
        Telemetry = parts.telemetry;
        latestCommands = parts.latestCommands;
        Defaults = Tuning.Capture();
    }

    public LocomotionTuning Tuning { get; }
    // The settings the character started the session with.
    public LocomotionPreset Defaults { get; }
    public LocomotionTelemetry Telemetry { get; }

    public GaitPreview Preview { get => stride.Preview; set => stride.Preview = value; }

    public LocomotionTelemetry ReadTelemetry() {
        Telemetry.Refresh((latestCommands(), Time.time));
        return Telemetry;
    }
}
