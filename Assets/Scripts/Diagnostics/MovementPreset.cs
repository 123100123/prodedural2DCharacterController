using System;
using UnityEngine;

// Every tunable movement setting as one plain value: what the runtime tuning panel edits, what presets save as JSON,
// and what is copied out to commit a tuned configuration back into the project. Lighting has its own preset.
[Serializable]
public class MovementPreset : ITuningPreset<MovementPreset> {
    public const int CurrentVersion = 16;

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
    // Legacy since version 15: the muzzle is the pistol picture's barrel tip. Kept so older files load.
    public float pistolMuzzleOffset;
    public WeaponSettings weapon;
    // Since version 15: where the holster hangs and how the pistol is held (missing before then). Since version 16 the
    // holster hangs from the front thigh, and its offset and angle are in that bone's space; a version 15 file's are in the
    // pelvis' space, so they load at their defaults (FromJson). The draw and holster timings are used since version 16.
    public PistolHandlingSettings pistolHandling;
    public CameraFeelSettings cameraFeel;
    public EffectsSettings effects;
    public CharacterAudioSettings audio;
    public WallSettings wall;
    public SlideSettings slide;
    public LedgeHangSettings ledgeHang;
    public OneWaySettings oneWay;
    public PushSettings push;
    public RollSettings roll;
    // Since version 13: the roll's momentum, and its run exit (missing before then).
    public RollMomentumSettings rollMomentum;
    public RollRunExitSettings rollRunExit;
    public ClimbSettings climb;
    // Since version 14: the release extension and the torso's slope lean (missing before then); the step shortening and
    // pelvis drop are kept, unused, so older files still load.
    public SlopeSettings slope;
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
            pistolHandling = new PistolHandlingSettings(),
            cameraFeel = new CameraFeelSettings(),
            effects = new EffectsSettings(),
            audio = new CharacterAudioSettings(),
            wall = new WallSettings(),
            slide = new SlideSettings(),
            ledgeHang = new LedgeHangSettings(),
            oneWay = new OneWaySettings(),
            push = new PushSettings(),
            roll = new RollSettings(),
            rollMomentum = new RollMomentumSettings(),
            rollRunExit = new RollRunExitSettings(),
            climb = new ClimbSettings(),
            slope = new SlopeSettings(),
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
        copy.pistolHandling = pistolHandling.Clone();
        copy.cameraFeel = cameraFeel.Clone();
        copy.effects = effects.Clone();
        copy.audio = audio.Clone();
        copy.wall = wall.Clone();
        copy.slide = slide.Clone();
        copy.ledgeHang = ledgeHang.Clone();
        copy.oneWay = oneWay.Clone();
        copy.push = push.Clone();
        copy.roll = roll.Clone();
        copy.rollMomentum = rollMomentum.Clone();
        copy.rollRunExit = rollRunExit.Clone();
        copy.climb = climb.Clone();
        copy.slope = slope.Clone();
        return copy;
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    // The file's values over the code defaults: a setting the file does not mention (it was saved before the setting
    // existed) keeps its default instead of loading as zero.
    public static MovementPreset FromJson(string json) {
        MovementPreset preset = CodeDefaults();
        JsonUtility.FromJsonOverwrite(json, preset);
        if (preset.version < ThighHolsterVersion) {
            PistolHandlingSettings defaults = new PistolHandlingSettings();
            preset.pistolHandling.holsterOffset = defaults.holsterOffset;
            preset.pistolHandling.holsterAngle = defaults.holsterAngle;
        }
        return preset;
    }

    // The first version whose holster offset and angle are in the front thigh's space (before it, the pelvis').
    private const int ThighHolsterVersion = 16;
}
