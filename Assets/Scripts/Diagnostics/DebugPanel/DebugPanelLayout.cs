using System.Collections.Generic;
using UnityEngine.UIElements;

// Arranges the panel: a header with the title, a badge while a gait preview overrides the input, and a collapse
// button; tabs for the live readout, each tuning domain's sections and the debug lines; and the preset toolbar of
// the domain the open tab belongs to.
public sealed class DebugPanelLayout {
    private readonly LiveTelemetryView live = new LiveTelemetryView();
    private readonly DebugDrawToggles drawToggles = new DebugDrawToggles();
    // Each tuning tab's section, and the toolbar of the domain it belongs to.
    private readonly Dictionary<Tab, object> sectionTabs = new Dictionary<Tab, object>();
    private readonly Dictionary<Tab, VisualElement> toolbarsByTab = new Dictionary<Tab, VisualElement>();
    private readonly List<VisualElement> toolbars = new List<VisualElement>();
    private readonly VisualElement body = new VisualElement();
    private readonly Label previewBadge = new Label();
    private readonly TabView tabs = new TabView();

    public DebugPanelLayout(LocomotionInspector inspector) {
        Root = new VisualElement();
        Root.AddToClassList("loco-panel");
        Root.Add(BuildHeader());

        tabs.AddToClassList("loco-tabs");
        AddTab(("LIVE", BuildLiveTab(inspector)));
        AddDomain(inspector.Movement);
        AddTab(("DEBUG", drawToggles.Root));
        body.Add(tabs);
        foreach (VisualElement toolbar in toolbars) {
            body.Add(toolbar);
        }
        tabs.activeTabChanged += (_, active) => ShowToolbarFor(active);
        ShowToolbarFor(tabs.activeTab);
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

    // One tab per section of the domain, with the domain's own editor and preset toolbar.
    private void AddDomain<TPreset>(TuningDomain<TPreset> domain) where TPreset : class, ITuningPreset<TPreset> {
        TuningEditor<TPreset> editor = new TuningEditor<TPreset>(domain);
        VisualElement toolbar = new PresetToolbar<TPreset>((editor, domain), () => CurrentSection<TPreset>()).Root;
        toolbars.Add(toolbar);
        foreach (TuningSection<TPreset> section in domain.Sections) {
            Tab tab = AddTab((section.Title, editor.BuildSection(section)));
            sectionTabs.Add(tab, section);
            toolbarsByTab.Add(tab, toolbar);
        }
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

    // The open tab's section, if it belongs to this domain.
    private TuningSection<TPreset> CurrentSection<TPreset>() {
        return tabs.activeTab != null && sectionTabs.TryGetValue(tabs.activeTab, out object section) ? section as TuningSection<TPreset> : null;
    }

    // The tabs that are not a tuning section (LIVE, DEBUG) keep the first domain's toolbar.
    private void ShowToolbarFor(Tab active) {
        VisualElement shown = active != null && toolbarsByTab.TryGetValue(active, out VisualElement toolbar) ? toolbar : toolbars[0];
        foreach (VisualElement element in toolbars) {
            element.style.display = element == shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
