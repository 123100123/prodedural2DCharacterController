// The roll's momentum as the debug tools read it: the current (or last) roll's phase, how it was launched, its speed and
// the deceleration acting on it this frame, the speed it had when the recovery began and the speed it handed on.
public readonly struct RollReadout {
    public RollReadout((bool isRolling, RollMotionPhase phase) state, RollLaunch launch, (float speed, float deceleration, float recoverySpeed, float handedBack) motion) {
        IsRolling = state.isRolling;
        Phase = state.phase;
        Launch = launch;
        Speed = motion.speed;
        Deceleration = motion.deceleration;
        RecoverySpeed = motion.recoverySpeed;
        HandedBack = motion.handedBack;
    }

    public bool IsRolling { get; }
    public RollMotionPhase Phase { get; }
    public RollLaunch Launch { get; }
    // Along the surface (u/s).
    public float Speed { get; }
    // Speed lost per second this frame (negative while the throw speeds the roll up).
    public float Deceleration { get; }
    // The speed when the recovery began (0 before it).
    public float RecoverySpeed { get; }
    // The speed handed to the next state when the roll ended (0 before it ends).
    public float HandedBack { get; }
}
