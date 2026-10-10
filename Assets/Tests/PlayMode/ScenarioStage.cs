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
    // The top of the test track.
    public const float GroundTop = -5.21f;
    private const float TrackHalfLength = 400f;
    // How thick the one-way platforms built for tests are.
    public const float OneWayThickness = 0.4f;
    // Names of the virtual devices a stage adds (the Input System numbers repeats: ScenarioKeyboard1, ...).
    private const string DevicePrefix = "Scenario";

    private readonly HashSet<Key> heldKeys = new HashSet<Key>();
    private Keyboard keyboard;
    private Mouse mouse;
    private MouseState heldMouse;
    private LocomotionInspector inspector;
    private (InputSettings.EditorInputBehaviorInPlayMode editor, InputSettings.BackgroundBehavior background) savedInputSettings;

    public CharacterDriver Character { get; private set; }
    public ScenarioSampler Sampler { get; private set; }
    public StrideController Stride { get; private set; }
    // The debug tools' view of the character (telemetry, tuning, gait preview), built on first use.
    public LocomotionInspector Inspector => inspector ?? (inspector = new LocomotionInspector(Character));

    // keepTheLevel: leave the scene's own blocks in place instead of swapping them for the flat test track (for scenarios
    // about the level itself, such as its shadows).
    public IEnumerator Load(bool keepTheLevel = false) {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        Time.captureDeltaTime = FrameTime;
        RouteAllInputToTheGame();
        RemoveStaleDevices();
        keyboard = InputSystem.AddDevice<Keyboard>(DevicePrefix + "Keyboard");
        mouse = InputSystem.AddDevice<Mouse>(DevicePrefix + "Mouse");
        if (!keepTheLevel) {
            ReplaceLevelWithTestTrack();
        }
        Character = Object.FindAnyObjectByType<CharacterDriver>();
        inspector = null;
        Stride = Object.FindAnyObjectByType<StrideController>();
        Sampler = new GameObject("scenarioSampler").AddComponent<ScenarioSampler>();
        Sampler.ResendHeldInput = ResendHeldInput;
        yield return null;
    }

    public void Unload() {
        Time.captureDeltaTime = 0f;
        if (Sampler != null) {
            Sampler.ResendHeldInput = null;
        }
        if (keyboard != null && keyboard.added) {
            InputSystem.RemoveDevice(keyboard);
        }
        if (mouse != null && mouse.added) {
            InputSystem.RemoveDevice(mouse);
        }
        keyboard = null;
        mouse = null;
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

    // A wall standing on the track: its face toward the character at faceX, the block reaching away from it toward side
    // (+1 the wall is right of the face's open side, -1 left). Returns the top of the wall.
    public float AddWall((float faceX, int side, float height) wall) {
        const float Thickness = 4f;
        CreateBlock(new Vector2(wall.faceX + wall.side * Thickness / 2f, GroundTop + wall.height / 2f), new Vector2(Thickness, wall.height));
        return GroundTop + wall.height;
    }

    // A thin one-way platform (OneWayPlatform) from startX, its top the given height above the track: the character lands
    // on it from above and passes through it from below and the sides. Returns its top.
    public float AddOneWayPlatform((float startX, float height, float width) platform) {
        float top = GroundTop + platform.height;
        GameObject block = CreateBlock(new Vector2(platform.startX + platform.width / 2f, top - OneWayThickness / 2f), new Vector2(platform.width, OneWayThickness));
        block.AddComponent<OneWayPlatform>();
        return top;
    }

    // A hill on the track from startX: a ramp rising at the given angle (degrees) to a flat top the given height above the
    // track and topWidth wide, then a ramp down at the same angle. One PolygonCollider2D, like the playground's hill.
    public Hill AddHill((float startX, float height, float angle, float topWidth) hill) {
        Hill made = new Hill((hill.startX, GroundTop), (hill.height, hill.angle, hill.topWidth));
        GameObject block = new GameObject("scenarioHill");
        block.AddComponent<PolygonCollider2D>().points = made.Outline();
        block.AddComponent<GroundPolygonMesh>().Configure((null, new Color(0.35f, 0.38f, 0.45f, 1f)));
        return made;
    }

    // A crate (PushableCrate: a dynamic body) standing on the track, its face toward the character at faceX and the crate
    // reaching away from it toward side (+1 right of the face, -1 left). Returns its body.
    public Rigidbody2D AddCrate((float faceX, int side) face, (Vector2 size, float mass) crate) {
        GameObject box = new GameObject("scenarioCrate");
        box.transform.position = new Vector2(face.faceX + face.side * crate.size.x / 2f, GroundTop + crate.size.y / 2f);
        box.AddComponent<SpriteRenderer>();
        PushableCrate pushable = box.AddComponent<PushableCrate>();
        pushable.Configure(crate);
        return pushable.Body;
    }

    // Lifts the whole character straight up into the open air; it falls from there.
    public void Lift(float height) => Character.transform.position += Vector3.up * height;

    // A block hanging over the track from startX whose underside is the given height above it: a low ceiling.
    public void AddCeiling((float startX, float clearance, float width) ceiling) {
        const float Thickness = 3f;
        CreateBlock(new Vector2(ceiling.startX + ceiling.width / 2f, GroundTop + ceiling.clearance + Thickness / 2f), new Vector2(ceiling.width, Thickness));
    }

    // Lifts the character onto a new platform the given height above the track and returns when it stands there: for
    // falls from a height. The platform runs from behind the character to widthAhead in front of it.
    // oneWay: a thin one-way platform (OneWayPlatform) instead of a solid block.
    public void PlaceOnPlatform((float height, float widthAhead) platform, bool oneWay = false) {
        const float Behind = 6f;
        Transform root = Character.transform;
        float x = Stride.transform.position.x;
        float top = GroundTop + platform.height;
        float thickness = oneWay ? OneWayThickness : 1f;
        GameObject block = CreateBlock(new Vector2(x + (platform.widthAhead - Behind) / 2f, top - thickness / 2f), new Vector2(platform.widthAhead + Behind, thickness));
        if (oneWay) {
            block.AddComponent<OneWayPlatform>();
        }
        root.position += Vector3.up * platform.height;
        Stride.TrySettleStance((x, 1, top));
        Stride.BeginGroundedMotion(Vector2.zero);
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

    // Holds the right mouse button (aim) with the pointer at a world point. The button goes through the virtual mouse; the
    // pointer position does not: it is converted with the camera and Screen the stance will use when it reads it that frame,
    // so the Editor's Game view size and layout cannot move the aim.
    public void AimAt(Vector2 worldPoint) => AimAndFire(worldPoint, false);

    // Aims at a world point and presses (or releases) the fire button, so alternating calls fire a shot each press. Aim
    // draws the pistol first (about drawDuration): one press made during the draw is buffered and fires when the pistol
    // reaches the aim, the rest are lost, so a test that counts shots or needs a shot on a given frame draws first
    // (DrawAt).
    public void AimAndFire(Vector2 worldPoint, bool fire) {
        Character.ScenarioPointerSource = () => Camera.main.WorldToScreenPoint(worldPoint);
        QueueMouse(Aiming(fire));
    }

    // Holds aim at a world point (given relative to the body each frame, so a moving character keeps its aim) until the
    // pistol is drawn to the aim.
    public IEnumerator DrawAt(System.Func<Vector2> worldPoint, int maxFrames = 120) {
        PistolHolder holder = Character.Parts.PistolHolder;
        for (int frame = 0; frame < maxFrames && !holder.IsEquipped; frame++) {
            AimAndFire(worldPoint(), false);
            yield return null;
        }
    }

    // Holds aim with the pointer at a fixed screen position, so a camera that moves does not drag the pointer along.
    public void AimAtScreen(Vector2 screen, bool fire) {
        Character.ScenarioPointerSource = () => screen;
        QueueMouse(Aiming(fire));
    }

    // The screen position of a world point under the camera as it is now.
    public static Vector2 ScreenPointOf(Vector2 worldPoint) => Camera.main.WorldToScreenPoint(worldPoint);

    // The pointer source stays: the button release reaches the game a frame later, and for that frame it still reads the
    // pointer, which must not jump to the real mouse (and turn the character toward it). Nothing reads it once aim is released.
    public void StopAiming() {
        QueueMouse(new MouseState());
    }

    private static MouseState Aiming(bool fire) {
        MouseState state = new MouseState().WithButton(MouseButton.Right);
        return fire ? state.WithButton(MouseButton.Left) : state;
    }

    private void QueueMouse(MouseState state) {
        heldMouse = state;
        InputSystem.QueueStateEvent(mouse, state);
    }

    // The Input System resets virtual devices whenever the Editor application is activated (the user clicking back into
    // it mid-run), which would silently drop held keys and buttons; every frame the held state is sent again.
    private void ResendHeldInput() {
        QueueKeyboard();
        InputSystem.QueueStateEvent(mouse, heldMouse);
    }

    private void QueueKeyboard() {
        Key[] keys = new Key[heldKeys.Count];
        heldKeys.CopyTo(keys);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    // Virtual devices a run left behind (a test aborted before its TearDown, or a stage loaded twice): each pair adds
    // controls to every binding that matches it, and enough of them overflow the Input System's per-binding control limit.
    private static void RemoveStaleDevices() {
        List<InputDevice> stale = new List<InputDevice>();
        foreach (InputDevice device in InputSystem.devices) {
            if (device.name.StartsWith(DevicePrefix, System.StringComparison.Ordinal)) {
                stale.Add(device);
            }
        }
        foreach (InputDevice device in stale) {
            InputSystem.RemoveDevice(device);
        }
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
        // The level's sloped ground (the playground hill) too.
        foreach (PolygonCollider2D ground in Object.FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None)) {
            ground.gameObject.SetActive(false);
        }
        CreateBlock(new Vector2(0f, GroundTop - 0.5f), new Vector2(2f * TrackHalfLength, 1f));
    }

    private static GameObject CreateBlock(Vector2 center, Vector2 size) {
        GameObject block = new GameObject("scenarioGround");
        block.transform.position = center;
        block.AddComponent<BoxCollider2D>().size = size;
        return block;
    }
}
