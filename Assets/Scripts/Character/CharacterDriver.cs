using UnityEngine;

// Composition root for the procedural character: builds the collaborators and runs one ordered tick per frame.
public class CharacterDriver : MonoBehaviour {
    [SerializeField] private StrideController stride;
    [Tooltip("Direction the character faces in the authored rig: -1 left, +1 right.")]
    [SerializeField] private int authoredFacingSign = -1;
    [SerializeField] private KinematicBodyMotor motor = new KinematicBodyMotor();
    [SerializeField] private AirborneSettings airborne = new AirborneSettings();
    [SerializeField] private JumpSettings jump = new JumpSettings();

    private PlayerInputReader input;
    private CharacterStateMachine machine;
    private CharacterStates states;

    private void Awake() {
        input = new PlayerInputReader();
        machine = new CharacterStateMachine();
        states = BuildStates(BuildRig());
    }

    private void OnEnable() => input.Enable();

    private void OnDisable() => input.Disable();

    private void OnDestroy() => input.Dispose();

    private void Start() => states.Walk.Begin();

    private void Update() {
        machine.Tick(Time.deltaTime, input.ReadCommands());
    }

    private CharacterRig BuildRig() {
        motor.Bind(stride.transform);
        CharacterFacing facing = new CharacterFacing((transform, authoredFacingSign));
        return new CharacterRig((stride, facing, motor));
    }

    private CharacterStates BuildStates(CharacterRig rig) {
        CharacterStates built = new CharacterStates();
        StateContext context = new StateContext((machine, rig, built));
        built.Register((
            new WalkState(context),
            new JumpWindupState(context, (jump, airborne)),
            new AirborneState(context, (airborne, jump))));
        return built;
    }
}
