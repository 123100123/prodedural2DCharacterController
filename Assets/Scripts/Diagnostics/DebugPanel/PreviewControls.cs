using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

// Chooses the gait preview: Auto follows the player; Idle, Walk and Run override the target speed, optionally at a
// chosen speed, so a gait (or the walk/run transition region) can be inspected consistently.
public sealed class PreviewControls {
    private const float MaxPreviewSpeed = 15f;

    private readonly LocomotionInspector inspector;
    private readonly RadioButtonGroup modes;
    private readonly Toggle overrideSpeed;
    private readonly Slider speed;

    public PreviewControls(LocomotionInspector inspector) {
        this.inspector = inspector;
        Root = new VisualElement();
        Root.AddToClassList("loco-preview");

        modes = new RadioButtonGroup("Preview", new List<string>(Enum.GetNames(typeof(GaitPreview.Mode)))) { value = 0 };
        modes.AddToClassList("loco-preview__modes");
        modes.RegisterValueChangedCallback(_ => ApplyPreview());
        Root.Add(modes);

        overrideSpeed = new Toggle("Override speed");
        overrideSpeed.RegisterValueChangedCallback(_ => ApplyPreview());
        Root.Add(overrideSpeed);

        speed = new Slider("Preview speed (u/s)", 0f, MaxPreviewSpeed) { value = 3.5f, showInputField = true };
        speed.RegisterValueChangedCallback(_ => ApplyPreview());
        Root.Add(speed);
    }

    public VisualElement Root { get; }

    private void ApplyPreview() {
        GaitPreview.Mode mode = (GaitPreview.Mode)Math.Max(0, modes.value);
        inspector.Preview = new GaitPreview((mode, overrideSpeed.value ? speed.value : (float?)null));
    }
}
