using UnityEngine;

// Composition root for the procedural character: builds the collaborators and runs one ordered tick per frame.
// Runs ahead of the components that subscribe to its events (lights, tools), so Events exists when they enable.
[DefaultExecutionOrder(-50)]
public class CharacterDriver : MonoBehaviour {
    [Header("Body")]
    [SerializeField] private StrideController stride;
    [Tooltip("Direction the torso and head face in the authored rig: -1 left, +1 right. inverseRobo faces right (its knees bend backward).")]
    [SerializeField] private int authoredFacingSign = 1;
    [SerializeField] private KinematicBodyMotor motor = new KinematicBodyMotor();

    [Header("Upper body")]
    [SerializeField] private ArmLimb frontArm = new ArmLimb();
    [SerializeField] private ArmLimb backArm = new ArmLimb();
    [SerializeField] private HeadLook head = new HeadLook();
    [Tooltip("Upper-spine bone that flexes with each step and curls forward during a roll.")]
    [SerializeField] private Transform chest;
    [Tooltip("The pelvis bone: the skeleton's root under the kinematic body (the StrideController's transform). It carries " +
             "the sustained forward lean of the locomotion posture, so the whole torso leans from the hips while the " +
             "kinematic body pivot and the world-space foot targets never tilt.")]
    [SerializeField] private Transform postureRoot;
    [SerializeField] private LocomotionPosture posture = new LocomotionPosture();
    [SerializeField] private TorsoMotion torsoMotion = new TorsoMotion();
    [SerializeField] private ArmSwing armSwing = new ArmSwing();

    [Header("Moves")]
    [SerializeField] private CrouchController crouch = new CrouchController();
    [SerializeField] private AirborneSettings airborne = new AirborneSettings();
    [SerializeField] private JumpSettings jump = new JumpSettings();
    [SerializeField] private AirPoseSettings airPose = new AirPoseSettings();
    [SerializeField] private FallSecondarySettings fallSecondary = new FallSecondarySettings();
    [SerializeField] private LandingSettings landing = new LandingSettings();
    [SerializeField] private RollSettings roll = new RollSettings();
    [SerializeField] private ClimbSettings climb = new ClimbSettings();
    [Tooltip("Wall slide and wall jump.")]
    [SerializeField] private WallSettings wall = new WallSettings();

    [Header("Shooting")]
    [SerializeField] private ShootingSettings shooting = new ShootingSettings();
    [SerializeField] private Pistol pistol = new Pistol();
    [SerializeField] private WeaponSettings weapon = new WeaponSettings();

    [Header("Camera")]
    [SerializeField] private FollowCamera followCamera = new FollowCamera();
    [SerializeField] private CameraFeelSettings cameraFeel = new CameraFeelSettings();

    [Header("Effects")]
    [SerializeField] private EffectsSettings effects = new EffectsSettings();

    [Header("Audio")]
    [SerializeField] private CharacterAudioSettings audioSettings = new CharacterAudioSettings();

    private PlayerInputReader input;
    private CharacterStateMachine machine;
    private CharacterStates states;
    private CharacterRig rig;
    private ShootingStance shootingStance;
    private WeaponController weaponController;
    private CharacterEffects characterEffects;
    private CharacterAudio characterAudio;
    private System.Action<(Vector2 muzzle, Vector2 direction)> kickTorsoOnFire;
    private System.Action<(Vector2 position, float impactSpeed)> foldTorsoOnLanding;
    private System.Func<Vector2> scenarioPointerSource;
    private PlayerCommands latestCommands;
    // Whether the character aimed this frame and where the pointer was, for the camera to frame.
    private (bool isAiming, Vector2 pointerWorld) cameraAim;

    public CharacterEvents Events { get; private set; }
    // What debug and tuning tools need; the character never reads anything back from them.
    public CharacterParts Parts => new CharacterParts((stride, posture, torsoMotion, head, armSwing), (crouch, postureRoot), (jump, airborne, states?.Airborne, (airPose, fallSecondary), landing), (shooting, pistol, (weapon, weaponController)), (cameraFeel, followCamera), effects, audioSettings, (wall, states?.WallSlide));
    public PlayerCommands LatestCommands => latestCommands;
    // The full-body state the character is in; null until the character is composed.
    public ICharacterState CurrentState => machine?.Current;
    // Scenario hook: supplies the pointer's screen position in place of the mouse (null goes back to the mouse).
    // Kept here as well, so it can be set before the input reader exists.
    public System.Func<Vector2> ScenarioPointerSource {
        set {
            scenarioPointerSource = value;
            if (input != null) {
                input.PointerSource = value;
            }
        }
    }
    // The dust, smoke, sparks and casings; null until the character is composed.
    public CharacterEffects Effects => characterEffects;
    // The synthesized sounds; null until the character is composed.
    public CharacterAudio Audio => characterAudio;

    // Composes here rather than in Awake: a script recompile during play mode reloads the domain,
    // which clears these non-serialized collaborators and calls OnEnable again, but not Awake.
    private void OnEnable() {
        if (input == null) {
            ComposeCharacter();
        }
        input.Enable();
    }

    private void OnDisable() => input?.Disable();

    private void OnDestroy() => DisposeCollaborators();

