using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Named presets of one domain saved as JSON files under the player's persistent data folder.
public sealed class PresetStore<TPreset> where TPreset : class, ITuningPreset<TPreset> {
    private const string Extension = ".json";

    private readonly string folderName;
    private readonly Func<string, TPreset> fromJson;

    public PresetStore((string folderName, Func<string, TPreset> fromJson) format) {
        folderName = format.folderName;
        fromJson = format.fromJson;
    }

    public string Folder => Path.Combine(Application.persistentDataPath, folderName);

    // Returns the file the preset was written to.
    public string Save((string name, TPreset preset) entry) {
        Directory.CreateDirectory(Folder);
        string path = PathFor(entry.name);
        File.WriteAllText(path, entry.preset.ToJson());
        return path;
    }

    public bool TryLoad(string name, out TPreset preset) {
        string path = PathFor(name);
        preset = File.Exists(path) ? fromJson(File.ReadAllText(path)) : null;
        return preset != null;
    }

    public IReadOnlyList<string> Names() {
        if (!Directory.Exists(Folder)) {
            return new List<string>();
        }
        return Directory.GetFiles(Folder, "*" + Extension).Select(Path.GetFileNameWithoutExtension).OrderBy(name => name).ToList();
    }

    private string PathFor(string name) {
        string safeName = string.Concat((string.IsNullOrWhiteSpace(name) ? "preset" : name.Trim()).Select(SafeFileNameChar));
        return Path.Combine(Folder, safeName + Extension);
    }

    private static char SafeFileNameChar(char character) => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character;
}
