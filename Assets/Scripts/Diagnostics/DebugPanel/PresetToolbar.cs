using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Resets, saves, loads and copies one domain's tuned settings. Presets are JSON files under the persistent data
// folder; the copied JSON can be pasted into a preset file or used to update the scene's defaults by hand.
public sealed class PresetToolbar<TPreset> where TPreset : class, ITuningPreset<TPreset> {
    private readonly TuningEditor<TPreset> editor;
    private readonly TuningDomain<TPreset> domain;
    private readonly Func<TuningSection<TPreset>> currentSection;
    private readonly TextField presetName;
    private readonly DropdownField savedPresets;
    private readonly Label status;

    public PresetToolbar((TuningEditor<TPreset> editor, TuningDomain<TPreset> domain) tuning, Func<TuningSection<TPreset>> currentSection) {
        editor = tuning.editor;
        domain = tuning.domain;
        this.currentSection = currentSection;
        Root = new VisualElement();
        Root.AddToClassList("loco-toolbar");

        AddButton(("Reset section", ResetSection));
        AddButton(("Reset all", editor.ResetAll));
        AddButton(("Code defaults", LoadCodeDefaults));
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
        TuningSection<TPreset> section = currentSection();
        if (section == null) {
            status.text = "Open a tuning tab to reset its section.";
            return;
        }
        editor.ResetSection(section);
        status.text = $"Reset {section.Title}.";
    }

    // Reset restores the values the session started with (the scene's); this loads what the code defines instead.
    private void LoadCodeDefaults() {
        editor.Load(domain.CodeDefaults());
        status.text = "Loaded the code defaults (not saved to the scene).";
    }

    private void SavePreset() {
        string path = domain.Store.Save((presetName.value, editor.Current));
        ListSavedPresets();
        savedPresets.value = presetName.value;
        status.text = $"Saved to {path}";
    }

    private void LoadPreset() {
        if (!domain.Store.TryLoad(savedPresets.value, out TPreset preset)) {
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

    private void ListSavedPresets() => savedPresets.choices = domain.Store.Names().ToList();

    private void AddButton((string text, Action clicked) button) {
        Button element = new Button(button.clicked) { text = button.text };
        element.AddToClassList("loco-toolbar__button");
        Root.Add(element);
    }
}
