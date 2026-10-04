using System;
using UnityEngine;

// Secondary chest motion on top of the locomotion posture: a small flex as each foot takes the weight and an extension
// over the supporting leg (twice per stride), recoil kicks, and breathing when standing still (the chest rises along
// the spine and extends). The sustained forward lean belongs to the posture root (LocomotionPosture), so these stay
// small and never dominate the silhouette.
[Serializable]
public class TorsoMotion {
    public static readonly SpringSettings DefaultSpring = new SpringSettings(3f, 0.8f, 0f);
    // Fades breathing in and out as the character stops and moves off, so the chest never jumps.
    private static readonly SpringSettings BreathFade = new SpringSettings(2f, 1f, 0f);

    [Tooltip("How the chest follows its flex and absorbs recoil kicks.")]
    [SerializeField] private SpringSettings spring = DefaultSpring;

    [SerializeField] private BreathingSettings breathing = BreathingSettings.Default;

    private CharacterRig rig;
    private Quaternion restChest;
    private Vector3 restChestPosition;
    private SecondOrderDynamics pitch;
    private SecondOrderDynamics breath;
    private float breathTime;

    public SpringSettings Spring { get => spring; set => spring = value; }
    public BreathingSettings Breath { get => breathing; set => breathing = value; }
    // Degrees the chest is flexed forward on top of the posture right now.
    public float CurrentPitch => pitch?.Value ?? 0f;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restChest = rig.Chest.localRotation;
        restChestPosition = rig.Chest.localPosition;
        pitch = new SecondOrderDynamics(0f);
        breath = new SecondOrderDynamics(0f);
    }

    // A backward jolt, e.g. pistol recoil (degrees per second of backward lean).
    public void Kick(float backwardDegreesPerSecond) => pitch.AddVelocity(-backwardDegreesPerSecond);

    public void Tick(float deltaTime) {
        if (deltaTime <= 0f) {
            return;
        }

        float lungs = BreathDepth(deltaTime);
        float fold = rig.Stride.CrouchSettings.chestFold * rig.Stride.CrouchShare;
        float forwardPitch = pitch.Update((GaitFlex() + fold - breathing.pitch * lungs, deltaTime), spring);
        PoseChest((forwardPitch, lungs));
    }

    // Turns the chest forward about its base and raises it along the spine as the lungs fill.
    private void PoseChest((float forwardPitch, float lungs) pose) {
        float forwardTurnSign = -rig.Facing.AuthoredSign;
        rig.Chest.localRotation = Quaternion.Euler(0f, 0f, pose.forwardPitch * forwardTurnSign) * restChest;
        rig.Chest.localPosition = restChestPosition + restChestPosition.normalized * (breathing.rise * pose.lungs);
    }

    // The per-step flex and extension, fading out as the character slows to a stop.
    private float GaitFlex() {
        StrideController stride = rig.Stride;
        if (!stride.IsDrivingGait) {
            return 0f;
        }

        GaitProfile gait = stride.CurrentGait;
        float lowestPhase = GaitWaves.LowestPhase((gait.dutyFactor, gait.lowAfterContact));
        return GaitWaves.TorsoPitch((stride.GaitPhase, lowestPhase)) * gait.pitchOscillation * stride.Pace.Moving;
    }

    // How full the lungs are (0 to 1) while standing still; breathing fades out as the character moves off.
    private float BreathDepth(float deltaTime) {
        breathTime += deltaTime;
        float stillness = rig.Stride.IsDrivingGait ? 1f - rig.Stride.Pace.Moving : 0f;
        return breath.Update((stillness * Breathing.Fullness(breathTime / breathing.period), deltaTime), BreathFade);
    }
}
