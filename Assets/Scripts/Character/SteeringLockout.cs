// A short spell after a wall jump in which steering back toward the wall just left is ignored, so the kick carries the
// character away instead of being cancelled at once. Steering away from that wall, or no input, passes through.
public readonly struct SteeringLockout {
    public static readonly SteeringLockout None = default;

    // blockedDirection: +1 blocks steering right, -1 blocks steering left.
    public SteeringLockout(int blockedDirection, float duration) {
        BlockedDirection = blockedDirection;
        TimeLeft = duration > 0f ? duration : 0f;
    }

    public int BlockedDirection { get; }
    public float TimeLeft { get; }
    public bool IsActive => BlockedDirection != 0 && TimeLeft > 0f;

    public SteeringLockout Tick(float deltaTime) => IsActive ? new SteeringLockout(BlockedDirection, TimeLeft - deltaTime) : None;

    // The move input with any push toward the blocked direction removed while the lockout lasts.
    public float Filter(float moveAxis) => IsActive && moveAxis * BlockedDirection > 0f ? 0f : moveAxis;
}
