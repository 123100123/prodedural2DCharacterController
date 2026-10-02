using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Named locomotion presets saved as JSON files under the player's persistent data folder.
public static class LocomotionPresetStore {
    private const string Extension = ".json";

    public static string Folder => Path.Combine(Application.persistentDataPath, "LocomotionPresets");

    // Returns the file the preset was written to.
    public static string Save((string name, LocomotionPreset preset) entry) {
        Directory.CreateDirectory(Folder);
        string path = PathFor(entry.name);
        File.WriteAllText(path, entry.preset.ToJson());
        return path;
    }

    public static bool TryLoad(string name, out LocomotionPreset preset) {
        string path = PathFor(name);
        preset = File.Exists(path) ? LocomotionPreset.FromJson(File.ReadAllText(path)) : null;
        return preset != null;
    }

    public static IReadOnlyList<string> Names() {
        if (!Directory.Exists(Folder)) {
            return new List<string>();
        }
        return Directory.GetFiles(Folder, "*" + Extension).Select(Path.GetFileNameWithoutExtension).OrderBy(name => name).ToList();
    }

    private static string PathFor(string name) {
        string safeName = string.Concat((string.IsNullOrWhiteSpace(name) ? "preset" : name.Trim()).Select(SafeFileNameChar));
        return Path.Combine(Folder, safeName + Extension);
    }

    private static char SafeFileNameChar(char character) => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character;
}
