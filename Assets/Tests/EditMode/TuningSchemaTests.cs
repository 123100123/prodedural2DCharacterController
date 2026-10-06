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
        Assert.That(titles, Is.EqualTo(new[] { "IDLE", "WALK", "RUN", "CROUCH", "JUMP", "FALL", "WALL", "SLIDE", "ROLL", "LEDGE", "CLIMB", "PUSH", "AIM", "CAMERA", "EFFECTS", "AUDIO", "TRANSITIONS", "ADVANCED" }));
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
            float changed = entry.parameter.IsToggle ? 1f - entry.parameter.ReadFrom(preset) : (entry.parameter.Min + entry.parameter.Max) / 2f + 0.0123f;
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

    [Test]
    public void EverySectionHasATabPlacedInTheLayoutsOrder() {
        foreach (TuningSection<MovementPreset> section in MovementTuningSchema.Build()) {
            Assert.That(DebugPanelLayout.TabTitles, Does.Contain(section.Title), $"{section.Title} would be pushed behind the DEBUG tab");
        }
        Assert.That(DebugPanelLayout.TabTitles, Is.Unique);
    }

    [Test]
    public void ThePanelTunesRollAndClimbAndTheirSwitches() {
        TuningSection<MovementPreset> roll = MovementTuningSchema.Build().Single(section => section.Title == "ROLL");
        TuningSection<MovementPreset> climb = MovementTuningSchema.Build().Single(section => section.Title == "CLIMB");
        Assert.That(roll.Parameters.Count(parameter => parameter.Group.StartsWith("1.") || parameter.Group.StartsWith("2.") || parameter.Group.StartsWith("3.") || parameter.Group.StartsWith("4.")), Is.GreaterThan(20), "a row per phase value");
        Assert.That(roll.Parameters.Single(parameter => parameter.IsToggle).Label, Is.EqualTo("Roll"));
        Assert.That(climb.Parameters.Select(parameter => parameter.Group).Distinct(), Is.EquivalentTo(new[] { "Detection", "Body", "Limbs", "Timing" }));
        Assert.That(climb.Parameters.Any(parameter => parameter.IsToggle), Is.False);
    }

    // The switches the panel offers, by tab: each reads and writes the preset's flag, with 0.5 and up meaning on.
    [Test]
    public void ASwitchReadsAndWritesItsFlag() {
        string[] expected = { "ROLL", "WALL", "SLIDE", "LEDGE", "PUSH", "CAMERA", "EFFECTS" };
        string[] offered = AllParameters().Where(entry => entry.parameter.IsToggle).Select(entry => entry.section).ToArray();
        Assert.That(offered, Is.EquivalentTo(expected));

        TuningParameter<MovementPreset> rollSwitch = AllParameters().Single(entry => entry.section == "ROLL" && entry.parameter.IsToggle).parameter;
        MovementPreset preset = Defaults();
        Assert.That(preset.roll.enabled, Is.True, "on in the code defaults");
        Assert.That(rollSwitch.ReadFrom(preset), Is.EqualTo(1f));
        rollSwitch.WriteTo(preset, 0f);
        Assert.That(preset.roll.enabled, Is.False);
        rollSwitch.WriteTo(preset, 1f);
        Assert.That(preset.roll.enabled, Is.True);
        Assert.That(rollSwitch.ReadFrom(preset), Is.EqualTo(1f));
        rollSwitch.WriteTo(preset, 0.49f);
        Assert.That(preset.roll.enabled, Is.False, "below half is off");
        rollSwitch.WriteTo(preset, 0.5f);
        Assert.That(preset.roll.enabled, Is.True, "half and up is on");
        Assert.That(rollSwitch.Min, Is.EqualTo(0f));
        Assert.That(rollSwitch.Max, Is.EqualTo(1f));
    }

    [Test]
    public void ANumberParameterIsNotASwitch() {
        Assert.That(AllParameters().Count(entry => !entry.parameter.IsToggle), Is.GreaterThan(400));
        Assert.That(AllParameters().Single(entry => entry.section == "ROLL" && entry.parameter.Label == "Cooldown after a roll").parameter.IsToggle, Is.False);
    }
}
