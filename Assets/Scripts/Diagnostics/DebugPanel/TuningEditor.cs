using System.Collections.Generic;
using UnityEngine.UIElements;

// Edits a working copy of one domain's settings through rows built from its schema, and applies every change to the
// owners at once. Resets restore the settings the session started with.
public sealed class TuningEditor<TPreset> where TPreset : class, ITuningPreset<TPreset> {
    private readonly TuningDomain<TPreset> domain;
    private readonly List<TuningRow<TPreset>> rows = new List<TuningRow<TPreset>>();
    private TPreset working;

    public TuningEditor(TuningDomain<TPreset> domain) {
        this.domain = domain;
        working = domain.Capture();
    }

    // A copy of the settings as currently tuned.
    public TPreset Current => working.Copy();

    // The section's rows, under a foldout per group.
    public VisualElement BuildSection(TuningSection<TPreset> section) {
        ScrollView scroll = new ScrollView();
        scroll.AddToClassList("loco-scroll");
        Foldout group = null;
        foreach (TuningParameter<TPreset> parameter in section.Parameters) {
            if (group == null || group.text != parameter.Group) {
                group = new Foldout { text = parameter.Group };
                group.AddToClassList("loco-group");
                scroll.Add(group);
            }
            group.Add(CreateRow(parameter));
        }
        return scroll;
    }

    public void ResetSection(TuningSection<TPreset> section) {
        foreach (TuningParameter<TPreset> parameter in section.Parameters) {
            parameter.WriteTo(working, parameter.ReadFrom(domain.Defaults));
        }
        ApplyAndShow();
    }

    public void ResetAll() => Load(domain.Defaults);

    public void Load(TPreset preset) {
        working = preset.Copy();
        ApplyAndShow();
    }

    private TuningRow<TPreset> CreateRow(TuningParameter<TPreset> parameter) {
        TuningRow<TPreset> row = new TuningRow<TPreset>(parameter);
        row.Show(parameter.ReadFrom(working));
        row.Changed += ApplyEdit;
        rows.Add(row);
        return row;
    }

    private void ApplyEdit(TuningRow<TPreset> edited, float value) {
        edited.Parameter.WriteTo(working, value);
        domain.Apply(working);
        ShowWorkingValues();
    }

    private void ApplyAndShow() {
        domain.Apply(working);
        ShowWorkingValues();
    }

    private void ShowWorkingValues() {
        foreach (TuningRow<TPreset> row in rows) {
            row.Show(row.Parameter.ReadFrom(working));
        }
    }
}
