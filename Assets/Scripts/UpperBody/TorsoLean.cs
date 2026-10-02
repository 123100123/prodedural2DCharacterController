using System;
using UnityEngine;

// Pitches the chest with the body's horizontal acceleration through a spring: leans into a start,
// rocks back and settles on a stop, and takes recoil kicks.
[Serializable]
public class TorsoLean {
    [Tooltip("Lean in degrees per unit of forward acceleration (units per second squared).")]
    [SerializeField] private float leanPerAcceleration = 0.35f;
    [SerializeField] private float maxLean = 12f;
    [SerializeField] private SpringSettings spring = new SpringSettings(2.5f, 0.45f, 0f);

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

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }

        float leanAngle = lean.Update((LeanForAcceleration(ForwardAcceleration(deltaTime)), deltaTime), spring);
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
