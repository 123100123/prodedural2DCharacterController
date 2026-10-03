using UnityEngine;

// Composition root for the procedural character: builds the collaborators and runs one ordered tick per frame.
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
    [SerializeField] private AirborneSettings airborne = new AirborneSettings();
    [SerializeField] private JumpSettings jump = new JumpSettings();
    [SerializeField] private RollSettings roll = new RollSettings();
    [SerializeField] private ClimbSettings climb = new ClimbSettings();

    [Header("Shooting")]
    [SerializeField] private ShootingSettings shooting = new ShootingSettings();
    [SerializeField] private Pistol pistol = new Pistol();

    [Header("Camera")]
    [SerializeField] private FollowCamera followCamera = new FollowCamera();

    private PlayerInputReader input;
    private CharacterStateMachine machine;
    private CharacterStates states;
    private CharacterRig rig;
    private ShootingStance shootingStance;
    private PlayerCommands latestCommands;

    public CharacterEvents Events { get; private set; }
    // Read-only telemetry, runtime tuning and the gait preview for debug tools; the character never reads it back.
    public LocomotionInspector Inspector { get; private set; }

    // Composes here rather than in Awake: a script recompile during play mode reloads the domain,
    // which clears these non-serialized collaborators and calls OnEnable again, but not Awake.
    private void OnEnable() {
        if (input == null) {
            ComposeCharacter();
        }
        input.Enable();
    }

    private void OnDisable() => input?.Disable();

    private void OnDestroy() => input?.Dispose();

    // Reports a broken rig while editing, as soon as the references change.
    private void OnValidate() {
        if (stride != null && chest != null) {
            ReportRigProblems();
        }
    }

    private void ComposeCharacter() {
        ReportRigProblems();
        input = new PlayerInputReader();
        machine = new CharacterStateMachine();
        Events = new CharacterEvents();
        rig = BuildRig();
        states = BuildStates();
        shootingStance = new ShootingStance(rig, (pistol, shooting));
        ComposeSecondaryMotion();
        Inspector = BuildInspector();
        states.Walk.Begin();
    }

    // Full-body state first, then the posture and the upper-body layer on top of it.
    private void Update() {
        PlayerCommands commands = input.ReadCommands();
        latestCommands = commands;
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
            return;
        }
        rig.Head.HoldLevel();
        armSwing.Tick(Time.deltaTime);
    }

    private void LateUpdate() {
        rig.ApplyUpperBodyPose(Time.deltaTime);
        followCamera.Tick(Time.deltaTime);
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
            new AirborneState(context, (airborne, jump)),
            new RollState(context, roll),
            new ClimbState(context, climb)));
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

    private LocomotionInspector BuildInspector() {
        LocomotionTuning tuning = new LocomotionTuning((stride, posture, torsoMotion, head, armSwing));
        LocomotionTelemetry telemetry = new LocomotionTelemetry(stride, (posture, torsoMotion, head, postureRoot));
        return new LocomotionInspector((stride, tuning, telemetry, () => latestCommands));
    }

    private void ComposeSecondaryMotion() {
        posture.Initialize(rig);
        torsoMotion.Initialize(rig);
        armSwing.Initialize(rig);
        followCamera.Initialize(rig.Body, Events);
        Events.Fired += _ => torsoMotion.Kick(shooting.recoilTorsoKick);
    }
}
