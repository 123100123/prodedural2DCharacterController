using UnityEngine;
using UnityEngine.Rendering.Universal;

// The sun (or moon): a Freeform light that covers the view and throws near-parallel shadows from a pivot far away
// toward it (SunGeometry). It follows the camera so it always covers the view and keeps its direction, and its shape is
// rebuilt only when the angle, distance or coverage change.
public sealed class SunRig {
    private readonly Light2D light;
    private (float angle, float distance, Vector2 coverage) shape = (float.NaN, 0f, Vector2.zero);

    public SunRig(Transform parent) {
        light = Light2DFactory.Create("Sun", parent, Light2D.LightType.Freeform);
    }

    public float Intensity => light.enabled ? light.intensity : 0f;
    public Vector2 Pivot => light.transform.position;
    public bool CastsShadows => light.enabled && light.shadowsEnabled && light.shadowIntensity > 0f;

    public void Apply(LightingSettings settings) {
        light.enabled = settings.sunEnabled;
        light.color = settings.sunColor;
        light.intensity = settings.sunIntensity;
        light.shapeLightFalloffSize = Mathf.Max(0.1f, settings.sunEdgeFalloff);
        light.shadowsEnabled = settings.sunShadowStrength > 0f;
        light.shadowIntensity = settings.sunShadowStrength;
        light.shadowSoftness = settings.sunShadowSoftness;
        light.shadowSoftnessFalloffIntensity = settings.sunShadowSoftnessFalloff;
        RebuildShapeIfChanged(settings);
    }

    // Centres the sun's cover on the view (called every frame, after the camera has moved).
    public void Follow(Vector2 viewCenter, LightingSettings settings) {
        Vector2 pivot = SunGeometry.Pivot(viewCenter, (settings.sunAngle, settings.sunDistance));
        light.transform.position = new Vector3(pivot.x, pivot.y, 0f);
    }

    private void RebuildShapeIfChanged(LightingSettings settings) {
        (float angle, float distance, Vector2 coverage) wanted = (settings.sunAngle, settings.sunDistance, settings.sunCoverage);
        if (wanted.angle == shape.angle && wanted.distance == shape.distance && wanted.coverage == shape.coverage) {
            return;
        }
        shape = wanted;
        light.SetShapePath(SunGeometry.LocalShape((wanted.angle, wanted.distance), wanted.coverage));
    }
}
