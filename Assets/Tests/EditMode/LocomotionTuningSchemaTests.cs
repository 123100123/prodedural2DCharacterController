using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class LocomotionTuningSchemaTests {
    private static LocomotionPreset Defaults() {
        return new LocomotionPreset {
            walk = GaitProfile.Walk,
            run = GaitProfile.Run,
            transitions = GaitTransitionSettings.Default,
            balance = BalanceSettings.Default,
            idle = IdleStanceSettings.Default,
            bodyHeight = 2.58f,
            body = BodyMassSettings.Default,
            posture = PostureSettings.Default,
            chestSpring = new SpringSettings(3f, 0.8f, 0f),
            breathing = BreathingSettings.Default,
            head = HeadMotionSettings.Default,
            arms = ArmSwingSettings.Default,
        };
    }

    private static IEnumerable<(string section, TuningParameter parameter)> AllParameters() {
        return LocomotionTuningSchema.Build().SelectMany(section => section.Parameters.Select(parameter => (section.Title, parameter)));
    }

    [Test]
    public void OffersTheTabsInOrder() {
        string[] titles = LocomotionTuningSchema.Build().Select(section => section.Title).ToArray();
        Assert.That(titles, Is.EqualTo(new[] { "IDLE", "WALK", "RUN", "TRANSITIONS", "BODY", "UPPER BODY" }));
    }

    [Test]
    public void EveryDefaultFitsItsSliderRange() {
        LocomotionPreset defaults = Defaults();
        foreach ((string section, TuningParameter parameter) entry in AllParameters()) {
            float value = entry.parameter.ReadFrom(defaults);
            Assert.That(value, Is.InRange(entry.parameter.Min, entry.parameter.Max), $"{entry.section} / {entry.parameter.Label}");
            Assert.That(entry.parameter.Unit, Is.Not.Empty, $"{entry.section} / {entry.parameter.Label} has a unit");
        }
    }

    // Writing a value changes exactly what the parameter reads back, and leaves everything else in the preset alone
    // (unless another row shows the same setting, e.g. the base lean under both RUN and UPPER BODY).
    [Test]
    public void EachParameterWritesOnlyItsOwnValue() {
        foreach ((string section, TuningParameter parameter) entry in AllParameters()) {
            LocomotionPreset preset = Defaults();
            float changed = (entry.parameter.Min + entry.parameter.Max) / 2f + 0.0123f;
            entry.parameter.WriteTo(preset, changed);
            Assert.That(entry.parameter.ReadFrom(preset), Is.EqualTo(changed).Within(0.0001f), $"{entry.section} / {entry.parameter.Label}");

            LocomotionPreset untouched = Defaults();
            int differences = AllParameters().Count(other => !UnityEngine.Mathf.Approximately(other.parameter.ReadFrom(preset), other.parameter.ReadFrom(untouched)));
            int sameSetting = AllParameters().Count(other => UnityEngine.Mathf.Approximately(other.parameter.ReadFrom(preset), changed));
            Assert.That(differences, Is.EqualTo(sameSetting), $"{entry.section} / {entry.parameter.Label} changed something else");
        }
    }
}
