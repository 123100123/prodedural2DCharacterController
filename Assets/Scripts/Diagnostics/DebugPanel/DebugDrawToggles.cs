using System;
using UnityEngine.UIElements;

// The DEBUG tab: one toggle per visualization layer.
public sealed class DebugDrawToggles {
    public DebugDrawToggles() {
        Root = new VisualElement();
        Label hint = new Label("Lines show in the Scene view, and in the Game view while its Gizmos toggle is on.");
        hint.AddToClassList("loco-hint");
        Root.Add(hint);
        foreach (LocomotionDebugDraw.Layers layer in Enum.GetValues(typeof(LocomotionDebugDraw.Layers))) {
            if (layer != LocomotionDebugDraw.Layers.None) {
                AddToggle(layer);
            }
        }
    }

    public VisualElement Root { get; }
    public LocomotionDebugDraw.Layers Layers { get; private set; }

    private void AddToggle(LocomotionDebugDraw.Layers layer) {
        Toggle toggle = new Toggle(layer.ToString());
        toggle.RegisterValueChangedCallback(change => Layers = change.newValue ? Layers | layer : Layers & ~layer);
        Root.Add(toggle);
    }
}
