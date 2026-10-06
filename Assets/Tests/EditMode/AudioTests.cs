using System;
using NUnit.Framework;
using UnityEngine;

// The synthesized sounds and the audio mapping, without Unity audio: the buffers are the right length, in range,
// repeatable and decaying, the volumes follow the impact thresholds, the voice pool steals the oldest, the reload schedules
// the magazine near its end, and the audio settings travel through presets.
public class AudioTests {
    private const int Rate = ClipSynth.DefaultSampleRate;
    private static readonly CharacterAudioSettings Settings = new CharacterAudioSettings();

    private static SoundId[] Synthesized() {
        return new[] { SoundId.Footstep, SoundId.Landing, SoundId.Jump, SoundId.Roll, SoundId.Gunshot, SoundId.DryClick, SoundId.MagazineOut, SoundId.MagazineIn, SoundId.Casing, SoundId.TargetHit, SoundId.WoodKnock, SoundId.WoodBreak };
    }

    // ---- Synthesis ------------------------------------------------------------------------------------------

    [Test]
    public void EveryBufferHasItsLengthAndStaysInRangeWithoutNaN() {
        (SoundId id, float seconds)[] lengths = {
            (SoundId.Footstep, 0.1f), (SoundId.Landing, 0.4f), (SoundId.Jump, 0.26f), (SoundId.Roll, 0.5f), (SoundId.Gunshot, 0.75f),
            (SoundId.DryClick, 0.07f), (SoundId.MagazineOut, 0.12f), (SoundId.MagazineIn, 0.2f), (SoundId.Casing, 0.3f), (SoundId.TargetHit, 0.22f),
            (SoundId.WoodKnock, 0.2f), (SoundId.WoodBreak, 0.7f),
        };
        foreach ((SoundId id, float seconds) entry in lengths) {
            for (int variant = 0; variant < ClipSynth.VariantCount(entry.id); variant++) {
                float[] samples = ClipSynth.Build(entry.id, variant, Rate);
                Assert.That(samples.Length, Is.EqualTo(Mathf.RoundToInt(entry.seconds * Rate)), $"{entry.id} {variant} length");
                float peak = 0f;
                foreach (float sample in samples) {
                    Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False, $"{entry.id} {variant} has a bad sample");
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                Assert.That(peak, Is.LessThanOrEqualTo(1f), $"{entry.id} {variant} peak");
                Assert.That(peak, Is.GreaterThan(0.5f), $"{entry.id} {variant} is audible");
                Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.01f), $"{entry.id} {variant} ends at silence");
            }
        }
    }

    [Test]
    public void TheSameRecipeGivesTheSameSamplesEveryTime() {
        foreach (SoundId id in Synthesized()) {
            for (int variant = 0; variant < ClipSynth.VariantCount(id); variant++) {
                Assert.That(ClipSynth.Build(id, variant, Rate), Is.EqualTo(ClipSynth.Build(id, variant, Rate)), $"{id} {variant}");
            }
        }
    }

    [Test]
    public void TheFootstepVariantsDifferFromEachOther() {
        Assert.That(ClipSynth.VariantCount(SoundId.Footstep), Is.GreaterThanOrEqualTo(3).And.LessThanOrEqualTo(4));
        Assert.That(ClipSynth.Footstep(0, Rate), Is.Not.EqualTo(ClipSynth.Footstep(1, Rate)));
    }

    [Test]
    public void EveryTailIsQuieterThanItsHead() {
        foreach (SoundId id in Synthesized()) {
            float[] samples = ClipSynth.Build(id, 0, Rate);
            int quarter = samples.Length / 4;
            float head = Dsp.Rms(samples, 0, quarter);
            float tail = Dsp.Rms(samples, samples.Length - quarter, quarter);
            Assert.That(tail, Is.LessThan(head * 0.5f), $"{id}: tail {tail:F4}, head {head:F4}");
        }
    }

    [Test]
    public void ARicochetPlaysTheCasingBuffersAndNeedsNoneOfItsOwn() {
        Assert.That(ClipSynth.SourceOf(SoundId.Ricochet), Is.EqualTo(SoundId.Casing));
        Assert.That(ClipSynth.VariantCount(SoundId.Ricochet), Is.Zero);
    }

    [Test]
    public void SynthesisOfEverythingIsFast() {
        ClipSynth.Build(SoundId.Gunshot, 0, Rate);
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (SoundId id in Synthesized()) {
            for (int variant = 0; variant < ClipSynth.VariantCount(id); variant++) {
                ClipSynth.Build(id, variant, Rate);
            }
        }
        Assert.That(watch.ElapsedMilliseconds, Is.LessThan(250), "a few ms normally; the bound is generous for a busy test machine");
    }

    [Test]
    public void NoiseIsSeededAndSpansTheRange() {
        Dsp.Noise a = new Dsp.Noise(7);
        Dsp.Noise b = new Dsp.Noise(7);
        Dsp.Noise other = new Dsp.Noise(8);
        float low = 0f;
        float high = 0f;
        bool differs = false;
        for (int index = 0; index < 4000; index++) {
            float sample = a.Next();
            Assert.That(sample, Is.EqualTo(b.Next()));
            differs |= sample != other.Next();
            low = Mathf.Min(low, sample);
            high = Mathf.Max(high, sample);
        }
        Assert.That(differs, Is.True);
        Assert.That(low, Is.LessThan(-0.9f));
        Assert.That(high, Is.GreaterThan(0.9f));
    }

    [Test]
    public void ALowPassKeepsABassToneAndAHighPassDropsIt() {
        const float tone = 100f;
        Dsp.LowPass low = new Dsp.LowPass(2000f, Rate);
        Dsp.HighPass high = new Dsp.HighPass(2000f, Rate);
        float[] throughLow = new float[Rate / 2];
        float[] throughHigh = new float[Rate / 2];
        for (int index = 0; index < throughLow.Length; index++) {
            float input = Mathf.Sin(2f * Mathf.PI * tone * index / Rate);
            throughLow[index] = low.Process(input);
            throughHigh[index] = high.Process(input);
        }
        Assert.That(Dsp.Rms(throughLow, 1000, 10000), Is.GreaterThan(0.6f));
        Assert.That(Dsp.Rms(throughHigh, 1000, 10000), Is.LessThan(0.1f));
    }

    // ---- Mapping --------------------------------------------------------------------------------------------

    [Test]
    public void ASlowFootPlantMakesNoSoundAndAFirmerOneIsLouder() {
        Assert.That(AudioMath.FootstepLevel(0f, Settings), Is.Zero);
        Assert.That(AudioMath.FootstepLevel(Settings.footMinImpact, Settings), Is.Zero, "at the threshold is still silent");
        float soft = AudioMath.FootstepLevel(Settings.footMinImpact + 0.5f, Settings);
        float firm = AudioMath.FootstepLevel((Settings.footMinImpact + Settings.footFullImpact) / 2f, Settings);
        Assert.That(soft, Is.GreaterThan(0f).And.LessThan(firm));
        Assert.That(firm, Is.LessThan(1f));
        Assert.That(AudioMath.FootstepLevel(Settings.footFullImpact, Settings), Is.EqualTo(1f));
        Assert.That(AudioMath.FootstepLevel(99f, Settings), Is.EqualTo(1f));
    }

    [Test]
    public void ALandingIsLouderAndDeeperTheHarderItIs() {
        Assert.That(AudioMath.LandingLevel(Settings.landingMinImpact, Settings), Is.Zero);
        float gentle = AudioMath.LandingLevel(Settings.landingMinImpact + 1f, Settings);
        float hard = AudioMath.LandingLevel(Settings.landingFullImpact, Settings);
        Assert.That(gentle, Is.GreaterThan(0f).And.LessThan(hard));
        Assert.That(hard, Is.EqualTo(1f));
        Assert.That(AudioMath.LandingPitch(Settings.landingFullImpact, Settings), Is.LessThan(AudioMath.LandingPitch(Settings.landingMinImpact + 1f, Settings)));
        Assert.That(AudioMath.LandingPitch(0f, Settings), Is.EqualTo(AudioMath.LandingPitch(Settings.landingMinImpact, Settings)), "the lightest landing is the highest pitch");
    }

    [Test]
    public void TheMasterVolumeAndEachCategoryScaleTheGainAndMuteSilencesAll() {
        CharacterAudioSettings mix = new CharacterAudioSettings { masterVolume = 0.5f, weaponVolume = 0.4f, stepsVolume = 1f };
        Assert.That(AudioMath.Gain(SoundCategory.Weapon, mix), Is.EqualTo(0.2f).Within(1e-5f));
        Assert.That(AudioMath.Gain(SoundCategory.Steps, mix), Is.EqualTo(0.5f).Within(1e-5f));
        mix.masterVolume = 0f;
        Assert.That(AudioMath.Gain(SoundCategory.Steps, mix), Is.Zero, "a master volume of 0 is the mute the panel offers");
        mix.masterVolume = 1f;
        mix.mute = true;
        foreach (SoundCategory category in Enum.GetValues(typeof(SoundCategory))) {
            Assert.That(AudioMath.Gain(category, mix), Is.Zero, category.ToString());
        }
    }

    [Test]
    public void EverySoundBelongsToTheRightCategory() {
        Assert.That(AudioMath.CategoryOf(SoundId.Footstep), Is.EqualTo(SoundCategory.Steps));
        Assert.That(AudioMath.CategoryOf(SoundId.Landing), Is.EqualTo(SoundCategory.Landing));
        Assert.That(AudioMath.CategoryOf(SoundId.Gunshot), Is.EqualTo(SoundCategory.Weapon));
        Assert.That(AudioMath.CategoryOf(SoundId.MagazineIn), Is.EqualTo(SoundCategory.Weapon));
        Assert.That(AudioMath.CategoryOf(SoundId.Casing), Is.EqualTo(SoundCategory.Weapon));
        Assert.That(AudioMath.CategoryOf(SoundId.TargetHit), Is.EqualTo(SoundCategory.Impacts));
        Assert.That(AudioMath.CategoryOf(SoundId.Ricochet), Is.EqualTo(SoundCategory.Impacts));
        Assert.That(AudioMath.CategoryOf(SoundId.WoodKnock), Is.EqualTo(SoundCategory.Impacts));
        Assert.That(AudioMath.CategoryOf(SoundId.WoodBreak), Is.EqualTo(SoundCategory.Impacts));
    }

    [Test]
    public void PanFollowsTheSideOfTheScreenAndIsClamped() {
        Assert.That(AudioMath.Pan(5f, (5f, 10f)), Is.Zero);
        Assert.That(AudioMath.Pan(10f, (5f, 10f)), Is.EqualTo(0.5f * AudioMath.MaxPan).Within(1e-5f));
        Assert.That(AudioMath.Pan(0f, (5f, 10f)), Is.LessThan(0f));
        Assert.That(AudioMath.Pan(500f, (5f, 10f)), Is.EqualTo(AudioMath.MaxPan));
        Assert.That(AudioMath.Pan(-500f, (5f, 10f)), Is.EqualTo(-AudioMath.MaxPan));
        Assert.That(AudioMath.Pan(3f, (0f, 0f)), Is.Zero);
    }

    [Test]
    public void TheMagazineSeatsNearTheEndOfTheReloadButNotBeforeItsMiddle() {
        Assert.That(AudioMath.MagazineInDelay(1.1f), Is.EqualTo(0.8f).Within(1e-5f));
        Assert.That(AudioMath.MagazineInDelay(0.4f), Is.EqualTo(0.2f).Within(1e-5f), "a short reload does not seat it at once");
        Assert.That(AudioMath.MagazineInDelay(0f), Is.Zero);
    }

    // ---- Voice pool -----------------------------------------------------------------------------------------

    [Test]
    public void ANewSoundTakesTheFirstFinishedVoice() {
        float[] startedAt = { 0f, 1f, 2f };
        float[] endsAt = { 5f, 2.5f, 9f };
        Assert.That(AudioMath.PickVoice(startedAt, endsAt, 3f), Is.EqualTo(1));
    }

    [Test]
    public void WhenEveryVoiceIsBusyTheOldestIsStolen() {
        float[] startedAt = { 2f, 0.5f, 1f };
        float[] endsAt = { 9f, 9f, 9f };
        Assert.That(AudioMath.PickVoice(startedAt, endsAt, 3f), Is.EqualTo(1));
    }

    [Test]
    public void AFreshPoolHandsOutTheFirstVoice() {
        float[] startedAt = { float.NegativeInfinity, float.NegativeInfinity };
        float[] endsAt = { float.NegativeInfinity, float.NegativeInfinity };
        Assert.That(AudioMath.PickVoice(startedAt, endsAt, 0f), Is.Zero);
    }

    // ---- Presets --------------------------------------------------------------------------------------------

    [Test]
    public void TheAudioSettingsSurviveAJsonRoundTripAndACopy() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.audio.masterVolume = 0.35f;
        original.audio.pitchJitter = 0.2f;
        original.audio.mute = true;
        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.audio.masterVolume, Is.EqualTo(0.35f));
        Assert.That(loaded.audio.pitchJitter, Is.EqualTo(0.2f));
        Assert.That(loaded.audio.mute, Is.True);

        MovementPreset copy = original.Copy();
        copy.audio.masterVolume = 0.9f;
        Assert.That(original.audio.masterVolume, Is.EqualTo(0.35f), "a copy has its own audio settings");
    }

    [Test]
    public void APresetSavedBeforeTheAudioExistedKeepsTheAudioDefaults() {
        MovementPreset loaded = MovementPreset.FromJson("{\"version\":5,\"run\":{\"lean\":21}}");
        CharacterAudioSettings defaults = new CharacterAudioSettings();

        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(5));
        Assert.That(loaded.run.lean, Is.EqualTo(21f));
        Assert.That(loaded.audio, Is.Not.Null);
        Assert.That(loaded.audio.masterVolume, Is.EqualTo(defaults.masterVolume));
        Assert.That(loaded.audio.mute, Is.False);
        Assert.That(loaded.audio.footMinImpact, Is.EqualTo(defaults.footMinImpact));
        Assert.That(loaded.audio.landingFullImpact, Is.EqualTo(defaults.landingFullImpact));
    }

    [Test]
    public void CopyFromCopiesEveryField() {
        CharacterAudioSettings source = new CharacterAudioSettings {
            masterVolume = 0.1f, mute = true, stepsVolume = 0.2f, landingVolume = 0.3f, weaponVolume = 0.4f, impactsVolume = 0.5f,
            pitchJitter = 0.15f, footMinImpact = 1.1f, footFullImpact = 9f, landingMinImpact = 3.3f, landingFullImpact = 25f,
        };
        CharacterAudioSettings target = new CharacterAudioSettings();
        target.CopyFrom(source);
        Assert.That(JsonUtility.ToJson(target), Is.EqualTo(JsonUtility.ToJson(source)));
    }
}
