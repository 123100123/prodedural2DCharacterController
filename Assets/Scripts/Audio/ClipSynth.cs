using UnityEngine;

// The recipes for every sound, as float sample buffers: filtered noise bursts, decaying sweeps and a few partials. Pure
// and deterministic (each recipe seeds its own noise), so the buffers are the same on every run and are tested without
// Unity's audio. Every buffer peaks at Peak (below 1, so mixing in the voice never clips) and ends at silence.
public static class ClipSynth {
    public const int DefaultSampleRate = 44100;
    public const float Peak = 0.9f;
    private const int FootstepVariants = 4;
    private const int CasingVariants = 2;
    private const int WoodKnockVariants = 2;
    private const float EndFade = 0.004f;

    // Buffers made for a sound: several slightly different ones for what is heard often, one for the rest. A ricochet has
    // none of its own: it is the casing's tinkle played higher.
    public static int VariantCount(SoundId id) {
        switch (id) {
            case SoundId.Footstep:
                return FootstepVariants;
            case SoundId.Casing:
                return CasingVariants;
            case SoundId.WoodKnock:
                return WoodKnockVariants;
            case SoundId.Ricochet:
                return 0;
            default:
                return 1;
        }
    }

    // The sound whose buffers a sound plays.
    public static SoundId SourceOf(SoundId id) => id == SoundId.Ricochet ? SoundId.Casing : id;

    public static float[] Build(SoundId id, int variant, int sampleRate) {
        switch (SourceOf(id)) {
            case SoundId.Footstep:
                return Footstep(variant, sampleRate);
            case SoundId.Landing:
                return Landing(sampleRate);
            case SoundId.Jump:
                return Jump(sampleRate);
            case SoundId.Roll:
                return Roll(sampleRate);
            case SoundId.Gunshot:
                return Gunshot(sampleRate);
            case SoundId.DryClick:
                return DryClick(sampleRate);
            case SoundId.MagazineOut:
                return MagazineOut(sampleRate);
            case SoundId.MagazineIn:
                return MagazineIn(sampleRate);
            case SoundId.Casing:
                return Casing(variant, sampleRate);
            case SoundId.WoodKnock:
                return WoodKnock(variant, sampleRate);
            case SoundId.WoodBreak:
                return WoodBreak(sampleRate);
            default:
                return TargetHit(sampleRate);
        }
    }

    // A dull thump of filtered noise over a low sine, a touch different in each variant.
    public static float[] Footstep(int variant, int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.1f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(100 + variant);
        Dsp.LowPass body = new Dsp.LowPass(650f + 220f * variant, sampleRate);
        Dsp.Sweep thump = new Dsp.Sweep((130f - 12f * variant, 60f), 0.08f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float envelope = Dsp.Attack(time, 0.002f) * Dsp.Decay(time, 0.024f);
            samples[index] = (body.Process(noise.Next()) * 1.4f + thump.Sine(time) * 0.6f) * envelope;
        }
        return Finish(samples, sampleRate);
    }

    // A deep sine sliding down with a burst of low noise on top.
    public static float[] Landing(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.4f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(200);
        Dsp.LowPass rumble = new Dsp.LowPass(480f, sampleRate);
        Dsp.Sweep body = new Dsp.Sweep((115f, 38f), 0.3f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float tone = body.Sine(time) * Dsp.Attack(time, 0.003f) * Dsp.Decay(time, 0.1f);
            float scuff = rumble.Process(noise.Next()) * 1.6f * Dsp.Attack(time, 0.002f) * Dsp.Decay(time, 0.055f);
            samples[index] = tone + scuff * 0.55f;
        }
        return Finish(samples, sampleRate);
    }

    // A short airy swish: band-limited noise that swells quickly and falls away.
    public static float[] Jump(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.26f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(300);
        Dsp.BandPass air = new Dsp.BandPass((550f, 3200f), sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            samples[index] = air.Process(noise.Next()) * Dsp.Attack(time, 0.05f) * Dsp.Decay(time, 0.075f);
        }
        return Finish(samples, sampleRate);
    }

