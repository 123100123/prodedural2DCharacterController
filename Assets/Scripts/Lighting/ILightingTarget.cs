// What the tuning tools see of the scene's lighting: its settings, and the brightness of the lights now. The lighting
// itself (which needs the render pipeline's 2D lights) lives in its own assembly and implements this.
public interface ILightingTarget {
    LightingSettings Settings { get; }

    // Applies the settings (after they were changed) to the lights.
    void Apply();

    float PlayerIntensity { get; }
    float MuzzleIntensity { get; }
    // Where the muzzle flash was last lit, and how many shots have lit it.
    UnityEngine.Vector2 MuzzlePosition { get; }
    int MuzzleShots { get; }
    // The sun's light now (0 when off), where its pivot is, and whether it throws shadows.
    float SunIntensity { get; }
    UnityEngine.Vector2 SunPivot { get; }
    bool SunCastsShadows { get; }
    // How many placed lamps are lit by the rig, and how many shadow casters the scene has.
    int LampCount { get; }
    int ShadowCasterCount { get; }
}
