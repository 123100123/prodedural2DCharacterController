using System;
using UnityEngine;

// Pitches the chest through a spring: a sustained lean from the gait (more when running), plus a lean with the body's
// horizontal acceleration (into a start, rocking back on a stop), plus recoil kicks.
[Serializable]
public class TorsoLean {
    [Tooltip("Lean in degrees per unit of forward acceleration (units per second squared).")]
    [SerializeField] private float leanPerAcceleration = 0.35f;
    [SerializeField] private float maxLean = 12f;
    [Tooltip("Cap on the combined gait and acceleration lean, so a sprint start does not fold the torso over.")]
    [SerializeField] private float maxTotalLean = 16f;
    [SerializeField] private SpringSettings spring = new SpringSettings(2.5f, 0.7f, 0f);

    private (Transform chest, Transform body) bones;
    private (CharacterFacing facing, int authoredSign) orientation;
    private Quaternion restChest;
    private SecondOrderDynamics lean;
    private Vector2 previousBodyPosition;
    private float previousSpeedX;

    public void Initialize((Transform chest, Transform body) rigBones, CharacterFacing facing) {
        bones = rigBones;
        orientation = (facing, facing.AuthoredSign);
        restChest = rigBones.chest.localRotation;
        lean = new SecondOrderDynamics(0f);
        previousBodyPosition = rigBones.body.position;
    }

    // A backward jolt, e.g. pistol recoil (degrees per second of backward lean).
    public void Kick(float backwardDegreesPerSecond) => lean.AddVelocity(-backwardDegreesPerSecond);

    public void Tick((float deltaTime, float gaitLean) frame) {
        if (frame.deltaTime <= 0f) {
            return;
        }

        float combined = frame.gaitLean + LeanForAcceleration(ForwardAcceleration(frame.deltaTime));
        float target = Mathf.Clamp(combined, -maxTotalLean, maxTotalLean);
        float leanAngle = lean.Update((target, frame.deltaTime), spring);
        float forwardTurnSign = -orientation.authoredSign;
        bones.chest.localRotation = Quaternion.Euler(0f, 0f, leanAngle * forwardTurnSign) * restChest;
    }

    private float ForwardAcceleration(float deltaTime) {
        Vector2 bodyPosition = bones.body.position;
        float speedX = (bodyPosition.x - previousBodyPosition.x) / deltaTime;
        float accelerationX = (speedX - previousSpeedX) / deltaTime;
        previousBodyPosition = bodyPosition;
        previousSpeedX = speedX;
        return accelerationX * orientation.facing.Sign;
    }

    private float LeanForAcceleration(float forwardAcceleration) {
        return Mathf.Clamp(forwardAcceleration * leanPerAcceleration, -maxLean, maxLean);
    }
}
