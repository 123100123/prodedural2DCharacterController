using UnityEngine;

// The building blocks of the synthesized sounds, all pure and deterministic: seeded noise, one-pole filters, envelopes
// and a sweeping oscillator. Nothing here touches Unity's audio, so it runs (and is tested) anywhere.
public static class Dsp {
    // White noise from a seed (xorshift32), so a clip is the same on every run and machine.
    public struct Noise {
        private uint state;

        public Noise(int seed) {
            state = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
            if (state == 0u) {
                state = 1u;
            }
        }

        // The next sample in [-1, 1].
        public float Next() {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / (float)0x7FFFFF - 1f;
        }
    }

    // A one-pole low-pass: smooths toward its input. The high-pass of the same cutoff is the input minus this.
    public struct LowPass {
        private readonly float coefficient;
        private float value;

        public LowPass(float cutoff, int sampleRate) {
            coefficient = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, cutoff) / sampleRate);
            value = 0f;
        }

        public float Process(float input) {
            value += coefficient * (input - value);
            return value;
        }
    }

    // A one-pole high-pass: what a low-pass of the same cutoff leaves out.
    public struct HighPass {
        private LowPass low;

        public HighPass(float cutoff, int sampleRate) {
            low = new LowPass(cutoff, sampleRate);
        }

        public float Process(float input) => input - low.Process(input);
    }

    // A simple band-pass: a high-pass at the low edge followed by a low-pass at the high edge.
    public struct BandPass {
        private HighPass lowEdge;
        private LowPass highEdge;

        public BandPass((float low, float high) band, int sampleRate) {
            lowEdge = new HighPass(band.low, sampleRate);
            highEdge = new LowPass(band.high, sampleRate);
        }

        public float Process(float input) => highEdge.Process(lowEdge.Process(input));
    }

    // A sine (or triangle) whose frequency glides exponentially from start to end over the duration; the phase carries on
    // between samples, so there is no click.
    public struct Sweep {
        private readonly float startFrequency;
        private readonly float endFrequency;
        private readonly float duration;
        private readonly int sampleRate;
        private float phase;

        public Sweep((float start, float end) frequency, float duration, int sampleRate) {
            startFrequency = frequency.start;
            endFrequency = frequency.end;
            this.duration = Mathf.Max(1e-4f, duration);
            this.sampleRate = sampleRate;
            phase = 0f;
        }

        public float Sine(float time) {
            Advance(time);
            return Mathf.Sin(2f * Mathf.PI * phase);
        }

        public float Triangle(float time) {
            Advance(time);
            return 4f * Mathf.Abs(phase - Mathf.Floor(phase + 0.5f)) - 1f;
        }

        private void Advance(float time) {
            float frequency = startFrequency * Mathf.Pow(endFrequency / startFrequency, Mathf.Clamp01(time / duration));
            phase += frequency / sampleRate;
            phase -= Mathf.Floor(phase);
        }
    }

    // Exponential decay: 1 at time 0, 1/e after tau seconds.
    public static float Decay(float time, float tau) => Mathf.Exp(-time / Mathf.Max(1e-4f, tau));

    // A short ramp from 0 to 1 over attack seconds, so a sound does not begin with a click.
    public static float Attack(float time, float attack) => attack <= 0f ? 1f : Mathf.Clamp01(time / attack);

    public static int SampleCount(float seconds, int sampleRate) => Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));

    // Scales the buffer so its loudest sample is peak.
    public static void NormalizePeak(float[] samples, float peak) {
        float loudest = 0f;
        for (int index = 0; index < samples.Length; index++) {
            loudest = Mathf.Max(loudest, Mathf.Abs(samples[index]));
        }
        if (loudest <= 1e-6f) {
            return;
        }
        float gain = peak / loudest;
        for (int index = 0; index < samples.Length; index++) {
            samples[index] *= gain;
        }
    }

    // Ramps the last fadeSeconds to silence, so a clip ends at zero whatever its envelope left.
    public static void FadeOut(float[] samples, float fadeSeconds, int sampleRate) {
        int fade = Mathf.Min(samples.Length, SampleCount(fadeSeconds, sampleRate));
        for (int index = 0; index < fade; index++) {
            samples[samples.Length - 1 - index] *= (float)index / fade;
        }
    }

    // Root-mean-square level of samples[start, start + count).
    public static float Rms(float[] samples, int start, int count) {
        double sum = 0.0;
        int end = Mathf.Min(samples.Length, start + count);
        for (int index = start; index < end; index++) {
            sum += samples[index] * samples[index];
        }
        return end <= start ? 0f : Mathf.Sqrt((float)(sum / (end - start)));
    }
}
