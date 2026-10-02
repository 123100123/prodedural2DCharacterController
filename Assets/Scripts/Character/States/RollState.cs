using UnityEngine;

// Forward roll: crouch, hop toward the facing direction, spin once around the pelvis while tucked, then stand back up.
public sealed class RollState : ICharacterState {
    private const float FullTurn = 360f;
    private const float GroundProbeHeadroom = 0.5f;
    private const float StandingPhaseStart = 0.85f;

    private readonly StateContext context;
    private readonly RollSettings settings;

    private float elapsed;
    private float readyTime;
    private int direction;
    private (Quaternion body, Quaternion chest) uprightPose;
    private (Vector2 leadFoot, Vector2 trailFoot, Vector2 hands) tuckInBodySpace;
    private (LegStepper lead, LegStepper trail) legs;

    public RollState(StateContext context, RollSettings settings) {
        this.context = context;
        this.settings = settings;
    }

    public bool AllowsUpperBodyActions => false;

    public bool IsReady => Time.time >= readyTime;

    public void Begin() => context.Machine.ChangeTo(this);

    public void Enter() {
        elapsed = 0f;
        direction = context.Rig.Facing.Sign;
        uprightPose = (context.Rig.Body.localRotation, context.Rig.Chest.localRotation);
        tuckInBodySpace = TuckPoseInBodySpace();
        legs = context.Rig.Stride.LegsOrderedByFacing(direction);
        context.Rig.Motor.Velocity = Vector2.zero;
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        elapsed += deltaTime;
        float t = Mathf.Clamp01(elapsed / settings.duration);

        TravelForward((t, deltaTime));
        if (!TryFollowGround((t, deltaTime), out float groundY)) {
            context.States.Airborne.Fall(context.Rig.Motor.Velocity);
            return;
        }

        CurlAndSpin(t);
        PoseLimbs((t, deltaTime), groundY);
        context.Rig.Stride.PlaceStrideTargetUnderBody();

        if (t >= 1f) {
            StandUp();
        }
    }

    public void Exit() {
        context.Rig.Body.localRotation = uprightPose.body;
        context.Rig.Chest.localRotation = uprightPose.chest;
        readyTime = Time.time + settings.cooldown;
    }

    // Tuck offsets are authored relative to the upright body, then carried in body space so they spin with it.
    private (Vector2 leadFoot, Vector2 trailFoot, Vector2 hands) TuckPoseInBodySpace() {
        return (BodySpacePoint(settings.leadFootTucked), BodySpacePoint(settings.trailFootTucked), BodySpacePoint(settings.handsTucked));
    }

    private Vector2 BodySpacePoint(Vector2 forwardOffset) {
        Transform body = context.Rig.Body;
        Vector3 world = body.position + new Vector3(forwardOffset.x * direction, forwardOffset.y, 0f);
        return body.InverseTransformPoint(world);
    }

    private void TravelForward((float t, float deltaTime) frame) {
        KinematicBodyMotor motor = context.Rig.Motor;
        float speed = settings.IsTravelling(frame.t) ? settings.TravelSpeed * direction : 0f;
        motor.Velocity = new Vector2(speed, 0f);
        motor.MoveWithCollisions(frame.deltaTime);
    }

    private bool TryFollowGround((float t, float deltaTime) frame, out float groundY) {
        Transform body = context.Rig.Body;
        float rollHeight = settings.bodyHeight.Evaluate(frame.t);
        Vector2 origin = (Vector2)body.position + Vector2.up * GroundProbeHeadroom;
        float depth = GroundProbeHeadroom + rollHeight + settings.maxGroundDrop;

        bool hasGround = FootholdProbe.TryFindGroundBelow((origin, depth), context.Rig.Stride.GroundMask, out Vector2 ground);
        groundY = ground.y;
        if (hasGround) {
            float ease = FrameRateEase.FractionForFrame(settings.groundFollowEase, frame.deltaTime);
            float bodyY = Mathf.Lerp(body.position.y, groundY + rollHeight, ease);
            body.position = new Vector3(body.position.x, bodyY, body.position.z);
        }
        return hasGround;
    }

    // Rotations are in the rig's authored space; mirroring the root turns them into a forward roll for either facing.
    private void CurlAndSpin(float t) {
        float forwardTurnSign = -context.Rig.Facing.AuthoredSign;
        float spin = FullTurn * settings.SpinAmount(t) * forwardTurnSign;
        float curl = settings.chestCurlAngle * settings.CurlAmount(t) * forwardTurnSign;

        context.Rig.Body.localRotation = Quaternion.Euler(0f, 0f, spin) * uprightPose.body;
        context.Rig.Chest.localRotation = Quaternion.Euler(0f, 0f, curl) * uprightPose.chest;
    }

    private void PoseLimbs((float t, float deltaTime) frame, float groundY) {
        if (frame.t < settings.travelWindow.x) {
            return;
        }

        float ease = FrameRateEase.FractionForFrame(settings.limbPoseEase, frame.deltaTime);
        if (frame.t >= StandingPhaseStart) {
            ReachFeetForGround((groundY, ease));
            return;
        }

        Transform body = context.Rig.Body;
        legs.lead.SwingFreelyToward(body.TransformPoint(tuckInBodySpace.leadFoot), ease);
        legs.trail.SwingFreelyToward(body.TransformPoint(tuckInBodySpace.trailFoot), ease);
        context.Rig.FrontArm.ReachToward(body.TransformPoint(tuckInBodySpace.hands));
        context.Rig.BackArm.ReachToward(body.TransformPoint(tuckInBodySpace.hands));
    }

    private void ReachFeetForGround((float groundY, float ease) reach) {
        float bodyX = context.Rig.Body.position.x;
        float halfWidth = context.Rig.Stride.StanceHalfWidth;
        legs.lead.SwingFreelyToward(new Vector2(bodyX + direction * halfWidth, reach.groundY), reach.ease);
        legs.trail.SwingFreelyToward(new Vector2(bodyX - direction * halfWidth, reach.groundY), reach.ease);
    }

    private void StandUp() {
        if (context.Rig.Stride.TrySettleStance((context.Rig.Body.position.x, direction))) {
            context.States.Walk.Begin();
            return;
        }
        context.States.Airborne.Fall(Vector2.zero);
    }
}
