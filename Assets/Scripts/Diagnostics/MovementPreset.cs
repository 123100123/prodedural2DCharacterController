using System;
using UnityEngine;

// Every tunable movement setting as one plain value: what the runtime tuning panel edits, what presets save as JSON,
// and what is copied out to commit a tuned configuration back into the project. Lighting has its own preset.
[Serializable]
public class MovementPreset : ITuningPreset<MovementPreset> {
    public const int CurrentVersion = 2;

    // Written to every saved file; lets a later change to the format recognise the files written before it.
    public int version = CurrentVersion;
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
    public static MovementPreset CodeDefaults() {
        return new MovementPreset {
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
    public MovementPreset Copy() => (MovementPreset)MemberwiseClone();

    public string ToJson() => JsonUtility.ToJson(this, true);

    // The file's values over the code defaults: a setting the file does not mention (it was saved before the setting
    // existed) keeps its default instead of loading as zero.
    public static MovementPreset FromJson(string json) {
        MovementPreset preset = CodeDefaults();
        JsonUtility.FromJsonOverwrite(json, preset);
        return preset;
    }
}
