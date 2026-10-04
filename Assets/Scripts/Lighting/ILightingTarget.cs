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
}
