using System.Collections.Generic;
using UnityEngine.UIElements;

// Edits a working copy of the locomotion settings through rows built from the tuning schema, and applies every change
// to the character at once. Resets restore the settings the character started the session with.
public sealed class TuningEditor {
    private readonly LocomotionInspector inspector;
    private readonly List<TuningRow> rows = new List<TuningRow>();
    private LocomotionPreset working;

    public TuningEditor(LocomotionInspector inspector) {
        this.inspector = inspector;
        working = inspector.Tuning.Capture();
    }

    // A copy of the settings as currently tuned.
    public LocomotionPreset Current => working.Copy();

    // The section's rows, under a foldout per group.
    public VisualElement BuildSection(LocomotionTuningSchema.Section section) {
        ScrollView scroll = new ScrollView();
        scroll.AddToClassList("loco-scroll");
        Foldout group = null;
        foreach (TuningParameter parameter in section.Parameters) {
            if (group == null || group.text != parameter.Group) {
                group = new Foldout { text = parameter.Group };
                group.AddToClassList("loco-group");
                scroll.Add(group);
            }
            group.Add(CreateRow(parameter));
        }
        return scroll;
    }

    public void ResetSection(LocomotionTuningSchema.Section section) {
        foreach (TuningParameter parameter in section.Parameters) {
            parameter.WriteTo(working, parameter.ReadFrom(inspector.Defaults));
        }
        ApplyAndShow();
    }

    public void ResetAll() => Load(inspector.Defaults);

    public void Load(LocomotionPreset preset) {
        working = preset.Copy();
        ApplyAndShow();
    }

    private TuningRow CreateRow(TuningParameter parameter) {
        TuningRow row = new TuningRow(parameter);
        row.Show(parameter.ReadFrom(working));
        row.Changed += ApplyEdit;
        rows.Add(row);
        return row;
    }

    private void ApplyEdit(TuningRow edited, float value) {
        edited.Parameter.WriteTo(working, value);
        inspector.Tuning.Apply(working);
        ShowWorkingValues();
    }

    private void ApplyAndShow() {
        inspector.Tuning.Apply(working);
        ShowWorkingValues();
    }

    // Some values appear in two sections (e.g. the base leans); every row shows the current value.
    private void ShowWorkingValues() {
        foreach (TuningRow row in rows) {
            row.Show(row.Parameter.ReadFrom(working));
        }
    }
}
