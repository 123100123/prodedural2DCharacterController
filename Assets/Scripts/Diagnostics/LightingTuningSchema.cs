using System;
using System.Collections.Generic;
using UnityEngine;
using static TuningUnits;
using Parameter = TuningParameter<LightingPreset>;
using Section = TuningSection<LightingPreset>;

// Every tunable lighting value the runtime panel offers: the ambient light, the light about the player and the muzzle
// flash. Colours are three sliders (red, green, blue); on/off switches are a slider across 0 to 1 (on from one half).
public static class LightingTuningSchema {
    public static IReadOnlyList<Section> Build() => new[] { new Section("LIGHTING", All()) };

    public static TuningDomain<LightingPreset> CreateDomain(ILightingTarget lighting) {
        PresetStore<LightingPreset> store = new PresetStore<LightingPreset>(("LightingPresets", LightingPreset.FromJson));
        Func<LightingPreset> capture = () => new LightingPreset { lighting = lighting.Settings.Clone() };
        Action<LightingPreset> apply = preset => {
            lighting.Settings.CopyFrom(preset.lighting);
            lighting.Apply();
        };
        return new TuningDomain<LightingPreset>(("Lighting", Build()), (capture, apply, LightingPreset.CodeDefaults), store);
    }

    private static List<Parameter> All() {
        List<Parameter> parameters = new List<Parameter>();
        parameters.AddRange(Colour("Ambient light", "Colour", (p => p.lighting.ambientColor, (p, c) => p.lighting.ambientColor = c)));
        parameters.Add(new Parameter(("Ambient light", "Intensity", Multiple), (0.2f, 1.5f), (p => p.lighting.ambientIntensity, (p, v) => p.lighting.ambientIntensity = v)));

        parameters.Add(Switch("Player light", "On", (p => p.lighting.playerEnabled, (p, on) => p.lighting.playerEnabled = on)));
        parameters.AddRange(Colour("Player light", "Colour", (p => p.lighting.playerColor, (p, c) => p.lighting.playerColor = c)));
        parameters.Add(new Parameter(("Player light", "Intensity", Multiple), (0f, 2f), (p => p.lighting.playerIntensity, (p, v) => p.lighting.playerIntensity = v)));
        parameters.Add(new Parameter(("Player light", "Inner radius", Units), (0f, 10f), (p => p.lighting.playerInnerRadius, (p, v) => p.lighting.playerInnerRadius = v)));
        parameters.Add(new Parameter(("Player light", "Outer radius", Units), (0.5f, 20f), (p => p.lighting.playerOuterRadius, (p, v) => p.lighting.playerOuterRadius = v)));
        parameters.Add(new Parameter(("Player light", "Edge softness", Share), (0f, 1f), (p => p.lighting.playerFalloff, (p, v) => p.lighting.playerFalloff = v)));
        parameters.Add(new Parameter(("Player light", "Height above the pelvis", Units), (-1f, 4f), (p => p.lighting.playerHeight, (p, v) => p.lighting.playerHeight = v)));

        parameters.AddRange(Colour("Muzzle flash", "Colour", (p => p.lighting.muzzleColor, (p, c) => p.lighting.muzzleColor = c)));
        parameters.Add(new Parameter(("Muzzle flash", "Peak intensity", Multiple), (0f, 6f), (p => p.lighting.muzzlePeakIntensity, (p, v) => p.lighting.muzzlePeakIntensity = v)));
        parameters.Add(new Parameter(("Muzzle flash", "Inner radius", Units), (0f, 5f), (p => p.lighting.muzzleInnerRadius, (p, v) => p.lighting.muzzleInnerRadius = v)));
        parameters.Add(new Parameter(("Muzzle flash", "Outer radius", Units), (0.5f, 15f), (p => p.lighting.muzzleOuterRadius, (p, v) => p.lighting.muzzleOuterRadius = v)));
        parameters.Add(new Parameter(("Muzzle flash", "Duration", Seconds), (0.02f, 0.5f), (p => p.lighting.muzzleDuration, (p, v) => p.lighting.muzzleDuration = v)));
        parameters.Add(new Parameter(("Muzzle flash", "Decay sharpness", Multiple), (0.2f, 8f), (p => p.lighting.muzzleDecayExponent, (p, v) => p.lighting.muzzleDecayExponent = v)));
        parameters.Add(new Parameter(("Muzzle flash", "Radius shrinks by", Share), (0f, 1f), (p => p.lighting.muzzleRadiusDecay, (p, v) => p.lighting.muzzleRadiusDecay = v)));
        parameters.Add(Switch("Muzzle flash", "Casts shadows", (p => p.lighting.muzzleCastsShadows, (p, on) => p.lighting.muzzleCastsShadows = on)));
        parameters.Add(new Parameter(("Muzzle flash", "Shadow strength", Share), (0f, 1f), (p => p.lighting.muzzleShadowIntensity, (p, v) => p.lighting.muzzleShadowIntensity = v)));
        return parameters;
    }

    // Red, green and blue sliders over a colour (its alpha is left alone).
    private static IEnumerable<Parameter> Colour(string group, string name, (Func<LightingPreset, Color> read, Action<LightingPreset, Color> write) colour) {
        yield return new Parameter((group, name + " red", Share), (0f, 1f), (p => colour.read(p).r, (p, v) => { Color c = colour.read(p); c.r = v; colour.write(p, c); }));
        yield return new Parameter((group, name + " green", Share), (0f, 1f), (p => colour.read(p).g, (p, v) => { Color c = colour.read(p); c.g = v; colour.write(p, c); }));
        yield return new Parameter((group, name + " blue", Share), (0f, 1f), (p => colour.read(p).b, (p, v) => { Color c = colour.read(p); c.b = v; colour.write(p, c); }));
    }

    private static Parameter Switch(string group, string name, (Func<LightingPreset, bool> read, Action<LightingPreset, bool> write) flag) {
        return new Parameter((group, name, "0 off, 1 on"), (0f, 1f), (p => flag.read(p) ? 1f : 0f, (p, v) => flag.write(p, v >= 0.5f)));
    }
}
