using System;
using UnityEngine.UIElements;

// One tunable value in the panel: its name, a slider over its range, a number field showing the exact value (type
// into it for values the slider cannot place precisely), and its unit. A switch parameter has a checkbox instead.
public sealed class TuningRow<TPreset> : VisualElement {
    private readonly Slider slider;
    private readonly FloatField field;
    private readonly Toggle toggle;

    public TuningRow(TuningParameter<TPreset> parameter) {
        Parameter = parameter;
        AddToClassList("loco-row");
        AddLabel((parameter.Label, "loco-row__label"));

        if (parameter.IsToggle) {
            toggle = new Toggle();
            toggle.AddToClassList("loco-row__toggle");
            toggle.RegisterValueChangedCallback(change => Submit(change.newValue ? 1f : 0f));
            Add(toggle);
            return;
        }

        slider = new Slider(parameter.Min, parameter.Max);
        slider.AddToClassList("loco-row__slider");
        slider.RegisterValueChangedCallback(change => Submit(change.newValue));
        Add(slider);

        field = new FloatField { formatString = "0.###" };
        field.AddToClassList("loco-row__field");
        field.RegisterValueChangedCallback(change => Submit(change.newValue));
        Add(field);

        AddLabel((parameter.Unit, "loco-row__unit"));
    }

    public TuningParameter<TPreset> Parameter { get; }
    public event Action<TuningRow<TPreset>, float> Changed;

    // Shows a value without reporting it as an edit.
    public void Show(float value) {
        if (toggle != null) {
            toggle.SetValueWithoutNotify(value >= TuningParameter<TPreset>.ToggleThreshold);
            return;
        }
        slider.SetValueWithoutNotify(value);
        field.SetValueWithoutNotify(value);
    }

    // The checkbox of a switch parameter, null for a number. Tests drive it as a click would.
    public Toggle Switch => toggle;

    private void Submit(float value) {
        Show(value);
        Changed?.Invoke(this, value);
    }

    private void AddLabel((string text, string className) label) {
        Label element = new Label(label.text) { tooltip = label.text };
        element.AddToClassList(label.className);
        Add(element);
    }
}
