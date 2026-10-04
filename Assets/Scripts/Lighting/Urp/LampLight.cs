using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A lamp placed in the scene (Assets/Prefabs/Lamp.prefab): a point light with shadows, made at startup. The lighting
// rig gives every lamp the shared Lamps settings (colour, intensity, radii, shadows), scaled by this lamp's own intensity
// scale; place as many as you like.
public sealed class LampLight : MonoBehaviour {
    private static readonly List<LampLight> Active = new List<LampLight>();

    [Tooltip("This lamp's brightness as a multiple of the shared lamp intensity.")]
    [SerializeField] private float intensityScale = 1f;

    private Light2D lamp;

    public static IReadOnlyList<LampLight> All => Active;
    public float Intensity => lamp != null && lamp.enabled ? lamp.intensity : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => Active.Clear();

    private void Awake() => lamp = Light2DFactory.Create("Lamp light", transform, Light2D.LightType.Point);

    private void OnEnable() {
        Active.Add(this);
        SceneLightingController.Current?.ApplyToLamp(this);
    }

    private void OnDisable() => Active.Remove(this);

    public void Apply(LightingSettings settings) {
        if (lamp == null) {
            return;
        }
        lamp.enabled = settings.lampsEnabled;
        lamp.color = settings.lampColor;
        lamp.intensity = settings.lampIntensity * intensityScale;
        lamp.pointLightOuterRadius = settings.lampOuterRadius;
        lamp.pointLightInnerRadius = Mathf.Min(settings.lampInnerRadius, settings.lampOuterRadius - 0.01f);
        lamp.shadowsEnabled = settings.lampShadowStrength > 0f;
        lamp.shadowIntensity = settings.lampShadowStrength;
        lamp.shadowSoftness = settings.lampShadowSoftness;
    }
}
