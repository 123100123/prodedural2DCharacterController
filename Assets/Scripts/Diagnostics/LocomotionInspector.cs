using System;
using UnityEngine;

// The debug tools' view of one character: live telemetry, the tunable movement settings, and the gait preview
// override. The character never depends on it; nothing in the gait reads it back.
public sealed class LocomotionInspector {
    private readonly StrideController stride;
    private readonly Func<PlayerCommands> latestCommands;

    public LocomotionInspector(CharacterDriver character) {
        CharacterParts parts = character.Parts;
        stride = parts.Stride;
        latestCommands = () => character.LatestCommands;
        Tuning = new MovementTuning(parts);
        Telemetry = new LocomotionTelemetry(stride, (parts.Posture, parts.Torso, parts.Head, parts.PostureRoot));
        Movement = MovementTuningSchema.CreateDomain(Tuning);
    }

    public MovementTuning Tuning { get; }
    public TuningDomain<MovementPreset> Movement { get; }
    // The movement settings the character started the session with.
    public MovementPreset Defaults => Movement.Defaults;
    public LocomotionTelemetry Telemetry { get; }

    public GaitPreview Preview { get => stride.Preview; set => stride.Preview = value; }

    public LocomotionTelemetry ReadTelemetry() {
        Telemetry.Refresh((latestCommands(), Time.time));
        return Telemetry;
    }
}
