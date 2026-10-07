// Why a full-body state cannot hand the character over to a grounded run right now. An enum, so reading it every frame
// (the driver, the F1 telemetry) allocates nothing.
public enum RunBlock {
    None,
    // Aiming slows the character to a walk.
    Aiming,
    // The crouch button is physically held: a held crouch wins over the run intent (a crouch walk).
    CrouchHeld,
    // Only a quick tap's latch holds the crouch: the run intent cancels it.
    CrouchLatched,
    // A ceiling too low to stand under keeps the character crouched until there is room.
    NoHeadroom,
    // Pushing a crate limits the gait's speed.
    Pushing,
    Airborne,
    JumpWindup,
    // Diving, planting the hands or rolling over: the roll cannot be left.
    RollCommitted,
    // Getting up out of the roll: with the run intent along the roll it hands over to the run part way through.
    RollRecovering,
    Climbing,
    WallSliding,
    Sliding,
}

// Each full-body state's answer to "could the character run now, and if not, why not". Only a state that can run
// (the grounded walk) ever answers yes; every other state names what it is busy with, and acts on the run intent at its
// own legal exit.
public readonly struct RunReadiness {
    // What the F1 panel shows for each reason, built once (indexed by RunBlock).
    private static readonly string[] ReasonNames = {
        "none", "aiming", "crouch held", "crouch latched", "low ceiling", "pushing", "airborne", "jump windup",
        "roll committed", "roll recovering", "climbing", "wall sliding", "sliding",
    };

    public static readonly RunReadiness Ready = new RunReadiness(RunBlock.None);

    public RunReadiness(RunBlock block) {
        Block = block;
    }

    public RunBlock Block { get; }
    public bool CanRun => Block == RunBlock.None;
    // A cached human-readable reason (no allocation).
    public string Reason => ReasonNames[(int)Block];

    public static RunReadiness BlockedBy(RunBlock block) => new RunReadiness(block);

    // The grounded walk: aiming slows it to a walk; a held crouch wins over the run; a low ceiling keeps the crouch; a
    // crouch only the tap latch holds is the one block the run intent clears (the driver drops the latch); a push
    // limits the speed. Otherwise the walk runs as soon as the run intent is there.
    public static RunReadiness OnFeet((bool isAiming, (bool requested, bool held, bool headroomBlocked) crouch, bool isPushing) walk) {
        if (walk.isAiming) {
            return BlockedBy(RunBlock.Aiming);
        }
        if (walk.crouch.held) {
            return BlockedBy(RunBlock.CrouchHeld);
        }
        if (walk.crouch.headroomBlocked) {
            return BlockedBy(RunBlock.NoHeadroom);
        }
        if (walk.crouch.requested) {
            return BlockedBy(RunBlock.CrouchLatched);
        }
        return walk.isPushing ? BlockedBy(RunBlock.Pushing) : Ready;
    }
}
