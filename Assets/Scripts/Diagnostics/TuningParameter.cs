using System;

// One tunable value of a preset as the tuning panel shows it: what it is called, its unit and a sensible range, and
// how to read and write it.
public sealed class TuningParameter<TPreset> {
    private readonly Func<TPreset, float> read;
    private readonly Action<TPreset, float> write;

    public TuningParameter((string group, string label, string unit) display, (float min, float max) range, (Func<TPreset, float> read, Action<TPreset, float> write) access) {
        Group = display.group;
        Label = display.label;
        Unit = display.unit;
        Min = range.min;
        Max = range.max;
        read = access.read;
        write = access.write;
    }

    // An on/off switch, kept as the number 0 (off) or 1 (on) so that everything that reads and writes a value (resets,
    // presets, the tests) treats it like any other parameter; the panel shows it as a checkbox. Any value from 0.5 up is on.
    public static TuningParameter<TPreset> Toggle((string group, string label) display, (Func<TPreset, bool> read, Action<TPreset, bool> write) access) {
        return new TuningParameter<TPreset>((display.group, display.label, ToggleUnit), (0f, 1f), (
            preset => access.read(preset) ? 1f : 0f,
            (preset, value) => access.write(preset, value >= ToggleThreshold))) { IsToggle = true };
    }

    public const string ToggleUnit = "on / off";
    public const float ToggleThreshold = 0.5f;

    // Whether the parameter is a switch (shown as a checkbox) rather than a number (a slider and a field).
    public bool IsToggle { get; private set; }

    // Heading the parameter is listed under within its section.
    public string Group { get; }
    public string Label { get; }
    public string Unit { get; }
    public float Min { get; }
    public float Max { get; }

    public float ReadFrom(TPreset preset) => read(preset);

    public void WriteTo(TPreset preset, float value) => write(preset, value);
}
