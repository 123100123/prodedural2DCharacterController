using System;
using UnityEngine;

// Every synthesized clip, built once when the character is composed: the buffers come from ClipSynth and are handed to
// Unity as mono AudioClips. Building them all takes a few milliseconds.
public sealed class SoundBank {
    private readonly AudioClip[][] clips;

    public SoundBank(int sampleRate) {
        SampleRate = sampleRate;
        Array ids = Enum.GetValues(typeof(SoundId));
        clips = new AudioClip[ids.Length][];
        foreach (SoundId id in ids) {
            SoundId source = ClipSynth.SourceOf(id);
            if (source != id) {
                clips[(int)id] = clips[(int)source];
                continue;
            }
            int variants = ClipSynth.VariantCount(id);
            clips[(int)id] = new AudioClip[variants];
            for (int variant = 0; variant < variants; variant++) {
                clips[(int)id][variant] = MakeClip(id + (variants > 1 ? "-" + variant : ""), ClipSynth.Build(id, variant, sampleRate));
            }
        }
    }

    public int SampleRate { get; }

    // Clips can only be shared once a ricochet's source is built, which the enum order guarantees (Casing precedes Ricochet).
    public int VariantCount(SoundId id) => clips[(int)id].Length;

    public AudioClip Clip(SoundId id, int variant) => clips[(int)id][variant];

    public void Dispose() {
        foreach (AudioClip[] group in clips) {
            foreach (AudioClip clip in group) {
                Discard(clip);
            }
        }
    }

    private AudioClip MakeClip(string name, float[] samples) {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void Discard(UnityEngine.Object unwanted) {
        if (unwanted == null) {
            return;
        }
        if (Application.isPlaying) {
            UnityEngine.Object.Destroy(unwanted);
        }
        else {
            UnityEngine.Object.DestroyImmediate(unwanted);
        }
    }
}
