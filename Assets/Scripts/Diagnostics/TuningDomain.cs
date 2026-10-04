using System;
using System.Collections.Generic;

// A preset every tuning domain edits: a plain value that can be copied and written as JSON.
public interface ITuningPreset<TPreset> {
    TPreset Copy();
    string ToJson();
}

// A group of tuning parameters shown as one tab.
public sealed class TuningSection<TPreset> {
    public TuningSection(string title, IReadOnlyList<TuningParameter<TPreset>> parameters) {
        Title = title;
        Parameters = parameters;
    }

    public string Title { get; }
    public IReadOnlyList<TuningParameter<TPreset>> Parameters { get; }
}

// One independently tunable and savable area of the game (movement, lighting): its sections, how its settings are
// read from and written to their owners, the settings it started the session with, and where its presets are kept.
// The panel edits a domain only through this; the owners never know the panel exists.
public sealed class TuningDomain<TPreset> where TPreset : class, ITuningPreset<TPreset> {
    private readonly Func<TPreset> capture;
    private readonly Action<TPreset> apply;

    public TuningDomain((string name, IReadOnlyList<TuningSection<TPreset>> sections) description, (Func<TPreset> capture, Action<TPreset> apply, Func<TPreset> codeDefaults) model, PresetStore<TPreset> store) {
        Name = description.name;
        Sections = description.sections;
        capture = model.capture;
        apply = model.apply;
        CodeDefaults = model.codeDefaults;
        Store = store;
        Defaults = capture();
    }

    public string Name { get; }
    public IReadOnlyList<TuningSection<TPreset>> Sections { get; }
    public PresetStore<TPreset> Store { get; }
    // The settings the owners had when the session started (the scene's).
    public TPreset Defaults { get; }
    // The settings as the code defines them, whatever the scene has serialized.
    public Func<TPreset> CodeDefaults { get; }

    // The settings as currently applied to their owners.
    public TPreset Capture() => capture();

    public void Apply(TPreset preset) => apply(preset);
}
