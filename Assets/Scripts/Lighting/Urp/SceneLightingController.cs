using UnityEngine;
using UnityEngine.Rendering.Universal;

// The scene's lighting rig: a global ambient light that keeps everything readable, a sun that throws shadows across the
// level and the background, a soft light that follows the character, the muzzle flash, and the shared settings of the
// lamps placed in the scene. Any 2D light placed in the scene by hand works alongside it (an existing global light is
// adopted instead of adding a second). Created by LightingBootstrap when a scene has none, with the default settings;
// add one to a scene to own the settings there.
[DisallowMultipleComponent]
public sealed class SceneLightingController : MonoBehaviour, ILightingTarget {
    [SerializeField] private LightingSettings settings = new LightingSettings();
    [SerializeField] private CharacterDriver character;

    private Light2D ambient;
    private Light2D player;
    private SunRig sun;
    private MuzzleFlashLight muzzle;
    private Camera viewCamera;
    private int shadowCasterCount;

    // The rig of the loaded scene, which lamps that wake later go to for their settings.
    public static SceneLightingController Current { get; private set; }

    public LightingSettings Settings => settings;
    public MuzzleFlashLight Muzzle => muzzle;
    public float PlayerIntensity => player != null && player.enabled ? player.intensity : 0f;
    public float MuzzleIntensity => muzzle != null ? muzzle.Intensity : 0f;
    public Vector2 MuzzlePosition => muzzle != null ? (Vector2)muzzle.transform.position : Vector2.zero;
    public int MuzzleShots => muzzle != null ? muzzle.Shots : 0;
    public float SunIntensity => sun != null ? sun.Intensity : 0f;
    public Vector2 SunPivot => sun != null ? sun.Pivot : Vector2.zero;
    public bool SunCastsShadows => sun != null && sun.CastsShadows;
    public int LampCount => LampLight.All.Count;
    public int ShadowCasterCount => shadowCasterCount;

    // The controller of a scene that has none gets its character before it wakes.
    public void Bind(CharacterDriver driver) => character = driver;

    private void Awake() {
        Current = this;
        if (character == null) {
            character = FindAnyObjectByType<CharacterDriver>();
        }
        viewCamera = Camera.main;
        ambient = AdoptOrCreateGlobalLight();
        player = CreatePlayerLight();
        sun = new SunRig(transform);
        muzzle = CreateMuzzleFlash();
        Apply();
    }

    private void OnDestroy() {
        if (Current == this) {
            Current = null;
        }
    }

    // The sun covers the view from wherever the camera is, once the camera has moved this frame.
    private void LateUpdate() {
        if (sun == null || viewCamera == null) {
            return;
        }
        sun.Follow(viewCamera.transform.position, settings);
    }

    public void Apply() {
        if (ambient == null) {
            return;
        }
        ambient.color = settings.ambientColor;
        ambient.intensity = settings.ambientIntensity;

        player.enabled = settings.playerEnabled;
        player.color = settings.playerColor;
        player.intensity = settings.playerIntensity;
        player.pointLightOuterRadius = settings.playerOuterRadius;
        player.pointLightInnerRadius = Mathf.Min(settings.playerInnerRadius, settings.playerOuterRadius - 0.01f);
        player.falloffIntensity = settings.playerFalloff;
        player.shadowsEnabled = settings.playerCastsShadows;
        player.shadowIntensity = settings.lampShadowStrength;
        player.transform.localPosition = new Vector3(0f, settings.playerHeight, 0f);

        sun.Apply(settings);
        foreach (LampLight lamp in LampLight.All) {
            lamp.Apply(settings);
        }
        muzzle.Refresh();
        shadowCasterCount = CountShadowCasters();
    }

    // A lamp that woke after the rig: give it the shared settings.
    public void ApplyToLamp(LampLight lamp) => lamp.Apply(settings);

    private Light2D AdoptOrCreateGlobalLight() {
        foreach (Light2D existing in FindObjectsByType<Light2D>(FindObjectsSortMode.None)) {
            if (existing.lightType == Light2D.LightType.Global) {
                return existing;
            }
        }
        return Light2DFactory.Create("Ambient light", transform, Light2D.LightType.Global);
    }

    private Light2D CreatePlayerLight() {
        Transform parent = character != null ? character.Parts.Stride.transform : transform;
        return Light2DFactory.Create("Player light", parent, Light2D.LightType.Point);
    }

    private MuzzleFlashLight CreateMuzzleFlash() {
        Light2D flash = Light2DFactory.Create("Muzzle flash light", transform, Light2D.LightType.Point);
        MuzzleFlashLight controller = flash.gameObject.AddComponent<MuzzleFlashLight>();
        controller.Initialize((flash, character, settings));
        return controller;
    }

    private static int CountShadowCasters() {
        return FindObjectsByType<ShadowCaster2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
