using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Runtime Game View panel for watching and tuning a character's locomotion: opens and closes with a key, sits on top
// of the game, and keeps clicks over it and typing into its fields away from gameplay. It only talks to the
// character through a LocomotionInspector it builds from the character's parts; the locomotion never depends on it,
// and hidden it does nothing but wait for its key.
public sealed class LocomotionDebugPanel : MonoBehaviour, UiInputCapture.ICapture {
    private const string ThemePath = "LocomotionDebug/LocomotionDebugTheme";
    private const string StylePath = "LocomotionDebug/LocomotionDebugPanel";
    // Above gameplay UI.
    private const int SortingOrder = 1000;

    [SerializeField] private CharacterDriver character;
    [SerializeField] private Key toggleKey = Key.F1;
    [SerializeField] private bool isVisibleOnStart;
    [Tooltip("Seconds between refreshes of the live readout.")]
    [SerializeField] private float refreshInterval = 0.1f;

    private PanelSettings panelSettings;
    private UIDocument document;
    private LocomotionInspector inspector;
    private DebugPanelLayout layout;
    private bool isVisible;
    private bool isFieldClaimedByClick;
    private float nextRefresh;

    public bool IsPointerOverUi => isVisible && IsPointerOverPanel();
    public bool IsTyping => isVisible && isFieldClaimedByClick && IsTextFieldFocused();

    private void Start() {
        if (character == null) {
            Debug.LogWarning($"{name}: no character to inspect.", this);
            enabled = false;
            return;
        }
        inspector = new LocomotionInspector(character, FindLighting());
        document = CreateDocument();
        layout = new DebugPanelLayout(inspector);
        document.rootVisualElement.Add(layout.Root);
        layout.Root.RegisterCallback<PointerDownEvent>(_ => isFieldClaimedByClick = true, TrickleDown.TrickleDown);
        SetVisible(isVisibleOnStart);
        UiInputCapture.Register(this);
    }

    private void OnDestroy() {
        UiInputCapture.Unregister(this);
        if (panelSettings != null) {
            Destroy(panelSettings);
        }
    }

    private void Update() {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) {
            SetVisible(!isVisible);
        }
        if (!isVisible) {
            return;
        }
        KeepKeyboardForGameplayUnlessAFieldWasClicked();
        LocomotionTelemetry telemetry = inspector.ReadTelemetry();
        LocomotionDebugDraw.Draw((telemetry, layout.DrawLayers));
        if (Time.unscaledTime >= nextRefresh) {
            nextRefresh = Time.unscaledTime + refreshInterval;
            layout.ShowTelemetry(telemetry);
        }
    }

    // The scene's lighting rig, if it has one (it exists only under the Universal Render Pipeline).
    private static ILightingTarget FindLighting() {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) {
            if (behaviour is ILightingTarget lighting) {
                return lighting;
            }
        }
        return null;
    }

    private void SetVisible(bool visible) {
        isVisible = visible;
        layout.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        ReleaseKeyboard();
    }

    // A field only holds the keyboard after the player clicks into it; UI Toolkit also hands focus to a field when key
    // presses arrive with nothing focused, and gameplay keys must keep working then. Clicking outside gives it back.
    private void KeepKeyboardForGameplayUnlessAFieldWasClicked() {
        bool clickedOutside = Pointer.current != null && Pointer.current.press.wasPressedThisFrame && !IsPointerOverPanel();
        if (clickedOutside || (!isFieldClaimedByClick && IsTextFieldFocused())) {
            ReleaseKeyboard();
        }
    }

    private void ReleaseKeyboard() {
        isFieldClaimedByClick = false;
        (document.rootVisualElement.panel?.focusController?.focusedElement as VisualElement)?.Blur();
    }

    // The document lives on its own child so its panel settings are set before it first builds.
    private UIDocument CreateDocument() {
        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemePath);
        panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        panelSettings.sortingOrder = SortingOrder;

        GameObject host = new GameObject("LocomotionDebugDocument");
        host.SetActive(false);
        host.transform.SetParent(transform, false);
        UIDocument created = host.AddComponent<UIDocument>();
        created.panelSettings = panelSettings;
        host.SetActive(true);

        created.rootVisualElement.pickingMode = PickingMode.Ignore;
        StyleSheet style = Resources.Load<StyleSheet>(StylePath);
        if (style != null) {
            created.rootVisualElement.styleSheets.Add(style);
        }
        return created;
    }

    private bool IsPointerOverPanel() {
        if (Pointer.current == null || document.rootVisualElement.panel == null) {
            return false;
        }
        Vector2 screen = Pointer.current.position.ReadValue();
        Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(screen.x, Screen.height - screen.y));
        VisualElement picked = document.rootVisualElement.panel.Pick(panelPoint);
        return picked != null && picked != document.rootVisualElement;
    }

    private bool IsTextFieldFocused() {
        VisualElement focused = document.rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
        return focused != null && (focused is TextField || focused is FloatField
                                   || focused.GetFirstAncestorOfType<TextField>() != null
                                   || focused.GetFirstAncestorOfType<FloatField>() != null);
    }
}
