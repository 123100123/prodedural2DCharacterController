using System;
using UnityEngine;

// Moves the chest through springs so the torso moves with the legs: a sustained lean that grows with speed, a flex
// as each foot takes the weight and an extension over the supporting leg (twice per stride), a lean into starts and a
// rock back on stops, recoil kicks, and breathing when standing still (the chest rises along the spine and extends).
[Serializable]
public class TorsoMotion {
    // Fades breathing in and out as the character stops and moves off, so the chest never jumps.
    private static readonly SpringSettings BreathFade = new SpringSettings(2f, 1f, 0f);

    [Tooltip("Lean in degrees per unit of forward acceleration of the gait (units per second squared).")]
    [SerializeField] private float leanPerAcceleration = 0.6f;
    [SerializeField] private float maxAccelerationLean = 10f;
    [Tooltip("Cap on the combined lean, so a sprint start does not fold the torso over.")]
    [SerializeField] private float maxTotalLean = 16f;
    [SerializeField] private SpringSettings spring = new SpringSettings(2.5f, 0.7f, 0f);

    [Header("Breathing when standing still")]
    [Tooltip("Seconds per breath.")]
    [SerializeField] private float breathPeriod = 4f;
    [Tooltip("Chest extension in degrees with full lungs.")]
    [SerializeField] private float breathPitch = 3.5f;
    [Tooltip("How far the chest rises along the spine with full lungs, lifting the shoulders, arms and head.")]
    [SerializeField] private float breathRise = 0.07f;

    private CharacterRig rig;
    private Quaternion restChest;
    private Vector3 restChestPosition;
    private SecondOrderDynamics pitch;
    private SecondOrderDynamics breath;
    private float previousGroundSpeed;
    private float breathTime;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restChest = rig.Chest.localRotation;
        restChestPosition = rig.Chest.localPosition;
        pitch = new SecondOrderDynamics(0f);
        breath = new SecondOrderDynamics(0f);
        previousGroundSpeed = rig.Stride.GroundSpeed;
    }

    // A backward jolt, e.g. pistol recoil (degrees per second of backward lean).
    public void Kick(float backwardDegreesPerSecond) => pitch.AddVelocity(-backwardDegreesPerSecond);

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }

        float lungs = BreathDepth(deltaTime);
        float target = GaitPitch() + AccelerationLean(deltaTime) - breathPitch * lungs;
        float forwardPitch = pitch.Update((Mathf.Clamp(target, -maxTotalLean, maxTotalLean), deltaTime), spring);
        PoseChest((forwardPitch, lungs));
    }

    // Turns the chest forward about its base and raises it along the spine as the lungs fill.
    private void PoseChest((float forwardPitch, float lungs) pose) {
        float forwardTurnSign = -rig.Facing.AuthoredSign;
        rig.Chest.localRotation = Quaternion.Euler(0f, 0f, pose.forwardPitch * forwardTurnSign) * restChest;
        rig.Chest.localPosition = restChestPosition + restChestPosition.normalized * (breathRise * pose.lungs);
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

    // How full the lungs are (0 to 1) while standing still; breathing fades out as the character moves off.
    private float BreathDepth(float deltaTime) {
        breathTime += deltaTime;
        float stillness = rig.Stride.IsDrivingGait ? 1f - rig.Stride.SpeedShare : 0f;
        return breath.Update((stillness * Breathing.Fullness(breathTime / breathPeriod), deltaTime), BreathFade);
    }
}
