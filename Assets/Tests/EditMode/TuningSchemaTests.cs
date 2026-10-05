using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class TuningSchemaTests {
    private static MovementPreset Defaults() => MovementPreset.CodeDefaults();

    private static IEnumerable<(string section, TuningParameter<MovementPreset> parameter)> AllParameters() {
        return MovementTuningSchema.Build().SelectMany(section => section.Parameters.Select(parameter => (section.Title, parameter)));
    }

    [Test]
    public void OffersTheTabsInOrder() {
        string[] titles = MovementTuningSchema.Build().Select(section => section.Title).ToArray();
        Assert.That(titles, Is.EqualTo(new[] { "IDLE", "WALK", "RUN", "CROUCH", "JUMP", "FALL", "WALL", "SLIDE", "LEDGE", "PUSH", "AIM", "CAMERA", "EFFECTS", "AUDIO", "TRANSITIONS", "ADVANCED" }));
    }

    [Test]
    public void EveryDefaultFitsItsSliderRange() {
        MovementPreset defaults = Defaults();
        foreach ((string section, TuningParameter<MovementPreset> parameter) entry in AllParameters()) {
            float value = entry.parameter.ReadFrom(defaults);
            Assert.That(value, Is.InRange(entry.parameter.Min, entry.parameter.Max), $"{entry.section} / {entry.parameter.Label}");
            Assert.That(entry.parameter.Unit, Is.Not.Empty, $"{entry.section} / {entry.parameter.Label} has a unit");
        }
    }

    // Writing a value changes exactly what the parameter reads back and nothing else in the preset: every setting is
    // offered in one tab, owned by one parameter.
    [Test]
    public void EachParameterWritesOnlyItsOwnValue() {
        foreach ((string section, TuningParameter<MovementPreset> parameter) entry in AllParameters()) {
            MovementPreset preset = Defaults();
            float changed = (entry.parameter.Min + entry.parameter.Max) / 2f + 0.0123f;
            entry.parameter.WriteTo(preset, changed);
            Assert.That(entry.parameter.ReadFrom(preset), Is.EqualTo(changed).Within(0.0001f), $"{entry.section} / {entry.parameter.Label}");

            MovementPreset untouched = Defaults();
            int differences = AllParameters().Count(other => !UnityEngine.Mathf.Approximately(other.parameter.ReadFrom(preset), other.parameter.ReadFrom(untouched)));
            Assert.That(differences, Is.EqualTo(1), $"{entry.section} / {entry.parameter.Label} changed something else, or another row shows the same setting");
        }
    }

    [Test]
    public void EveryParameterHasAGroupAndALabel() {
        foreach ((string section, TuningParameter<MovementPreset> parameter) entry in AllParameters()) {
            Assert.That(entry.parameter.Group, Is.Not.Empty, $"{entry.section} / {entry.parameter.Label}");
            Assert.That(entry.parameter.Label, Is.Not.Empty, entry.section);
        }
    }
}
