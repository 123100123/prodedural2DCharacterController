using System;
using UnityEngine;

// The lighting settings as one plain value: what the lighting tab edits and what lighting presets save as JSON. Kept
// apart from the movement preset, so a look can be saved, shared and loaded without touching how the character moves.
[Serializable]
public class LightingPreset : ITuningPreset<LightingPreset> {
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public LightingSettings lighting = new LightingSettings();

    public static LightingPreset CodeDefaults() => new LightingPreset();

    public LightingPreset Copy() {
        LightingPreset copy = (LightingPreset)MemberwiseClone();
        copy.lighting = lighting.Clone();
        return copy;
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    // The file's values over the code defaults: a setting the file does not mention keeps its default.
    public static LightingPreset FromJson(string json) {
        LightingPreset preset = CodeDefaults();
        JsonUtility.FromJsonOverwrite(json, preset);
        return preset;
    }
}
