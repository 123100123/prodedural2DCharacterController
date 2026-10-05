using UnityEngine;

// The collaborators a composed character hands to tools that tune and inspect it. Plain references, so the
// character knows nothing about who uses them: the tools depend on the character, never the other way round.
public readonly struct CharacterParts {
    public CharacterParts((StrideController stride, LocomotionPosture posture, TorsoMotion torso, HeadLook head, ArmSwing arms) movement, (CrouchController crouch, Transform postureRoot) body, (JumpSettings jump, AirborneSettings airborne, AirborneState airborneState, (AirPoseSettings pose, FallSecondarySettings fall) air, LandingSettings landing) moves, (ShootingSettings shooting, Pistol pistol, (WeaponSettings settings, WeaponController controller) shot) weapon, (CameraFeelSettings settings, FollowCamera camera) cameraRig, EffectsSettings effects, CharacterAudioSettings audio, (WallSettings settings, WallSlideState state) wall) {
        Stride = movement.stride;
        Posture = movement.posture;
        Torso = movement.torso;
        Head = movement.head;
        Arms = movement.arms;
        Crouch = body.crouch;
        PostureRoot = body.postureRoot;
        Jump = moves.jump;
        Airborne = moves.airborne;
        AirborneState = moves.airborneState;
        AirPose = moves.air.pose;
        FallSecondary = moves.air.fall;
        Landing = moves.landing;
        Shooting = weapon.shooting;
        Pistol = weapon.pistol;
        Weapon = weapon.shot.settings;
        WeaponController = weapon.shot.controller;
        CameraFeel = cameraRig.settings;
        FollowCamera = cameraRig.camera;
        Effects = effects;
        Audio = audio;
        Wall = wall.settings;
        WallSlide = wall.state;
    }

    public StrideController Stride { get; }
    public LocomotionPosture Posture { get; }
    public TorsoMotion Torso { get; }
    public HeadLook Head { get; }
    public ArmSwing Arms { get; }
    public CrouchController Crouch { get; }
    public Transform PostureRoot { get; }
    public JumpSettings Jump { get; }
    public AirborneSettings Airborne { get; }
    // The flight state, for its read-only snapshot.
    public AirborneState AirborneState { get; }
    public AirPoseSettings AirPose { get; }
    public FallSecondarySettings FallSecondary { get; }
    public LandingSettings Landing { get; }
    public ShootingSettings Shooting { get; }
    public Pistol Pistol { get; }
    public WeaponSettings Weapon { get; }
    // The pistol's magazine, reload and shot; null until the character is composed.
    public WeaponController WeaponController { get; }
    public CameraFeelSettings CameraFeel { get; }
    public FollowCamera FollowCamera { get; }
    public EffectsSettings Effects { get; }
    public CharacterAudioSettings Audio { get; }
    public WallSettings Wall { get; }
    // The wall slide, for whether and where it slides; null until the character is composed.
    public WallSlideState WallSlide { get; }
}
