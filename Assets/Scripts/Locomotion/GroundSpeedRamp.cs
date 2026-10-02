using UnityEngine;

// Pure ground speed change: speeds up at the gait's acceleration and brakes (slowing, stopping or turning) at its
// harder deceleration, so starts and stops carry momentum.
public static class GroundSpeedRamp {
    public static float Step((float speed, float target, GaitProfile gait) ramp, float deltaTime) {
        bool isSpeedingUp = Mathf.Abs(ramp.target) > Mathf.Abs(ramp.speed) && ramp.target * ramp.speed >= 0f;
        float rate = isSpeedingUp ? ramp.gait.acceleration : ramp.gait.deceleration;
        return Mathf.MoveTowards(ramp.speed, ramp.target, rate * deltaTime);
    }
}
