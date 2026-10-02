using UnityEngine;

// Composition root for the procedural character: builds the collaborators and runs one ordered tick per frame.
public class CharacterDriver : MonoBehaviour {
    [SerializeField] private StrideController stride;
    [Tooltip("Direction the character faces in the authored rig: -1 left, +1 right.")]
    [SerializeField] private int authoredFacingSign = -1;

    private PlayerInputReader input;
    private CharacterStateMachine machine;
    private WalkState walk;

    private void Awake() {
        input = new PlayerInputReader();
        machine = new CharacterStateMachine();
        CharacterFacing facing = new CharacterFacing((transform, authoredFacingSign));
        walk = new WalkState(machine, (stride, facing));
    }

    private void OnEnable() => input.Enable();

    private void OnDisable() => input.Disable();

    private void OnDestroy() => input.Dispose();

    private void Start() => walk.Begin();

    private void Update() {
        machine.Tick(Time.deltaTime, input.ReadCommands());
    }
}
