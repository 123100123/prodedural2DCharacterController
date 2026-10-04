using UnityEngine;

// Steering the horizontal speed in the air. Input steers up to the air speed, opposite input brakes and reverses, and
// speed carried in from the ground beyond the air speed is never trimmed by steering with it; with no input only a
// gentle drag acts, so a jump keeps its momentum instead of stopping the moment a key is released.
public static class AirSteering {
    public static float Step((float speedX, float moveAxis, float deltaTime) air, AirborneSettings settings) {
        if (Mathf.Approximately(air.moveAxis, 0f)) {
            return Mathf.MoveTowards(air.speedX, 0f, settings.neutralDrag * air.deltaTime);
        }

        float target = air.moveAxis * settings.airSpeed;
        bool isReversing = air.speedX * air.moveAxis < 0f;
        if (!isReversing && Mathf.Abs(air.speedX) >= Mathf.Abs(target)) {
            return air.speedX;
        }
        return Mathf.MoveTowards(air.speedX, target, settings.airAcceleration * air.deltaTime);
    }
}
