using System;
using UnityEngine;

// Every tunable movement setting as one plain value: what the runtime tuning panel edits, what presets save as JSON,
// and what is copied out to commit a tuned configuration back into the project. Lighting has its own preset.
[Serializable]
public class MovementPreset : ITuningPreset<MovementPreset> {
    public const int CurrentVersion = 3;

    // Written to every saved file; lets a later change to the format recognise the files written before it.
    public int version = CurrentVersion;
    public GaitProfile walk;
    public GaitProfile run;
    public GaitProfile crouchGait;
    public CrouchSettings crouch;
    public JumpSettings jump;
    public AirborneSettings air;
    public AirPoseSettings airPose;
    public FallSecondarySettings fallSecondary;
    public LandingSettings landing;
    public ShootingSettings shooting;
    public float pistolCooldown;
    public float pistolMuzzleOffset;
    public WeaponSettings weapon;
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
            crouchGait = GaitProfile.Crouch,
            crouch = CrouchSettings.Default,
            jump = new JumpSettings(),
            air = new AirborneSettings(),
            airPose = new AirPoseSettings(),
            fallSecondary = new FallSecondarySettings(),
            landing = new LandingSettings(),
            shooting = new ShootingSettings(),
            pistolCooldown = 0.18f,
            pistolMuzzleOffset = 0.15f,
            weapon = new WeaponSettings(),
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

    // The settings that are classes (held by reference by the states) are copied too.
    public MovementPreset Copy() {
        MovementPreset copy = (MovementPreset)MemberwiseClone();
        copy.jump = jump.Clone();
        copy.air = air.Clone();
        copy.airPose = airPose.Clone();
        copy.fallSecondary = fallSecondary.Clone();
        copy.landing = landing.Clone();
        copy.shooting = shooting.Clone();
        copy.weapon = weapon.Clone();
        return copy;
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    // The file's values over the code defaults: a setting the file does not mention (it was saved before the setting
    // existed) keeps its default instead of loading as zero.
    public static MovementPreset FromJson(string json) {
        MovementPreset preset = CodeDefaults();
        JsonUtility.FromJsonOverwrite(json, preset);
        return preset;
    }
}
