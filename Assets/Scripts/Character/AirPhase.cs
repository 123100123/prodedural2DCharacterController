using UnityEngine;

// Where the flight is between taking off and coming down, as three weights that add up to 1, from the vertical speed
// alone: rising, hanging near the top, and falling. A short hop passes through them quickly and a long fall stays in
// the last; nothing is a timer or a switch, so poses blended by these weights change smoothly.
public readonly struct AirPhase {
    private AirPhase(float ascent, float apex, float descent) {
        Ascent = ascent;
        Apex = apex;
        Descent = descent;
    }

    public float Ascent { get; }
    public float Apex { get; }
    public float Descent { get; }

    // apexBand: the vertical speed (up or down) at which the pose is fully ascending or fully descending.
    public static AirPhase From(float verticalSpeed, float apexBand) {
        float signed = Mathf.Clamp(verticalSpeed / Mathf.Max(0.01f, apexBand), -1f, 1f);
        float ascent = Mathf.SmoothStep(0f, 1f, signed);
        float descent = Mathf.SmoothStep(0f, 1f, -signed);
        return new AirPhase(ascent, 1f - Mathf.Max(ascent, descent), descent);
    }
}
