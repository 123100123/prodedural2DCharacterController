using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Loads the sample scene, swaps its short level for a long test track and drives the character through virtual
// input devices at a fixed 60 fps, so scenarios are repeatable without a human at the keyboard.
public sealed class ScenarioStage {
    public const float FrameTime = 1f / 60f;
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const float GroundTop = -5.21f;
    private const float TrackHalfLength = 400f;

    private readonly HashSet<Key> heldKeys = new HashSet<Key>();
    private Keyboard keyboard;
    private Mouse mouse;
    private LocomotionInspector inspector;
    private (InputSettings.EditorInputBehaviorInPlayMode editor, InputSettings.BackgroundBehavior background) savedInputSettings;

    public CharacterDriver Character { get; private set; }
    public ScenarioSampler Sampler { get; private set; }
    public StrideController Stride { get; private set; }
    // The debug tools' view of the character (telemetry, tuning, gait preview), built on first use.
    public LocomotionInspector Inspector => inspector ?? (inspector = new LocomotionInspector(Character));

    public IEnumerator Load() {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        Time.captureDeltaTime = FrameTime;
        RouteAllInputToTheGame();
        keyboard = InputSystem.AddDevice<Keyboard>("ScenarioKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("ScenarioMouse");
        ReplaceLevelWithTestTrack();
        Character = Object.FindAnyObjectByType<CharacterDriver>();
        inspector = null;
        Stride = Object.FindAnyObjectByType<StrideController>();
        Sampler = new GameObject("scenarioSampler").AddComponent<ScenarioSampler>();
        yield return null;
    }

    public void Unload() {
        Time.captureDeltaTime = 0f;
        if (keyboard != null) {
            InputSystem.RemoveDevice(keyboard);
        }
        if (mouse != null) {
            InputSystem.RemoveDevice(mouse);
        }
        InputSystem.settings.editorInputBehaviorInPlayMode = savedInputSettings.editor;
        InputSystem.settings.backgroundBehavior = savedInputSettings.background;
    }

    // Raised steps of the given height every spacing units along x, for uneven-ground scenarios.
    public void AddBumps((float startX, float endX) span, (float height, float width, float spacing) bump) {
        for (float x = span.startX; x < span.endX; x += bump.spacing) {
            CreateBlock(new Vector2(x + bump.width / 2f, GroundTop + bump.height / 2f), new Vector2(bump.width, bump.height));
        }
    }

    // A raised block starting at startX, its top the given height above the track: a ledge to climb.
    public float AddLedge((float startX, float height, float width) ledge) {
        CreateBlock(new Vector2(ledge.startX + ledge.width / 2f, GroundTop + ledge.height / 2f), new Vector2(ledge.width, ledge.height));
        return GroundTop + ledge.height;
    }

    public void Press(params Key[] keys) {
        heldKeys.UnionWith(keys);
        QueueKeyboard();
    }

    public void Release(params Key[] keys) {
        heldKeys.ExceptWith(keys);
        QueueKeyboard();
    }

    public void ReleaseAll() {
        heldKeys.Clear();
        QueueKeyboard();
    }

    // Holds the right mouse button (aim) with the pointer at a world point.
    public void AimAt(Vector2 worldPoint) {
        Camera camera = Camera.main;
        Vector2 screen = camera.WorldToScreenPoint(worldPoint);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Right));
    }

    public void StopAiming() => InputSystem.QueueStateEvent(mouse, new MouseState());

    private void QueueKeyboard() {
        Key[] keys = new Key[heldKeys.Count];
        heldKeys.CopyTo(keys);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    // Without Game view focus the editor would otherwise drop keyboard input and reset devices.
    private void RouteAllInputToTheGame() {
        InputSettings settings = InputSystem.settings;
        savedInputSettings = (settings.editorInputBehaviorInPlayMode, settings.backgroundBehavior);
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    private static void ReplaceLevelWithTestTrack() {
        foreach (BoxCollider2D ground in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)) {
            ground.gameObject.SetActive(false);
        }
        CreateBlock(new Vector2(0f, GroundTop - 0.5f), new Vector2(2f * TrackHalfLength, 1f));
    }

    private static void CreateBlock(Vector2 center, Vector2 size) {
        GameObject block = new GameObject("scenarioGround");
        block.transform.position = center;
        block.AddComponent<BoxCollider2D>().size = size;
    }
}
