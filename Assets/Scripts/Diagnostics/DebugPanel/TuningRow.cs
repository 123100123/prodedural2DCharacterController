using System;
using UnityEngine.UIElements;

// One tunable value in the panel: its name, a slider over its range, a number field showing the exact value (type
// into it for values the slider cannot place precisely), and its unit.
public sealed class TuningRow<TPreset> : VisualElement {
    private readonly Slider slider;
    private readonly FloatField field;

    public TuningRow(TuningParameter<TPreset> parameter) {
        Parameter = parameter;
        AddToClassList("loco-row");
        AddLabel((parameter.Label, "loco-row__label"));

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
        slider.SetValueWithoutNotify(value);
        field.SetValueWithoutNotify(value);
    }

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
