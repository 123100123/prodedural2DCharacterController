// Reads and writes every tunable movement setting through its owners' settings properties, as one MovementPreset.
// In the Editor the changes live on the scene's components for the play session only; nothing is saved to assets.
public sealed class MovementTuning {
    private readonly StrideController stride;
    private readonly LocomotionPosture posture;
    private readonly TorsoMotion torso;
    private readonly HeadLook head;
    private readonly ArmSwing arms;
    private readonly CrouchController crouch;
    private readonly JumpSettings jump;
    private readonly AirborneSettings air;
    private readonly AirPoseSettings airPose;
    private readonly FallSecondarySettings fallSecondary;
    private readonly LandingSettings landing;
    private readonly ShootingSettings shooting;
    private readonly Pistol pistol;
    private readonly WeaponSettings weapon;
    private readonly CameraFeelSettings cameraFeel;
    private readonly EffectsSettings effects;
    private readonly CharacterAudioSettings audio;
    private readonly WallSettings wall;
    private readonly SlideSettings slide;
    private readonly LedgeHangSettings ledgeHang;
    private readonly OneWaySettings oneWay;
    private readonly PushSettings push;
    private readonly RollSettings roll;
    private readonly RollMomentumSettings rollMomentum;
    private readonly RollRunExitSettings rollRunExit;
    private readonly ClimbSettings climb;
    private readonly SlopeSettings slope;

    public MovementTuning(CharacterParts parts) {
        stride = parts.Stride;
        posture = parts.Posture;
        torso = parts.Torso;
        head = parts.Head;
        arms = parts.Arms;
        crouch = parts.Crouch;
        jump = parts.Jump;
        air = parts.Airborne;
        airPose = parts.AirPose;
        fallSecondary = parts.FallSecondary;
        landing = parts.Landing;
        shooting = parts.Shooting;
        pistol = parts.Pistol;
        weapon = parts.Weapon;
        cameraFeel = parts.CameraFeel;
        effects = parts.Effects;
        audio = parts.Audio;
        wall = parts.Wall;
        slide = parts.Slide;
        ledgeHang = parts.LedgeHang;
        oneWay = parts.OneWay;
        push = parts.Push;
        roll = parts.Roll;
        rollMomentum = parts.RollMomentum;
        rollRunExit = parts.RollRunExit;
        climb = parts.ClimbSettings;
        slope = parts.Slope;
    }

    public MovementPreset Capture() {
        return new MovementPreset {
            walk = stride.Walk,
            run = stride.Run,
            crouchGait = stride.CrouchGait,
            crouch = crouch.Settings,
            jump = jump.Clone(),
            air = air.Clone(),
            airPose = airPose.Clone(),
            fallSecondary = fallSecondary.Clone(),
            landing = landing.Clone(),
            shooting = shooting.Clone(),
            pistolCooldown = pistol.Cooldown,
            pistolMuzzleOffset = pistol.MuzzleOffset,
            weapon = weapon.Clone(),
            cameraFeel = cameraFeel.Clone(),
            effects = effects.Clone(),
            audio = audio.Clone(),
            wall = wall.Clone(),
            slide = slide.Clone(),
            ledgeHang = ledgeHang.Clone(),
            oneWay = oneWay.Clone(),
            push = push.Clone(),
            roll = roll.Clone(),
            rollMomentum = rollMomentum.Clone(),
            rollRunExit = rollRunExit.Clone(),
            climb = climb.Clone(),
            slope = slope.Clone(),
            transitions = stride.Transitions,
            balance = stride.Balance,
            idle = stride.IdleStance,
            bodyHeight = stride.BodyHeight,
            body = stride.BodyMass,
            posture = posture.Settings,
            chestSpring = torso.Spring,
            breathing = torso.Breath,
            head = head.Motion,
            arms = arms.Settings,
        };
    }

    public void Apply(MovementPreset preset) {
        stride.Walk = preset.walk;
        stride.Run = preset.run;
        stride.CrouchGait = preset.crouchGait;
        crouch.Settings = preset.crouch;
        jump.CopyFrom(preset.jump);
        air.CopyFrom(preset.air);
        airPose.CopyFrom(preset.airPose);
        fallSecondary.CopyFrom(preset.fallSecondary);
        landing.CopyFrom(preset.landing);
        shooting.CopyFrom(preset.shooting);
        pistol.Cooldown = preset.pistolCooldown;
        pistol.MuzzleOffset = preset.pistolMuzzleOffset;
        weapon.CopyFrom(preset.weapon);
        cameraFeel.CopyFrom(preset.cameraFeel);
        effects.CopyFrom(preset.effects);
        audio.CopyFrom(preset.audio);
        wall.CopyFrom(preset.wall);
        slide.CopyFrom(preset.slide);
        ledgeHang.CopyFrom(preset.ledgeHang);
        oneWay.CopyFrom(preset.oneWay);
        push.CopyFrom(preset.push);
        roll.CopyFrom(preset.roll);
        rollMomentum.CopyFrom(preset.rollMomentum);
        rollRunExit.CopyFrom(preset.rollRunExit);
        climb.CopyFrom(preset.climb);
        slope.CopyFrom(preset.slope);
        stride.Transitions = preset.transitions;
        stride.Balance = preset.balance;
        stride.IdleStance = preset.idle;
        stride.BodyHeight = preset.bodyHeight;
        stride.BodyMass = preset.body;
        posture.Settings = preset.posture;
        torso.Spring = preset.chestSpring;
        torso.Breath = preset.breathing;
        head.Motion = preset.head;
        arms.Settings = preset.arms;
    }
}
