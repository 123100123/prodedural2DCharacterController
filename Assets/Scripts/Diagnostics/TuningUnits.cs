using System;
using System.Collections.Generic;

// Unit labels and the parameter groups every tuning schema shares.
public static class TuningUnits {
    public const string Units = "u";
    public const string UnitsPerSecond = "u/s";
    public const string UnitsPerSecondSquared = "u/s²";
    public const string Seconds = "s";
    public const string Hertz = "Hz";
    public const string Degrees = "°";
    public const string Share = "share";
    public const string Multiple = "×";

    // A spring's frequency, damping and response; a spring whose frequency another tab already owns omits it.
    public static IEnumerable<TuningParameter<TPreset>> Spring<TPreset>((string group, (Func<TPreset, SpringSettings> read, Action<TPreset, SpringSettings> write) access) spring, bool includeFrequency = true) {
        if (includeFrequency) {
            yield return new TuningParameter<TPreset>((spring.group, "Frequency", Hertz), (0.2f, 12f), (
                p => spring.access.read(p).frequency,
                (p, v) => spring.access.write(p, new SpringSettings(v, spring.access.read(p).damping, spring.access.read(p).response))));
        }
        yield return new TuningParameter<TPreset>((spring.group, "Damping", "ratio"), (0.05f, 2f), (
            p => spring.access.read(p).damping,
            (p, v) => spring.access.write(p, new SpringSettings(spring.access.read(p).frequency, v, spring.access.read(p).response))));
        yield return new TuningParameter<TPreset>((spring.group, "Response", Multiple), (-2f, 3f), (
            p => spring.access.read(p).response,
            (p, v) => spring.access.write(p, new SpringSettings(spring.access.read(p).frequency, spring.access.read(p).damping, v))));
    }
}
