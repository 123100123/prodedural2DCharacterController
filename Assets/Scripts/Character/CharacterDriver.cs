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
    [Tooltip("Upper-spine bone that curls forward during a roll.")]
    [SerializeField] private Transform chest;

    [Header("Moves")]
    [SerializeField] private AirborneSettings airborne = new AirborneSettings();
    [SerializeField] private JumpSettings jump = new JumpSettings();
    [SerializeField] private RollSettings roll = new RollSettings();
    [SerializeField] private ClimbSettings climb = new ClimbSettings();

    [Header("Shooting")]
    [SerializeField] private ShootingSettings shooting = new ShootingSettings();
    [SerializeField] private Pistol pistol = new Pistol();

    private PlayerInputReader input;
    private CharacterStateMachine machine;
    private CharacterStates states;
    private CharacterRig rig;
    private ShootingStance shootingStance;

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

    private void ComposeCharacter() {
        input = new PlayerInputReader();
        machine = new CharacterStateMachine();
        rig = BuildRig();
        states = BuildStates();
        shootingStance = new ShootingStance(rig, (pistol, shooting));
        states.Walk.Begin();
    }

    // Full-body state first, then the upper-body layer on top of it.
    private void Update() {
        PlayerCommands commands = input.ReadCommands();
        machine.Tick(Time.deltaTime, commands);

        if (machine.Current.AllowsUpperBodyActions) {
            shootingStance.Tick(commands, Time.time);
        }
    }

    private void LateUpdate() => rig.ApplyUpperBodyPose(Time.deltaTime);

    private CharacterRig BuildRig() {
        motor.Bind(stride.transform);
        frontArm.Initialize((transform, authoredFacingSign));
        backArm.Initialize((transform, authoredFacingSign));
        head.Initialize((transform, authoredFacingSign));

        CharacterFacing facing = new CharacterFacing((transform, authoredFacingSign));
        return new CharacterRig((stride, facing, motor), (frontArm, backArm, head, chest));
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
}
