using UnityEngine;

// What the airborne state is doing, written by it each frame for debug tools to read; the character never reads it
// back. All zero on the ground.
public struct AirSnapshot {
    public bool IsAirborne;
    public Vector2 Velocity;
    // The velocity the flight started with, and whether it was a jump (rather than a fall off an edge).
    public Vector2 LaunchVelocity;
    public bool LaunchedByJump;
    public float TimeAirborne;
    // Seconds spent moving downward.
    public float TimeFalling;
    public float TimeSinceLaunch;
    // How far the flight is rising, hanging and falling (add up to 1), and how far the pose has moved from standing to
    // running.
    public float Ascent;
    public float Apex;
    public float Descent;
    public float RunningPoseShare;
    // How strongly the long-fall secondary motion (flailing arms, loose legs) is acting, 0 to 1.
    public float FlailStrength;
}
