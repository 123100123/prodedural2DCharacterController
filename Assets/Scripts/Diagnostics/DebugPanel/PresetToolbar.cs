using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Resets, saves, loads and copies the tuned settings. Presets are JSON files under the persistent data folder; the
// copied JSON can be pasted into a preset file or used to update the scene's defaults by hand.
public sealed class PresetToolbar {
    private readonly TuningEditor editor;
    private readonly Func<LocomotionTuningSchema.Section> currentSection;
    private readonly TextField presetName;
    private readonly DropdownField savedPresets;
    private readonly Label status;

    public PresetToolbar(TuningEditor editor, Func<LocomotionTuningSchema.Section> currentSection) {
        this.editor = editor;
        this.currentSection = currentSection;
        Root = new VisualElement();
        Root.AddToClassList("loco-toolbar");

        AddButton(("Reset section", ResetSection));
        AddButton(("Reset all", editor.ResetAll));
        presetName = new TextField { value = "my-preset" };
        presetName.AddToClassList("loco-toolbar__name");
        Root.Add(presetName);
        AddButton(("Save preset", SavePreset));
        savedPresets = new DropdownField();
        savedPresets.AddToClassList("loco-toolbar__presets");
        Root.Add(savedPresets);
        AddButton(("Load preset", LoadPreset));
        AddButton(("Copy current config", CopyConfig));
        status = new Label();
        status.AddToClassList("loco-toolbar__status");
        Root.Add(status);
        ListSavedPresets();
    }

    public VisualElement Root { get; }

    private void ResetSection() {
        LocomotionTuningSchema.Section section = currentSection();
        if (section == null) {
            status.text = "Open a tuning tab to reset its section.";
            return;
        }
        editor.ResetSection(section);
        status.text = $"Reset {section.Title}.";
    }

    private void SavePreset() {
        string path = LocomotionPresetStore.Save((presetName.value, editor.Current));
        ListSavedPresets();
        savedPresets.value = presetName.value;
        status.text = $"Saved to {path}";
    }

    private void LoadPreset() {
        if (!LocomotionPresetStore.TryLoad(savedPresets.value, out LocomotionPreset preset)) {
            status.text = "Choose a saved preset to load.";
            return;
        }
        editor.Load(preset);
        presetName.value = savedPresets.value;
        status.text = $"Loaded {savedPresets.value}.";
    }

    private void CopyConfig() {
        GUIUtility.systemCopyBuffer = editor.Current.ToJson();
        status.text = "Copied the current config as JSON.";
    }

    private void ListSavedPresets() => savedPresets.choices = LocomotionPresetStore.Names().ToList();

    private void AddButton((string text, Action clicked) button) {
        Button element = new Button(button.clicked) { text = button.text };
        element.AddToClassList("loco-toolbar__button");
        Root.Add(element);
    }
}