    // A longer brushing of cloth and ground: band-limited noise with a quick flutter in its level.
    public static float[] Roll(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.5f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(400);
        Dsp.BandPass brush = new Dsp.BandPass((260f, 2600f), sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float flutter = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 13f * time);
            samples[index] = brush.Process(noise.Next()) * flutter * Dsp.Attack(time, 0.03f) * Dsp.Decay(time, 0.17f);
        }
        return Finish(samples, sampleRate);
    }

    // A sharp crack of high noise, a low body that drops away and a longer, duller tail.
    public static float[] Gunshot(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.75f, sampleRate)];
        Dsp.Noise crackNoise = new Dsp.Noise(500);
        Dsp.Noise tailNoise = new Dsp.Noise(501);
        Dsp.HighPass crack = new Dsp.HighPass(1400f, sampleRate);
        Dsp.LowPass tail = new Dsp.LowPass(1700f, sampleRate);
        Dsp.Sweep body = new Dsp.Sweep((230f, 52f), 0.25f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float attack = Dsp.Attack(time, 0.0006f);
            float snap = crack.Process(crackNoise.Next()) * Dsp.Decay(time, 0.011f);
            float thud = body.Sine(time) * Dsp.Decay(time, 0.07f) * 0.85f;
            float rumble = tail.Process(tailNoise.Next()) * 1.5f * Dsp.Decay(time, 0.19f) * 0.4f;
            samples[index] = (snap + thud + rumble) * attack;
        }
        return Finish(samples, sampleRate);
    }

    // Two small dry ticks, the hammer falling on an empty chamber.
    public static float[] DryClick(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.07f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(600);
        Dsp.BandPass tick = new Dsp.BandPass((1800f, 6500f), sampleRate);
        Dsp.Sweep ping = new Dsp.Sweep((2300f, 1700f), 0.02f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float second = Mathf.Max(0f, time - 0.03f);
            float envelope = Dsp.Decay(time, 0.0045f) + 0.55f * (time >= 0.03f ? Dsp.Decay(second, 0.0055f) : 0f);
            samples[index] = (tick.Process(noise.Next()) * 1.6f + ping.Triangle(time) * 0.3f) * envelope;
        }
        return Finish(samples, sampleRate);
    }

    // The magazine release: one hard click and a short slide.
    public static float[] MagazineOut(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.12f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(700);
        Dsp.BandPass click = new Dsp.BandPass((1300f, 5200f), sampleRate);
        Dsp.Sweep slide = new Dsp.Sweep((950f, 520f), 0.05f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float envelope = Dsp.Attack(time, 0.0008f) * Dsp.Decay(time, 0.012f);
            samples[index] = (click.Process(noise.Next()) * 1.5f + slide.Triangle(time) * 0.4f) * envelope;
        }
        return Finish(samples, sampleRate);
    }

    // The magazine seating: a heavier clack with a low knock and a lighter latch click just after.
    public static float[] MagazineIn(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.2f, sampleRate)];
        Dsp.Noise noise = new Dsp.Noise(800);
        Dsp.BandPass clack = new Dsp.BandPass((700f, 4200f), sampleRate);
        Dsp.Sweep knock = new Dsp.Sweep((170f, 85f), 0.05f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float latch = time >= 0.07f ? 0.5f * Dsp.Decay(time - 0.07f, 0.008f) : 0f;
            float envelope = Dsp.Attack(time, 0.0008f) * Dsp.Decay(time, 0.02f) + latch;
            float body = knock.Sine(time) * Dsp.Decay(time, 0.03f) * 0.8f;
            samples[index] = clack.Process(noise.Next()) * 1.5f * envelope + body;
        }
        return Finish(samples, sampleRate);
    }

    // A high, short metallic ring: three inharmonic partials, struck twice (the bounce), each variant tuned a little apart.
    public static float[] Casing(int variant, int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.3f, sampleRate)];
        float detune = 1f + 0.06f * variant;
        float[] partials = { 3150f * detune, 4720f * detune, 6240f * detune };
        float[] levels = { 1f, 0.6f, 0.4f };
        float[] lifetimes = { 0.06f, 0.045f, 0.03f };
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float sum = 0f;
            for (int partial = 0; partial < partials.Length; partial++) {
                float ring = Mathf.Sin(2f * Mathf.PI * partials[partial] * time) * levels[partial];
                float first = Dsp.Attack(time, 0.0004f) * Dsp.Decay(time, lifetimes[partial]);
                float bounce = time >= 0.1f ? 0.35f * Dsp.Decay(time - 0.1f, lifetimes[partial] * 0.8f) : 0f;
                sum += ring * (first + bounce);
            }
            samples[index] = sum;
        }
        return Finish(samples, sampleRate);
    }

    // A dull thud with a sharp tick on top.
    public static float[] TargetHit(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.22f, sampleRate)];
        Dsp.Noise thudNoise = new Dsp.Noise(900);
        Dsp.Noise tickNoise = new Dsp.Noise(901);
        Dsp.LowPass dull = new Dsp.LowPass(850f, sampleRate);
        Dsp.HighPass tick = new Dsp.HighPass(2600f, sampleRate);
        Dsp.Sweep body = new Dsp.Sweep((165f, 68f), 0.1f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float thud = body.Sine(time) * Dsp.Decay(time, 0.05f) + dull.Process(thudNoise.Next()) * 1.4f * Dsp.Decay(time, 0.03f) * 0.5f;
            float snap = tick.Process(tickNoise.Next()) * 0.6f * Dsp.Decay(time, 0.003f);
            samples[index] = (thud + snap) * Dsp.Attack(time, 0.0005f);
        }
        return Finish(samples, sampleRate);
    }

    // A hollow knock on a wooden board: two quickly dying resonances, the lower one sliding down a little, and a dry click
    // at the start. Each variant is tuned a little apart.
    public static float[] WoodKnock(int variant, int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.2f, sampleRate)];
        float tune = 1f + 0.14f * variant;
        Dsp.Noise noise = new Dsp.Noise(1000 + variant);
        Dsp.BandPass click = new Dsp.BandPass((1500f, 5200f), sampleRate);
        Dsp.Sweep body = new Dsp.Sweep((520f * tune, 340f * tune), 0.05f, sampleRate);
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float low = body.Sine(time) * Dsp.Decay(time, 0.032f);
            float high = Mathf.Sin(2f * Mathf.PI * 1240f * tune * time) * 0.45f * Dsp.Decay(time, 0.014f);
            float tick = click.Process(noise.Next()) * 1.3f * Dsp.Decay(time, 0.0025f);
            samples[index] = (low + high + tick) * Dsp.Attack(time, 0.0005f);
        }
        return Finish(samples, sampleRate);
    }

    // A board breaking: a loud crack of noise over a low thump, then splinters and pieces clattering down, each a small
    // wooden knock higher or lower than the last and softer.
    public static float[] WoodBreak(int sampleRate) {
        float[] samples = new float[Dsp.SampleCount(0.7f, sampleRate)];
        Dsp.Noise crackNoise = new Dsp.Noise(1100);
        Dsp.Noise pieceNoise = new Dsp.Noise(1101);
        Dsp.HighPass crack = new Dsp.HighPass(1100f, sampleRate);
        Dsp.BandPass piece = new Dsp.BandPass((900f, 4800f), sampleRate);
        Dsp.Sweep thump = new Dsp.Sweep((210f, 80f), 0.12f, sampleRate);
        float[] starts = { 0.07f, 0.13f, 0.18f, 0.26f, 0.31f, 0.38f, 0.43f, 0.52f };
        float[] pitches = { 760f, 540f, 980f, 620f, 860f, 480f, 700f, 560f };
        for (int index = 0; index < samples.Length; index++) {
            float time = index / (float)sampleRate;
            float snap = crack.Process(crackNoise.Next()) * 1.2f * Dsp.Decay(time, 0.016f);
            float body = thump.Sine(time) * 0.9f * Dsp.Decay(time, 0.07f);
            float clatter = 0f;
            float grit = piece.Process(pieceNoise.Next());
            for (int knock = 0; knock < starts.Length; knock++) {
                float age = time - starts[knock];
                if (age < 0f) {
                    continue;
                }
                float level = 0.7f * Mathf.Pow(0.8f, knock);
                clatter += level * (Mathf.Sin(2f * Mathf.PI * pitches[knock] * age) * Dsp.Decay(age, 0.018f) + grit * 0.9f * Dsp.Decay(age, 0.004f));
            }
            samples[index] = (snap + body + clatter) * Dsp.Attack(time, 0.0005f);
        }
        return Finish(samples, sampleRate);
    }

    private static float[] Finish(float[] samples, int sampleRate) {
        Dsp.FadeOut(samples, EndFade, sampleRate);
        Dsp.NormalizePeak(samples, Peak);
        return samples;
    }
}
