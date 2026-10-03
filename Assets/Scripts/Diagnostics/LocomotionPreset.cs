using System;
using UnityEngine;

// Every tunable locomotion setting as one plain value: what the runtime tuning panel edits, what presets save as JSON,
// and what is copied out to commit a tuned configuration back into the project.
[Serializable]
public class LocomotionPreset {
    public GaitProfile walk;
    public GaitProfile run;
    public GaitTransitionSettings transitions;
    public BalanceSettings balance;
    public IdleStanceSettings idle;
    public float bodyHeight;
    public BodyMassSettings body;
    public PostureSettings posture;
    public SpringSettings chestSpring;
    public BreathingSettings breathing;
    public HeadMotionSettings head;
    public ArmSwingSettings arms;

    // The settings as the code defines them, whatever the scene has serialized.
    public static LocomotionPreset CodeDefaults() {
        return new LocomotionPreset {
            walk = GaitProfile.Walk,
            run = GaitProfile.Run,
            transitions = GaitTransitionSettings.Default,
            balance = BalanceSettings.Default,
            idle = IdleStanceSettings.Default,
            bodyHeight = StrideController.DefaultBodyHeight,
            body = BodyMassSettings.Default,
            posture = PostureSettings.Default,
            chestSpring = TorsoMotion.DefaultSpring,
            breathing = BreathingSettings.Default,
            head = HeadMotionSettings.Default,
            arms = ArmSwingSettings.Default,
        };
    }

    // Every field is a value, so a member-wise copy is a full, independent copy.
    public LocomotionPreset Copy() => (LocomotionPreset)MemberwiseClone();

    public string ToJson() => JsonUtility.ToJson(this, true);

    public static LocomotionPreset FromJson(string json) => JsonUtility.FromJson<LocomotionPreset>(json);
}
