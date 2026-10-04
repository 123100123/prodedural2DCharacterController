using System;
using UnityEngine;

// The debug tools' view of one character: live telemetry, the tunable movement settings, and the gait preview
// override. The character never depends on it; nothing in the gait reads it back.
public sealed class LocomotionInspector {
    private readonly StrideController stride;
    private readonly Func<PlayerCommands> latestCommands;

    // lighting: the scene's lighting rig, if it has one (the lighting tab and readout appear only then).
    public LocomotionInspector(CharacterDriver character, ILightingTarget lighting = null) {
        CharacterParts parts = character.Parts;
        stride = parts.Stride;
        latestCommands = () => character.LatestCommands;
        Tuning = new MovementTuning(parts);
        Telemetry = new LocomotionTelemetry(stride, (parts.Posture, parts.Torso, parts.Head, parts.PostureRoot), (parts.Crouch, parts.AirborneState));
        Movement = MovementTuningSchema.CreateDomain(Tuning);
        if (lighting != null) {
            Lighting = LightingTuningSchema.CreateDomain(lighting);
            Telemetry.Lighting = lighting;
        }
    }

    public MovementTuning Tuning { get; }
    public TuningDomain<MovementPreset> Movement { get; }
    // The lighting settings and presets; null in a scene with no lighting rig.
    public TuningDomain<LightingPreset> Lighting { get; }
    // The movement settings the character started the session with.
    public MovementPreset Defaults => Movement.Defaults;
    public LocomotionTelemetry Telemetry { get; }

    public GaitPreview Preview { get => stride.Preview; set => stride.Preview = value; }

    public LocomotionTelemetry ReadTelemetry() {
        Telemetry.Refresh((latestCommands(), Time.time));
        return Telemetry;
    }
}
