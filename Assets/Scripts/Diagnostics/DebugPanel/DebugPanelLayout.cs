using System.Collections.Generic;
using UnityEngine.UIElements;

// Arranges the panel: a header with the title, a badge while a gait preview overrides the input, and a collapse
// button; tabs for the live readout, each tuning section and the debug lines; and the preset toolbar.
public sealed class DebugPanelLayout {
    private readonly LiveTelemetryView live = new LiveTelemetryView();
    private readonly DebugDrawToggles drawToggles = new DebugDrawToggles();
    private readonly Dictionary<Tab, LocomotionTuningSchema.Section> sectionTabs = new Dictionary<Tab, LocomotionTuningSchema.Section>();
    private readonly VisualElement body = new VisualElement();
    private readonly Label previewBadge = new Label();
    private readonly TabView tabs = new TabView();

    public DebugPanelLayout(LocomotionInspector inspector) {
        Root = new VisualElement();
        Root.AddToClassList("loco-panel");
        Root.Add(BuildHeader());

        TuningEditor editor = new TuningEditor(inspector);
        tabs.AddToClassList("loco-tabs");
        AddTab(("LIVE", BuildLiveTab(inspector)));
        foreach (LocomotionTuningSchema.Section section in LocomotionTuningSchema.Build()) {
            sectionTabs.Add(AddTab((section.Title, editor.BuildSection(section))), section);
        }
        AddTab(("DEBUG", drawToggles.Root));
        body.Add(tabs);
        body.Add(new PresetToolbar(editor, CurrentSection).Root);
        Root.Add(body);
    }

    public VisualElement Root { get; }
    public LocomotionDebugDraw.Layers DrawLayers => drawToggles.Layers;

    public void ShowTelemetry(LocomotionTelemetry telemetry) {
        live.Show(telemetry);
        bool isPreviewing = telemetry.Preview.IsActive;
        previewBadge.EnableInClassList("loco-badge--visible", isPreviewing);
        previewBadge.text = isPreviewing ? $"PREVIEW: {telemetry.Preview.PreviewMode.ToString().ToUpperInvariant()}" : "";
    }

    private VisualElement BuildHeader() {
        VisualElement header = new VisualElement();
        header.AddToClassList("loco-header");
        Label title = new Label("Locomotion");
        title.AddToClassList("loco-title");
        header.Add(title);
        previewBadge.AddToClassList("loco-badge");
        header.Add(previewBadge);
        Button collapse = new Button { text = "–" };
        collapse.clicked += () => ToggleCollapsed(collapse);
        collapse.AddToClassList("loco-header__button");
        header.Add(collapse);
        return header;
    }

    private void ToggleCollapsed(Button collapse) {
        bool isCollapsed = body.style.display == DisplayStyle.None;
        body.style.display = isCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
        collapse.text = isCollapsed ? "–" : "+";
    }

    private VisualElement BuildLiveTab(LocomotionInspector inspector) {
        ScrollView scroll = new ScrollView();
        scroll.AddToClassList("loco-scroll");
        scroll.Add(new PreviewControls(inspector).Root);
        scroll.Add(live.Root);
        return scroll;
    }

    private Tab AddTab((string title, VisualElement content) page) {
        Tab tab = new Tab(page.title);
        tab.Add(page.content);
        tabs.Add(tab);
        return tab;
    }

    private LocomotionTuningSchema.Section CurrentSection() {
        return tabs.activeTab != null && sectionTabs.TryGetValue(tabs.activeTab, out LocomotionTuningSchema.Section section) ? section : null;
    }
}
