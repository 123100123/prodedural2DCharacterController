using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class LightingTests {
    private static readonly LightingSettings Settings = new LightingSettings();

    [Test]
    public void AFlashIsDarkBeforeTheFirstShot() {
        FlashEnvelope flash = new FlashEnvelope();
        flash.Advance(1f);
        Assert.That(flash.IsActive(Settings), Is.False);
        Assert.That(flash.Level(Settings), Is.EqualTo(0f));
    }

    [Test]
    public void AFlashIsAtFullBrightnessTheMomentTheShotIsFired() {
        FlashEnvelope flash = new FlashEnvelope();
        flash.Trigger();
        Assert.That(flash.IsActive(Settings), Is.True);
        Assert.That(flash.Level(Settings), Is.EqualTo(1f));
    }

    [Test]
    public void TheFlashDiesAwayQuicklyAndMonotonicallyToNothingAtItsDuration() {
        FlashEnvelope flash = new FlashEnvelope();
        flash.Trigger();
        float previous = 1f;
        for (float time = 0f; time < Settings.muzzleDuration; time += 0.001f) {
            float level = FlashEnvelope.LevelAt(time, (Settings.muzzleDuration, Settings.muzzleDecayExponent));
            Assert.That(level, Is.LessThanOrEqualTo(previous + 0.0001f), $"monotone at {time}");
            Assert.That(level, Is.InRange(0f, 1f));
            previous = level;
        }
        flash.Advance(Settings.muzzleDuration);
        Assert.That(flash.IsActive(Settings), Is.False);
        Assert.That(flash.Level(Settings), Is.EqualTo(0f));
    }

    [Test]
    public void ASharperDecayDropsFasterThanAnEvenOne() {
        float even = FlashEnvelope.LevelAt(0.03f, (0.09f, 1f));
        float sharp = FlashEnvelope.LevelAt(0.03f, (0.09f, 4f));
        Assert.That(sharp, Is.LessThan(even));
        Assert.That(even, Is.EqualTo(1f - 0.03f / 0.09f).Within(0.0001f), "an exponent of one fades evenly");
    }

    [Test]
    public void ANewShotRestartsTheFlashAtFullBrightnessAndNeverAddsUp() {
        FlashEnvelope flash = new FlashEnvelope();
        flash.Trigger();
        flash.Advance(Settings.muzzleDuration * 0.6f);
        float fading = flash.Level(Settings);
        flash.Trigger();
        Assert.That(fading, Is.LessThan(1f));
        Assert.That(flash.Level(Settings), Is.EqualTo(1f), "back to the peak, not above it");

        float highest = 0f;
        for (int shot = 0; shot < 200; shot++) {
            flash.Trigger();
            flash.Advance(0.02f);
            highest = Mathf.Max(highest, flash.Level(Settings));
        }
        Assert.That(highest, Is.LessThanOrEqualTo(1f), "rapid fire never builds into a glare");
    }

    [Test]
    public void AFlashAgesTheSameInOneBigStepOrManySmallOnes() {
        FlashEnvelope big = new FlashEnvelope();
        FlashEnvelope small = new FlashEnvelope();
        big.Trigger();
        small.Trigger();
        big.Advance(0.05f);
        for (int step = 0; step < 50; step++) {
            small.Advance(0.001f);
        }
        Assert.That(small.Level(Settings), Is.EqualTo(big.Level(Settings)).Within(0.001f));
    }

    [Test]
    public void AFlashAfterTheLongestPauseIsStillDarkAndRestartsCleanly() {
        FlashEnvelope flash = new FlashEnvelope();
        flash.Trigger();
        flash.Advance(1000f);
        Assert.That(flash.IsActive(Settings), Is.False);
        flash.Trigger();
        Assert.That(flash.Level(Settings), Is.EqualTo(1f));
    }

    [Test]
    public void TheDefaultAmbientLightKeepsTheSceneReadable() {
        Assert.That(Settings.ambientIntensity, Is.GreaterThanOrEqualTo(0.55f), "never darker than about 55 %");
        Assert.That(LightingTuningSchema.Build()[0].Parameters.First(p => p.Label == "Intensity" && p.Group == "Ambient light").Min, Is.GreaterThanOrEqualTo(0.2f));
    }

    [Test]
    public void TheMuzzleFlashIsWarmAndOrange() {
        Color color = Settings.muzzleColor;
        Assert.That(color.r, Is.GreaterThan(color.g));
        Assert.That(color.g, Is.GreaterThan(color.b));
        Assert.That(Settings.muzzleDuration, Is.InRange(0.04f, 0.2f), "a flash, not a glow");
    }

    [Test]
    public void ThePresetSurvivesACopyAndCopyFromCopiesEveryField() {
        LightingPreset preset = LightingPreset.CodeDefaults();
        preset.lighting.ambientIntensity = 0.9f;
        LightingPreset copy = preset.Copy();
        copy.lighting.ambientIntensity = 0.1f;
        Assert.That(preset.lighting.ambientIntensity, Is.EqualTo(0.9f), "a copy is independent");

        LightingSettings target = new LightingSettings();
        LightingSettings source = new LightingSettings();
        foreach (System.Reflection.FieldInfo field in typeof(LightingSettings).GetFields()) {
            if (field.FieldType == typeof(float)) {
                field.SetValue(source, (float)field.GetValue(source) + 0.37f);
            }
            else if (field.FieldType == typeof(bool)) {
                field.SetValue(source, !(bool)field.GetValue(source));
            }
            else if (field.FieldType == typeof(Color)) {
                field.SetValue(source, new Color(0.1f, 0.2f, 0.3f, 1f));
            }
            else if (field.FieldType == typeof(Vector2)) {
                field.SetValue(source, (Vector2)field.GetValue(source) + new Vector2(3f, 2f));
            }
        }
        target.CopyFrom(source);
        foreach (System.Reflection.FieldInfo field in typeof(LightingSettings).GetFields()) {
            Assert.That(field.GetValue(target), Is.EqualTo(field.GetValue(source)), field.Name);
        }
    }

    private static IEnumerable<TuningParameter<LightingPreset>> Parameters => LightingTuningSchema.Build().SelectMany(section => section.Parameters);

    [Test]
    public void TheLightingTabIsOfferedByItself() {
        Assert.That(LightingTuningSchema.Build().Select(section => section.Title), Is.EqualTo(new[] { "LIGHTING" }));
    }

    [Test]
    public void EveryLightingDefaultFitsItsSliderRangeAndEachParameterWritesOnlyItself() {
        LightingPreset defaults = LightingPreset.CodeDefaults();
        foreach (TuningParameter<LightingPreset> parameter in Parameters) {
            float value = parameter.ReadFrom(defaults);
            Assert.That(value, Is.InRange(parameter.Min, parameter.Max), $"{parameter.Group} / {parameter.Label}");
            Assert.That(parameter.Unit, Is.Not.Empty);

            LightingPreset edited = LightingPreset.CodeDefaults();
            bool isSwitch = parameter.Unit == "0 off, 1 on";
            float changed = isSwitch ? 1f - value : (parameter.Min + parameter.Max) / 2f + 0.0123f;
            parameter.WriteTo(edited, changed);
            Assert.That(parameter.ReadFrom(edited), Is.EqualTo(changed).Within(0.0001f), $"{parameter.Group} / {parameter.Label}");

            int differences = Parameters.Count(other => !Mathf.Approximately(other.ReadFrom(edited), other.ReadFrom(defaults)));
            Assert.That(differences, Is.EqualTo(1), $"{parameter.Group} / {parameter.Label} changed something else");
        }
    }

    [Test]
    public void EachColourHasRedGreenAndBlueSliders() {
        string[] groups = { "Ambient light", "Player light", "Muzzle flash" };
        foreach (string group in groups) {
            Assert.That(Parameters.Count(p => p.Group == group && p.Label.EndsWith("red")), Is.EqualTo(1), group);
            Assert.That(Parameters.Count(p => p.Group == group && p.Label.EndsWith("green")), Is.EqualTo(1), group);
            Assert.That(Parameters.Count(p => p.Group == group && p.Label.EndsWith("blue")), Is.EqualTo(1), group);
        }
    }
}
