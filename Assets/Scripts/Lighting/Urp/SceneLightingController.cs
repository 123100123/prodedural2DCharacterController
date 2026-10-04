using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The scene's lighting rig: a global ambient light that keeps everything readable, a soft light that follows the
// character, and the muzzle flash. Any 2D light placed in the scene by hand works alongside it (an existing global light
// is adopted instead of adding a second). Created by LightingBootstrap when a scene has none, with the default settings;
// add one to a scene to own the settings there.
[DisallowMultipleComponent]
public sealed class SceneLightingController : MonoBehaviour, ILightingTarget {
    [SerializeField] private LightingSettings settings = new LightingSettings();
    [SerializeField] private CharacterDriver character;

    private Light2D ambient;
    private Light2D player;
    private MuzzleFlashLight muzzle;

    public LightingSettings Settings => settings;
    public MuzzleFlashLight Muzzle => muzzle;
    public float PlayerIntensity => player != null && player.enabled ? player.intensity : 0f;
    public float MuzzleIntensity => muzzle != null ? muzzle.Intensity : 0f;
    public Vector2 MuzzlePosition => muzzle != null ? (Vector2)muzzle.transform.position : Vector2.zero;
    public int MuzzleShots => muzzle != null ? muzzle.Shots : 0;

    // The controller of a scene that has none gets its character before it wakes.
    public void Bind(CharacterDriver driver) => character = driver;

    private void Awake() {
        if (character == null) {
            character = FindAnyObjectByType<CharacterDriver>();
        }
        ambient = AdoptOrCreateGlobalLight();
        player = CreatePlayerLight();
        muzzle = CreateMuzzleFlash();
        Apply();
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
        player.transform.localPosition = new Vector3(0f, settings.playerHeight, 0f);

        muzzle.Refresh();
    }

    private Light2D AdoptOrCreateGlobalLight() {
        foreach (Light2D existing in FindObjectsByType<Light2D>(FindObjectsSortMode.None)) {
            if (existing.lightType == Light2D.LightType.Global) {
                return existing;
            }
        }
        Light2D created = CreateLight("Ambient light", transform, Light2D.LightType.Global);
        return created;
    }

    private Light2D CreatePlayerLight() {
        Transform parent = character != null ? character.Parts.Stride.transform : transform;
        return CreateLight("Player light", parent, Light2D.LightType.Point);
    }

    private MuzzleFlashLight CreateMuzzleFlash() {
        Light2D flash = CreateLight("Muzzle flash light", transform, Light2D.LightType.Point);
        MuzzleFlashLight controller = flash.gameObject.AddComponent<MuzzleFlashLight>();
        controller.Initialize((flash, character, settings));
        return controller;
    }

    // A 2D light that affects every sorting layer (a light made in code affects none until told to).
    private static Light2D CreateLight(string name, Transform parent, Light2D.LightType type) {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent, false);
        Light2D light = host.AddComponent<Light2D>();
        light.lightType = type;
        List<int> layers = new List<int>();
        foreach (SortingLayer layer in SortingLayer.layers) {
            layers.Add(layer.id);
        }
        light.targetSortingLayers = layers.ToArray();
        return light;
    }
}
