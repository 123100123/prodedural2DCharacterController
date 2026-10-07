using UnityEngine;

// The collaborators a composed character hands to tools that tune and inspect it. Plain references, so the
// character knows nothing about who uses them: the tools depend on the character, never the other way round.
public readonly struct CharacterParts {
    public CharacterParts((StrideController stride, LocomotionPosture posture, TorsoMotion torso, HeadLook head, ArmSwing arms) movement, (CrouchController crouch, Transform postureRoot) body, (JumpSettings jump, AirborneSettings airborne, AirborneState airborneState, (AirPoseSettings pose, FallSecondarySettings fall) air, LandingSettings landing) moves, (ShootingSettings shooting, Pistol pistol, (WeaponSettings settings, WeaponController controller) shot) weapon, (CameraFeelSettings settings, FollowCamera camera) cameraRig, EffectsSettings effects, CharacterAudioSettings audio, (WallSettings settings, WallSlideState state) wall, (SlideSettings settings, SlideState state) slide, (LedgeHangSettings hang, OneWaySettings oneWay, ClimbState climb) ledges, (PushSettings settings, CratePusher pusher, PushPose pose) push, ((RollSettings settings, RollMomentumSettings momentum, RollRunExitSettings runExit, RollState state) roll, ClimbSettings climb, SlopeSettings slope) moveTuning, (CharacterStateMachine machine, WalkState walk) fullBody) {
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
        Slide = slide.settings;
        SlideState = slide.state;
        LedgeHang = ledges.hang;
        OneWay = ledges.oneWay;
        Climb = ledges.climb;
        Push = push.settings;
        Pusher = push.pusher;
        PushPose = push.pose;
        Roll = moveTuning.roll.settings;
        RollMomentum = moveTuning.roll.momentum;
        RollRunExit = moveTuning.roll.runExit;
        RollState = moveTuning.roll.state;
        ClimbSettings = moveTuning.climb;
        Slope = moveTuning.slope;
        Machine = fullBody.machine;
        Walk = fullBody.walk;
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
    public SlideSettings Slide { get; }
    // The running slide, for whether, which way and how fast it slides; null until the character is composed.
    public SlideState SlideState { get; }
    public LedgeHangSettings LedgeHang { get; }
    public OneWaySettings OneWay { get; }
    // The ledge climb, for whether the character hangs; null until the character is composed.
    public ClimbState Climb { get; }
    public PushSettings Push { get; }
    // The crate push, for whether, which way and what it pushes; null until the character is composed.
    public CratePusher Pusher { get; }
    // The push pose, for how far it is blended in; null until the character is composed.
    public PushPose PushPose { get; }
    public RollSettings Roll { get; }
    public RollMomentumSettings RollMomentum { get; }
    public RollRunExitSettings RollRunExit { get; }
    // The roll, for its momentum readout (launch, speed, deceleration, hand-back); null until the character is composed.
    public RollState RollState { get; }
    public ClimbSettings ClimbSettings { get; }
    public SlopeSettings Slope { get; }
    // The full-body state machine, for the current state, its run readiness and the last switch; null until the
    // character is composed.
    public CharacterStateMachine Machine { get; }
    // The grounded walk, for the speed it was handed when it last began; null until the character is composed.
    public WalkState Walk { get; }
}