    // Disposes what the last composition built, and the camera's and torso's listeners on the character's events.
    private void DisposeCollaborators() {
        if (Events != null) {
            Events.Fired -= kickTorsoOnFire;
            Events.Landed -= foldTorsoOnLanding;
        }
        followCamera.Dispose();
        input?.Dispose();
        weaponController?.Dispose();
        characterAudio?.Dispose();
        characterEffects?.Dispose();
    }

    // Reports a broken rig while editing, as soon as the references change.
    private void OnValidate() {
        if (stride != null && chest != null) {
            ReportRigProblems();
        }
    }

    private void ComposeCharacter() {
        // A script reload in play mode clears the collaborators but leaves their scene objects behind; the marked
        // roots of the previous composition are destroyed here so the new one is the only one.
        DisposeCollaborators();
        OwnedRoot.DestroyOwnedBy(transform);
        ReportRigProblems();
        input = new PlayerInputReader();
        input.PointerSource = scenarioPointerSource;
        machine = new CharacterStateMachine();
        Events = new CharacterEvents();
        rig = BuildRig();
        crouch.Initialize(rig);
        states = BuildStates();
        weaponController = new WeaponController(pistol, weapon, (Events, transform));
        shootingStance = new ShootingStance(rig, (weaponController, shooting, weapon));
        ComposeSecondaryMotion();
        characterEffects = new CharacterEffects(rig, effects);
        characterAudio = new CharacterAudio(rig, audioSettings, characterEffects.Casings);
        OwnedRoot.Mark(weaponController.Tracers.Root, transform);
        OwnedRoot.Mark(characterEffects.Root, transform);
        OwnedRoot.Mark(characterAudio.Root, transform);
        states.Walk.Begin();
    }

    // Full-body state first, then the posture and the upper-body layer on top of it.
    private void Update() {
        PlayerCommands commands = input.ReadCommands();
        latestCommands = commands;
        cameraAim = (false, Vector2.zero);
        weaponController.Tick(Time.time, Time.deltaTime, commands.ReloadPressed);
        characterEffects.Tick(Time.deltaTime);
        characterAudio.Tick((Time.deltaTime, Time.unscaledDeltaTime));
        crouch.Tick(Time.deltaTime, (commands.CrouchRequested, machine.Current.AllowsCrouch));
        machine.Tick(Time.deltaTime, commands);
        posture.Tick(Time.deltaTime);

        if (machine.Current.AllowsUpperBodyActions) {
            PoseUpperBody(commands);
            torsoMotion.Tick(Time.deltaTime);
        }
    }

    // Aiming drives the arms and head; otherwise the head stays level and the arms swing with the legs.
    private void PoseUpperBody(PlayerCommands commands) {
        if (commands.IsAiming) {
            shootingStance.Tick(commands, Time.time);
            cameraAim = (true, shootingStance.AimPoint);
            armSwing.Track(Time.deltaTime);
            return;
        }
        rig.Head.HoldLevel();
        armSwing.Tick(Time.deltaTime);
    }

    private void LateUpdate() {
        rig.ApplyUpperBodyPose(Time.deltaTime);
        followCamera.Tick(Time.deltaTime, cameraAim);
    }

    private CharacterRig BuildRig() {
        CharacterFacing facing = new CharacterFacing((transform, authoredFacingSign));
        motor.Bind(stride.transform);
        stride.Connect((Events, facing));
        frontArm.Initialize(authoredFacingSign);
        backArm.Initialize(authoredFacingSign);
        head.Initialize((transform, authoredFacingSign));
        return new CharacterRig((stride, facing, motor, Events), (frontArm, backArm, head, chest, postureRoot));
    }

    private CharacterStates BuildStates() {
        CharacterStates built = new CharacterStates();
        StateContext context = new StateContext((machine, rig, built));
        built.Register((
            new WalkState(context),
            new JumpWindupState(context, (jump, airborne)),
            new AirborneState(context, (airborne, jump, airPose, fallSecondary, landing)),
            new RollState(context, roll),
            new ClimbState(context, climb),
            new WallSlideState(context, wall)));
        return built;
    }

    private void ReportRigProblems() {
        if (stride == null || stride.FrontLeg == null || stride.BackLeg == null) {
            Debug.LogWarning($"{name}: the stride controller or its legs are not assigned.", this);
            return;
        }
        (Transform foothold, Transform solver) front = stride.FrontLeg.WorldSpaceTargets;
        (Transform foothold, Transform solver) back = stride.BackLeg.WorldSpaceTargets;
        Transform[] worldSpaceTargets = { front.foothold, front.solver, back.foothold, back.solver };
        foreach (string problem in RigValidation.Problems((stride.transform, postureRoot, chest, worldSpaceTargets))) {
            Debug.LogWarning($"{name} rig: {problem}", this);
        }
    }

    private void ComposeSecondaryMotion() {
        posture.Initialize(rig);
        torsoMotion.Initialize(rig);
        armSwing.Initialize(rig);
        followCamera.Initialize(rig.Body, (Events, cameraFeel));
        kickTorsoOnFire = _ => torsoMotion.Kick(shooting.recoilTorsoKick);
        foldTorsoOnLanding = hit => torsoMotion.Kick(-LandingMath.ImpactShare(hit.impactSpeed, landing) * landing.torsoFoldKick);
        Events.Fired += kickTorsoOnFire;
        Events.Landed += foldTorsoOnLanding;
    }
}
