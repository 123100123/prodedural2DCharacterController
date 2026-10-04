using NUnit.Framework;
using UnityEngine;

// The pure relations of the effects: how big a puff is for an impact, when nothing is raised, how a casing fades, which
// pooled casing is reused, and that the effects settings travel through presets.
public class EffectsTests {
    private static readonly EffectsSettings Settings = new EffectsSettings();

    private static float FootShare(float impactSpeed) => EffectMath.ImpactShare((impactSpeed, Settings.footDustMinImpact, Settings.footDustFullImpact));

    [Test]
    public void AnImpactAtOrBelowTheThresholdRaisesNothing() {
        Assert.That(FootShare(0f), Is.EqualTo(0f));
        Assert.That(FootShare(Settings.footDustMinImpact), Is.EqualTo(0f));
        Assert.That(EffectMath.PuffCount((FootShare(Settings.footDustMinImpact), 1f), 3), Is.Zero);
    }

    [Test]
    public void TheShareRisesWithTheImpactAndStopsAtOne() {
        float light = FootShare(Settings.footDustMinImpact + 1f);
        float firm = FootShare((Settings.footDustMinImpact + Settings.footDustFullImpact) / 2f);
        Assert.That(light, Is.GreaterThan(0f).And.LessThan(firm));
        Assert.That(firm, Is.LessThan(1f));
        Assert.That(FootShare(Settings.footDustFullImpact), Is.EqualTo(1f));
        Assert.That(FootShare(100f), Is.EqualTo(1f));
    }

    [Test]
    public void AGentleImpactIsOnePuffAndAFullOneIsTheMostAtItsBiggest() {
        Assert.That(EffectMath.PuffCount((0.01f, 1f), 14), Is.EqualTo(1));
        Assert.That(EffectMath.PuffCount((1f, 1f), 14), Is.EqualTo(14));
        Assert.That(EffectMath.PuffCount((1f, 2f), 14), Is.EqualTo(28), "the scale multiplies the count");
        Assert.That(EffectMath.PuffSize(1f, 1f), Is.GreaterThan(EffectMath.PuffSize(0f, 1f)));
    }

    [Test]
    public void AScaleOfZeroSwitchesTheEffectOff() {
        Assert.That(EffectMath.PuffCount((1f, 0f), 14), Is.Zero);
        Assert.That(EffectMath.ScaledCount(6f, 0f), Is.Zero);
        Assert.That(EffectMath.ScaledCount(0f, 1f), Is.Zero);
        Assert.That(EffectMath.PuffSize(1f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void AFixedCountScalesAndNeverRoundsDownToNothing() {
        Assert.That(EffectMath.ScaledCount(6f, 1f), Is.EqualTo(6));
        Assert.That(EffectMath.ScaledCount(6f, 0.5f), Is.EqualTo(3));
        Assert.That(EffectMath.ScaledCount(1f, 0.1f), Is.EqualTo(1));
    }

    [Test]
    public void ACasingLiesOpaqueThenFadesOverTheFadeTimeAndIsGone() {
        Assert.That(EffectMath.CasingAlpha(0f, 4f, 0.8f), Is.EqualTo(1f));
        Assert.That(EffectMath.CasingAlpha(3.2f, 4f, 0.8f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(EffectMath.CasingAlpha(3.6f, 4f, 0.8f), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(EffectMath.CasingAlpha(4f, 4f, 0.8f), Is.EqualTo(0f));
        Assert.That(EffectMath.CasingAlpha(9f, 4f, 0.8f), Is.EqualTo(0f));
    }

    [Test]
    public void ACasingIsThrownToTheUpSideOfTheAim() {
        Assert.That(EffectMath.UpwardPerpendicular(Vector2.right).y, Is.GreaterThan(0.99f));
        Assert.That(EffectMath.UpwardPerpendicular(Vector2.left).y, Is.GreaterThan(0.99f));
        Assert.That(EffectMath.UpwardPerpendicular(new Vector2(1f, -1f).normalized).y, Is.GreaterThan(0f));
    }

    [Test]
    public void ThePoolReusesAFreeSlotFirstAndTheOldestCasingWhenFull() {
        bool[] active = { true, false, true };
        float[] ages = { 1f, 0f, 2f };
        Assert.That(CasingPool.PickSlot(active, ages), Is.EqualTo(1), "the free slot");

        active[1] = true;
        ages[1] = 0.5f;
        Assert.That(CasingPool.PickSlot(active, ages), Is.EqualTo(2), "all busy: the one that has lain longest");
    }

    [Test]
    public void TheEffectsSettingsSurviveAPresetRoundTripAndCopy() {
        MovementPreset preset = MovementPreset.CodeDefaults();
        preset.effects.dustScale = 1.7f;
        preset.effects.casingLifetime = 9f;

        MovementPreset loaded = MovementPreset.FromJson(preset.ToJson());
        Assert.That(loaded.effects.dustScale, Is.EqualTo(1.7f));
        Assert.That(loaded.effects.casingLifetime, Is.EqualTo(9f));

        MovementPreset copy = preset.Copy();
        copy.effects.dustScale = 0f;
        Assert.That(preset.effects.dustScale, Is.EqualTo(1.7f), "the copy owns its effects settings");

        EffectsSettings target = new EffectsSettings();
        target.CopyFrom(preset.effects);
        Assert.That(target.dustScale, Is.EqualTo(1.7f));
        Assert.That(target.casingLifetime, Is.EqualTo(9f));
    }

    // A preset saved at format version 4, before the effects existed, loads with the effects at their defaults.
    [Test]
    public void APresetSavedBeforeTheEffectsExistedKeepsTheEffectDefaults() {
        MovementPreset loaded = MovementPreset.FromJson("{\"version\":4,\"run\":{\"lean\":21}}");
        EffectsSettings defaults = new EffectsSettings();

        Assert.That(MovementPreset.CurrentVersion, Is.EqualTo(5));
        Assert.That(loaded.run.lean, Is.EqualTo(21f));
        Assert.That(loaded.effects, Is.Not.Null);
        Assert.That(loaded.effects.enabled, Is.True);
        Assert.That(loaded.effects.dustScale, Is.EqualTo(defaults.dustScale));
        Assert.That(loaded.effects.casingLifetime, Is.EqualTo(defaults.casingLifetime));
    }
}
