using System;

// One tunable value of a LocomotionPreset as the tuning panel shows it: what it is called, its unit and a sensible
// range, and how to read and write it.
public sealed class TuningParameter {
    private readonly Func<LocomotionPreset, float> read;
    private readonly Action<LocomotionPreset, float> write;

    public TuningParameter((string group, string label, string unit) display, (float min, float max) range, (Func<LocomotionPreset, float> read, Action<LocomotionPreset, float> write) access) {
        Group = display.group;
        Label = display.label;
        Unit = display.unit;
        Min = range.min;
        Max = range.max;
        read = access.read;
        write = access.write;
    }

    // Heading the parameter is listed under within its section.
    public string Group { get; }
    public string Label { get; }
    public string Unit { get; }
    public float Min { get; }
    public float Max { get; }

    public float ReadFrom(LocomotionPreset preset) => read(preset);

    public void WriteTo(LocomotionPreset preset, float value) => write(preset, value);
}
