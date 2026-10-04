using System;
using UnityEngine.InputSystem;

// Owns the player's input actions (keyboard/mouse and gamepad) and samples them into PlayerCommands.
public sealed class PlayerInputReader : IDisposable {
    // Presses shorter than this toggle aiming instead of holding it (touchpad right-clicks are instant).
    private const float AimTapMaxDuration = 0.25f;
    // Holding crouch crouches while held; a quicker tap latches it on or off.
    private const float CrouchTapMaxDuration = 0.25f;

    private readonly InputAction move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
    private readonly InputAction jump = new InputAction("Jump", InputActionType.Button);
    private readonly InputAction roll = new InputAction("Roll", InputActionType.Button);
    private readonly InputAction run = new InputAction("Run", InputActionType.Button);
    private readonly InputAction aim = new InputAction("Aim", InputActionType.Button);
    private readonly InputAction crouch = new InputAction("Crouch", InputActionType.Button);
    private readonly InputAction fire = new InputAction("Fire", InputActionType.Button);
    private readonly InputAction reload = new InputAction("Reload", InputActionType.Button);
    private readonly InputAction pointer = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
    private readonly HoldOrTapToggle aimMode = new HoldOrTapToggle(AimTapMaxDuration);
    private readonly HoldOrTapToggle crouchMode = new HoldOrTapToggle(CrouchTapMaxDuration);

    // Scenario hook: while set, supplies the pointer's screen position instead of the mouse, so a test's aim does not
    // depend on how the Editor's Game view maps (or remaps) virtual pointer coordinates. Unset in normal play.
    public Func<UnityEngine.Vector2> PointerSource { get; set; }

    public PlayerInputReader() {
        BindMovement();
        BindActions();
    }

    public void Enable() => ForEachAction(action => action.Enable());

    public void Disable() => ForEachAction(action => action.Disable());

    public void Dispose() => ForEachAction(action => action.Dispose());

    // Input an on-screen UI has captured (typing into a field, clicking over it) does not reach gameplay.
    public PlayerCommands ReadCommands() {
        bool isTyping = UiInputCapture.IsKeyboardCaptured;
        bool isPointerOverUi = UiInputCapture.IsPointerCaptured;
        return new PlayerCommands {
            MoveAxis = isTyping ? 0f : move.ReadValue<float>(),
            JumpPressed = !isTyping && jump.WasPressedThisFrame(),
            JumpHeld = !isTyping && jump.IsPressed(),
            RollPressed = !isTyping && roll.WasPressedThisFrame(),
            RunHeld = !isTyping && run.IsPressed(),
            IsAiming = ReadAimMode(isPointerOverUi),
            CrouchRequested = ReadCrouchMode(isTyping),
            FirePressed = !isPointerOverUi && fire.WasPressedThisFrame(),
            ReloadPressed = !isTyping && reload.WasPressedThisFrame(),
            PointerScreenPosition = PointerSource?.Invoke() ?? pointer.ReadValue<UnityEngine.Vector2>(),
        };
    }

    private bool ReadAimMode(bool isPointerOverUi) {
        (bool wentDown, bool wentUp, bool isHeld) button = isPointerOverUi
            ? (false, false, false)
            : (aim.WasPressedThisFrame(), aim.WasReleasedThisFrame(), aim.IsPressed());
        return aimMode.Update(button, UnityEngine.Time.unscaledTime);
    }

    private bool ReadCrouchMode(bool isTyping) {
        (bool wentDown, bool wentUp, bool isHeld) button = isTyping
            ? (false, false, false)
            : (crouch.WasPressedThisFrame(), crouch.WasReleasedThisFrame(), crouch.IsPressed());
        return crouchMode.Update(button, UnityEngine.Time.unscaledTime);
    }

    private void BindMovement() {
        move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
        move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick/x");
    }

    private void BindActions() {
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");
        roll.AddBinding("<Keyboard>/c");
        roll.AddBinding("<Gamepad>/buttonEast");
        run.AddBinding("<Keyboard>/leftShift");
        run.AddBinding("<Gamepad>/leftStickPress");
        aim.AddBinding("<Mouse>/rightButton");
        aim.AddBinding("<Gamepad>/leftTrigger");
        crouch.AddBinding("<Keyboard>/leftCtrl");
        crouch.AddBinding("<Gamepad>/rightStickPress");
        fire.AddBinding("<Mouse>/leftButton");
        fire.AddBinding("<Gamepad>/rightTrigger");
        reload.AddBinding("<Keyboard>/r");
        reload.AddBinding("<Gamepad>/buttonWest");
        pointer.AddBinding("<Pointer>/position");
    }

    private void ForEachAction(Action<InputAction> apply) {
        apply(move);
        apply(jump);
        apply(roll);
        apply(run);
        apply(aim);
        apply(crouch);
        apply(fire);
        apply(reload);
        apply(pointer);
    }
}
