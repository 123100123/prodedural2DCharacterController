using System;
using UnityEngine;

// Pitches the chest through a spring so the torso moves with the legs: a sustained lean that grows with speed, a flex
// as each foot takes the weight and an extension over the supporting leg (twice per stride), a lean into starts and a
// rock back on stops, slow breathing when standing still, and recoil kicks.
[Serializable]
public class TorsoMotion {
    [Tooltip("Lean in degrees per unit of forward acceleration of the gait (units per second squared).")]
    [SerializeField] private float leanPerAcceleration = 0.6f;
    [SerializeField] private float maxAccelerationLean = 10f;
    [Tooltip("Cap on the combined lean, so a sprint start does not fold the torso over.")]
    [SerializeField] private float maxTotalLean = 16f;
    [SerializeField] private SpringSettings spring = new SpringSettings(2.5f, 0.7f, 0f);

    [Header("Breathing when standing still")]
    [SerializeField] private float breathPeriod = 3.6f;
    [Tooltip("Chest extension in degrees at the top of a breath.")]
    [SerializeField] private float breathPitch = 1.2f;

    private CharacterRig rig;
    private Quaternion restChest;
    private SecondOrderDynamics pitch;
    private float previousGroundSpeed;
    private float breathTime;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restChest = rig.Chest.localRotation;
        pitch = new SecondOrderDynamics(0f);
        previousGroundSpeed = rig.Stride.GroundSpeed;
    }

    // A backward jolt, e.g. pistol recoil (degrees per second of backward lean).
    public void Kick(float backwardDegreesPerSecond) => pitch.AddVelocity(-backwardDegreesPerSecond);

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }

        float target = GaitPitch() + AccelerationLean(deltaTime) + BreathingPitch(deltaTime);
        float forwardPitch = pitch.Update((Mathf.Clamp(target, -maxTotalLean, maxTotalLean), deltaTime), spring);
        float forwardTurnSign = -rig.Facing.AuthoredSign;
        rig.Chest.localRotation = Quaternion.Euler(0f, 0f, forwardPitch * forwardTurnSign) * restChest;
    }

    // Sustained lean plus the per-step flex and extension, both fading out as the character slows to a stop.
    private float GaitPitch() {
        StrideController stride = rig.Stride;
        if (!stride.IsDrivingGait) {
            return 0f;
        }

        GaitProfile gait = stride.CurrentGait;
        float lowestPhase = GaitWaves.LowestPhase((gait.dutyFactor, gait.lowAfterContact));
        float stepPitch = GaitWaves.TorsoPitch((stride.GaitPhase, lowestPhase)) * gait.pitchOscillation;
        return (gait.lean + stepPitch) * stride.SpeedShare;
    }

    // Uses the gait's ground speed rather than the body position, so the step's own surge and bob do not read as starts.
    private float AccelerationLean(float deltaTime) {
        float groundSpeed = rig.Stride.GroundSpeed;
        float forwardAcceleration = (groundSpeed - previousGroundSpeed) / deltaTime * rig.Facing.Sign;
        previousGroundSpeed = groundSpeed;
        return Mathf.Clamp(forwardAcceleration * leanPerAcceleration, -maxAccelerationLean, maxAccelerationLean);
    }

    // Extension (negative forward pitch) on each in-breath, only while standing still.
    private float BreathingPitch(float deltaTime) {
        breathTime += deltaTime;
        float stillness = rig.Stride.IsDrivingGait ? 1f - rig.Stride.SpeedShare : 0f;
        return -breathPitch * stillness * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * breathTime / breathPeriod));
    }
}
