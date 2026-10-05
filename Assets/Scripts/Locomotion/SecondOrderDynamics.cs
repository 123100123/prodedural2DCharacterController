using System;
using UnityEngine;

// Spring-damper tuning in perceptual terms (t3ssel8r, "Giving Personality to Procedural Animations using Math").
[Serializable]
public struct SpringSettings {
    [Tooltip("Natural frequency in Hz: how fast the value responds.")]
    public float frequency;
    [Tooltip("Damping ratio: below 1 overshoots and settles, 1 is critically damped, above 1 is sluggish.")]
    public float damping;
    [Tooltip("Initial response: below 0 anticipates (moves away first), 1 matches the target, above 1 overshoots it.")]
    public float response;

    public SpringSettings(float frequency, float damping, float response) {
        this.frequency = frequency;
        this.damping = damping;
        this.response = response;
    }

    // How far the value trails a target moving steadily at the given speed: the damping drags it behind, the response
    // term leads it back.
    public float TrackingLag(float targetSpeed) {
        return targetSpeed * damping * (2f - response) / (2f * Mathf.PI * Mathf.Max(0.01f, frequency));
    }

    // How far the value's oscillation trails a target oscillating at the given frequency, in cycles: the phase of the
    // spring's response, less the lead its response term adds.
    public float PhaseLagCycles(float drivingFrequency) {
        float ratio = drivingFrequency / Mathf.Max(0.01f, frequency);
        float springLag = Mathf.Atan2(2f * damping * ratio, 1f - ratio * ratio);
        float responseLead = Mathf.Atan(response * damping * ratio);
        return (springLag - responseLead) / (2f * Mathf.PI);
    }
}

// A value that follows a moving target like a mass on a spring, giving procedural motion inertia, overshoot and settle.
// Semi-implicit Euler with the stability clamp on k2, so long frames do not blow up.
public sealed class SecondOrderDynamics {
    private float previousTarget;

    public SecondOrderDynamics(float initialValue) => Reset(initialValue);

    public float Value { get; private set; }
    public float Velocity { get; private set; }

    public void Reset(float value) => ResetWithVelocity((value, 0f));

    // Restarts at a value while keeping momentum, e.g. when the body arrives from another state.
    public void ResetWithVelocity((float value, float velocity) state) {
        Value = state.value;
        Velocity = state.velocity;
        previousTarget = state.value;
    }

    // An instantaneous push, e.g. the jolt of a foot plant or a landing.
    public void AddVelocity(float impulse) => Velocity += impulse;

    public float Update((float target, float deltaTime) step, SpringSettings settings) {
        if (step.deltaTime <= 0f) {
            return Value;
        }

        float targetVelocity = (step.target - previousTarget) / step.deltaTime;
        previousTarget = step.target;
        (float k1, float k2, float k3) = Coefficients(settings);
        // Nested two-argument Max: the params overload would allocate an array every call, and this runs every frame.
        float stableK2 = Mathf.Max(Mathf.Max(k2, step.deltaTime * step.deltaTime / 2f + step.deltaTime * k1 / 2f), step.deltaTime * k1);

        Value += step.deltaTime * Velocity;
        Velocity += step.deltaTime * (step.target + k3 * targetVelocity - Value - k1 * Velocity) / stableK2;
        return Value;
    }

    private static (float k1, float k2, float k3) Coefficients(SpringSettings settings) {
        float frequency = Mathf.Max(0.01f, settings.frequency);
        float angular = 2f * Mathf.PI * frequency;
        return (settings.damping / (Mathf.PI * frequency), 1f / (angular * angular), settings.response * settings.damping / angular);
    }
}
