using System;
using UnityEngine.InputSystem;

// Owns the player's input actions (keyboard/mouse and gamepad) and samples them into PlayerCommands.
public sealed class PlayerInputReader : IDisposable {
    // Presses shorter than this toggle aiming instead of holding it (touchpad right-clicks are instant).
    private const float AimTapMaxDuration = 0.25f;

    private readonly InputAction move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
    private readonly InputAction jump = new InputAction("Jump", InputActionType.Button);
    private readonly InputAction roll = new InputAction("Roll", InputActionType.Button);
    private readonly InputAction aim = new InputAction("Aim", InputActionType.Button);
    private readonly InputAction fire = new InputAction("Fire", InputActionType.Button);
    private readonly InputAction pointer = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
    private readonly HoldOrTapToggle aimMode = new HoldOrTapToggle(AimTapMaxDuration);

    public PlayerInputReader() {
        BindMovement();
        BindActions();
    }

    public void Enable() => ForEachAction(action => action.Enable());

    public void Disable() => ForEachAction(action => action.Disable());

    public void Dispose() => ForEachAction(action => action.Dispose());

    public PlayerCommands ReadCommands() {
        return new PlayerCommands {
            MoveAxis = move.ReadValue<float>(),
            JumpPressed = jump.WasPressedThisFrame(),
            RollPressed = roll.WasPressedThisFrame(),
            IsAiming = ReadAimMode(),
            FirePressed = fire.WasPressedThisFrame(),
            PointerScreenPosition = pointer.ReadValue<UnityEngine.Vector2>(),
        };
    }

    private bool ReadAimMode() {
        (bool wentDown, bool wentUp, bool isHeld) button = (aim.WasPressedThisFrame(), aim.WasReleasedThisFrame(), aim.IsPressed());
        return aimMode.Update(button, UnityEngine.Time.unscaledTime);
    }

    private void BindMovement() {
        move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
        move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick/x");
    }

    private void BindActions() {
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");
        roll.AddBinding("<Keyboard>/leftShift");
        roll.AddBinding("<Gamepad>/buttonEast");
        aim.AddBinding("<Mouse>/rightButton");
        aim.AddBinding("<Gamepad>/leftTrigger");
        fire.AddBinding("<Mouse>/leftButton");
        fire.AddBinding("<Gamepad>/rightTrigger");
        pointer.AddBinding("<Pointer>/position");
    }

    private void ForEachAction(Action<InputAction> apply) {
        apply(move);
        apply(jump);
        apply(roll);
        apply(aim);
        apply(fire);
        apply(pointer);
    }
}
