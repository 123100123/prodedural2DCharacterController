using UnityEngine;

// Pushes a dynamic Rigidbody2D (a crate) the walking character presses into. Each frame, before the gait steps, the walk
// asks it: it finds the crate in front of the body (PushProbe), takes hold or lets go with hysteresis (PushRules), and
// limits the gait to the crate's speed plus a slack scaled by the room left ahead, so the body follows the crate at its
// contact skin instead of running into its face and stopping dead. The force goes in at the physics rate: the driver
// calls FixedTick from FixedUpdate, which drives the crate's velocity toward the push speed (never more than a slack ahead
// of the character) with a force capped at the push's strength; friction, mass and whatever is behind the crate decide
// how fast it really goes, and the character follows that.
//
// Room is measured from the gait's anchor (where the body is heading), not from the body: the body sways around the
// anchor with every step, the anchor does not, so the contact never flickers. The crate's face is extrapolated from its
// last physics step to this frame, so frames without a physics step do not read as the crate stopping.
public sealed class CratePusher {
    // A crate slower than this (u/s) is standing still: physics settling, not motion.
    private const float StillSpeed = 0.02f;

    private readonly CharacterRig rig;
    private readonly PushSettings settings;
    // The crate held, its collider and the side it is on (+1 right of the body).
    private Rigidbody2D body;
    private Collider2D crateCollider;
    private int direction;
    // The pushed face relative to the crate's physics position, and the crate's extent relative to it.
    private float faceOffsetX;
    private (float bottom, float top) heightOffsets;
    // The character's speed along the push at the last frame, for the force at the physics rate.
    private float characterSpeed;

    public CratePusher(CharacterRig rig, PushSettings settings) {
        this.rig = rig;
        this.settings = settings;
    }

    public bool IsPushing { get; private set; }
    // +1 pushing right, -1 left; 0 when not pushing.
    public int Direction => IsPushing ? direction : 0;
    // The crate held; null when not pushing.
    public Rigidbody2D Crate => IsPushing ? body : null;
    // How far the crate's face is ahead of where the body is heading (u); infinity when not pushing.
    public float Room { get; private set; } = float.PositiveInfinity;
    // The crate's speed along the push (u/s) and the speed the gait may walk at, at the last frame.
    public float CrateSpeed { get; private set; }
    public float SpeedLimit { get; private set; } = float.PositiveInfinity;
    // The force (N) the last physics step put into the crate along the push.
    public float LastForce { get; private set; }

    // Where the pushed face is drawn (the crate's rendered, interpolated position) and how far the crate reaches down
    // and up: what the hands press on.
    public (float faceX, float bottom, float top) VisibleFace {
        get {
            if (!IsPushing || body == null) {
                return (0f, 0f, 0f);
            }
            Vector2 drawn = body.transform.position;
            return (drawn.x + faceOffsetX, drawn.y + heightOffsets.bottom, drawn.y + heightOffsets.top);
        }
    }

    // Called by the walk every frame before the gait steps.
    public void Tick(PlayerCommands commands) {
        StrideController stride = rig.Stride;
        int side = PushRules.PressedSide(commands.MoveAxis);
        bool canPush = settings.enabled && side != 0 && !commands.IsAiming && !stride.HasLostGround;
        PushProbe.Hit hit = default;
        bool found = canPush && PushProbe.TryFind(rig, (side, stride.WallProbeDistance), out hit) && StandsOnOurGround(hit, stride);
        float room = found ? RoomAhead(hit) : float.PositiveInfinity;

        bool continues = IsPushing && found && hit.Body == body && side == direction && PushRules.Holds(true, (true, room), settings);
        if (IsPushing && !continues) {
            Stop();
        }
        if (!IsPushing && found && PushRules.Holds(false, (true, room), settings)) {
            Start(hit);
        }

        characterSpeed = Mathf.Max(0f, stride.GroundSpeed * side);
        if (!IsPushing) {
            Room = float.PositiveInfinity;
            CrateSpeed = 0f;
            SpeedLimit = float.PositiveInfinity;
            stride.SpeedLimit = float.PositiveInfinity;
            return;
        }
        Room = room;
        CrateSpeed = AlongPush(body.linearVelocity.x);
        SpeedLimit = PushRules.CharacterSpeed((CrateSpeed, room), settings);
        stride.SpeedLimit = SpeedLimit;
    }

    // Drives the crate at the physics rate; the driver calls it from FixedUpdate.
    public void FixedTick(float fixedDeltaTime) {
        LastForce = 0f;
        if (!IsPushing || !PushProbe.IsPushable(body)) {
            return;
        }
        float target = PushRules.CrateTargetSpeed(characterSpeed, settings);
        float force = PushRules.CrateForce((body.mass, AlongPush(body.linearVelocity.x), target), (settings.maxForce, fixedDeltaTime));
        if (force <= 0f) {
            return;
        }
        // At the face, level with the crate's centre of mass, so the push slides it rather than tipping it over.
        Vector2 point = new Vector2(body.position.x + faceOffsetX, body.worldCenterOfMass.y);
        body.AddForceAtPosition(Vector2.right * (direction * force), point, ForceMode2D.Force);
        LastForce = force;
    }

    // Lets go (the walk ends: a jump, a fall, a roll, a slide).
    public void Release() {
        if (IsPushing) {
            Stop();
        }
        Room = float.PositiveInfinity;
        CrateSpeed = 0f;
        SpeedLimit = float.PositiveInfinity;
        rig.Stride.SpeedLimit = float.PositiveInfinity;
    }

    private void Start(PushProbe.Hit hit) {
        IsPushing = true;
        body = hit.Body;
        crateCollider = hit.Collider;
        direction = hit.Direction;
        Remember(hit);
        rig.Events.RaisePushStarted((crateCollider, direction));
    }

    private void Stop() {
        IsPushing = false;
        body = null;
        crateCollider = null;
        rig.Events.RaisePushStopped();
    }

    // Only a crate standing about where the feet stand is pushed (not one up on a ledge at head height).
    private static bool StandsOnOurGround(PushProbe.Hit hit, StrideController stride) {
        return hit.Collider != null && hit.Bottom <= stride.StanceHeight + stride.MaxStepUp;
    }

    // The face's x extrapolated from the crate's last physics step to now, less where the hull's face will be when the
    // body reaches the anchor (the hull half-width and the motor's skin), along the push.
    private float RoomAhead(PushProbe.Hit hit) {
        Rigidbody2D crate = hit.Body;
        float sincePhysicsStep = Mathf.Max(0f, Time.time - Time.fixedTime);
        float faceX = hit.FaceX + crate.linearVelocity.x * sincePhysicsStep;
        if (IsPushing && crate == body) {
            Remember(hit);
        }
        return (faceX - rig.Stride.StrideAnchorX) * hit.Direction - rig.Motor.HullHalfWidth - KinematicBodyMotor.SkinWidth;
    }

    private void Remember(PushProbe.Hit hit) {
        Vector2 physics = hit.Body.position;
        faceOffsetX = hit.FaceX - physics.x;
        heightOffsets = (hit.Bottom - physics.y, hit.Top - physics.y);
    }

    private float AlongPush(float velocityX) {
        float along = velocityX * direction;
        return Mathf.Abs(along) < StillSpeed ? 0f : along;
    }
}
