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
    private const int ContactCapacity = 8;

    private readonly CharacterRig rig;
    private readonly PushSettings settings;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[ContactCapacity];
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
        // At the face, level with the crate's centre of mass, so the push slides it rather than tipping it over. Along the
        // ground: flat, or up the ramp the crate has reached the foot of (its speed targets are then along the ramp).
        Vector2 along = Vector2.right * direction;
        float force;
        if (TryFindSlopeAhead(out Vector2 normal)) {
            along = new Vector2(normal.y, -normal.x) * direction;
            float gravity = Physics2D.gravity.magnitude * body.gravityScale;
            float load = PushRules.SlopeLoad((body.mass, gravity), SlopeMath.AngleOf(normal));
            float speedAlong = Vector2.Dot(body.linearVelocity, along);
            force = PushRules.SlopeForce((body.mass, speedAlong, target / normal.y), (load, settings.maxForce, fixedDeltaTime));
        }
        else {
            force = PushRules.CrateForce((body.mass, AlongPush(body.linearVelocity.x), target), (settings.maxForce, fixedDeltaTime));
        }
        if (force <= 0f) {
            return;
        }
        Vector2 point = new Vector2(body.position.x + faceOffsetX, body.worldCenterOfMass.y);
        body.AddForceAtPosition(along * force, point, ForceMode2D.Force);
        LastForce = force;
    }

    // Whether the body is walking up a slope in the push direction.
    private bool IsClimbingToward(StrideController stride) {
        float gradient = stride.SlopeGradient * direction;
        return gradient > 0f && SlopeMath.AngleOfGradient(gradient) > SlopeMath.MinSlopeAngle;
    }

    // Whether the crate rests on a walkable slope that rises in the push direction (a ramp it is being pushed up), and the
    // slope's normal.
    private bool TryFindSlopeAhead(out Vector2 normal) {
        normal = Vector2.up;
        float maxAngle = rig.Stride.Slope.maxSlopeAngle;
        int count = body.GetContacts(contacts);
        for (int index = 0; index < count; index++) {
            Vector2 candidate = contacts[index].normal;
            if (candidate.x * direction < 0f && SlopeMath.IsSloped(candidate, maxAngle)) {
                normal = candidate;
                return true;
            }
        }
        return false;
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

    // Only a crate standing about where the feet stand is pushed (not one up on a ledge at head height). A crate already
    // held that is riding up a ramp (or has just been pushed onto the top) is lifted off the feet's level by the ramp itself,
    // while the body climbs the same ramp behind it.
    private bool StandsOnOurGround(PushProbe.Hit hit, StrideController stride) {
        if (hit.Collider == null) {
            return false;
        }
        bool isRidingUp = IsPushing && hit.Body == body && (TryFindSlopeAhead(out _) || IsClimbingToward(stride));
        return isRidingUp || hit.Bottom <= stride.StanceHeight + stride.MaxStepUp;
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
