using UnityEngine;

// What the draw cycle and the hands on the pistol are doing right now, for the debug tools.
public readonly struct WeaponReadout {
    public WeaponReadout((DrawState state, float progress, bool quick) cycle, (bool inHand, string parent) carrier, (float grip, float support, float supportWeight) hands, (Vector2 muzzle, bool fireBuffered) shot) {
        State = cycle.state;
        Progress = cycle.progress;
        IsQuick = cycle.quick;
        InHand = carrier.inHand;
        Parent = carrier.parent;
        GripError = hands.grip;
        SupportError = hands.support;
        SupportWeight = hands.supportWeight;
        Muzzle = shot.muzzle;
        FireBuffered = shot.fireBuffered;
    }

    public DrawState State { get; }
    public float Progress { get; }
    // Holstering at the quick time (a roll, climb or wall slide took the arms).
    public bool IsQuick { get; }
    public bool InHand { get; }
    // The name of what carries the pistol: the Holster or the WeaponPivot.
    public string Parent { get; }
    // How far the hand's grip point (the weapon pivot) is from the pistol's grip.
    public float GripError { get; }
    // How far the support hand is from its place behind the front hand, and how much it is meant to be there.
    public float SupportError { get; }
    public float SupportWeight { get; }
    public Vector2 Muzzle { get; }
    // A fire press waiting for the draw to reach the aim.
    public bool FireBuffered { get; }
}

// Carries the pistol between the holster and the aim (plan §11-13). Every frame it runs the draw cycle from the aim and
// fire input and from whether the full-body state lets the upper body act, moves the pistol between the holster and the
// hand when the cycle grips or inserts it, and, while drawing or holstering, takes the hands along the draw path: the
// front hand from its swing (or whatever posed it) to the holster grip, then up along an arc to the aim grip, the
// support hand joining near the top; holstering is the same path backwards. It only asks the arms where to reach.
// When the state takes the arms (roll, climb, wall slide) a drawn pistol is quick-holstered: only the front arm is
// overridden, layered over the state's own request, and the path itself hands the arm back to that request.
public sealed class PistolHolder {
    // The share of the path above the grip over which the held pistol leaves (or comes back onto) the holster's grip:
    // the hand may lag its path, so the pistol is seated in the holster at the grip and the insert, and blends into the
    // hand from there.
    private const float SeatWindow = 0.25f;
    // The most the front hand's request is led ahead of its path (units).
    private const float MaxLead = 0.6f;

    private readonly CharacterRig rig;
    private readonly PistolView view;
    private readonly PistolHandlingSettings settings;
    private readonly ShootingSettings shooting;
    private readonly DrawCycle cycle;
    // The last aim grip, as an offset from the front shoulder and a barrel direction, with x along the facing: held so a
    // holster without aim starts from where the pistol was aimed, and mirrored with the body if it turns meanwhile.
    private (Vector2 reach, Vector2 direction) aimInFacing = (new Vector2(1.3f, 0f), Vector2.right);
    private bool showsReload;
    // The path target last frame and the facing it was in, for leading the hand's request.
    private (Vector2 target, int facing)? lastPathTarget;

    public PistolHolder(CharacterRig rig, (PistolView view, PistolHandlingSettings settings, ShootingSettings shooting) parts) {
        this.rig = rig;
        view = parts.view;
        settings = parts.settings;
        shooting = parts.shooting;
        cycle = new DrawCycle(settings);
    }

    public DrawCycle Cycle => cycle;
    public DrawState State => cycle.State;
    public bool IsEquipped => cycle.State == DrawState.Equipped;
    // 0 holstered, 1 at the aim: the shooting stance poses the arms itself only at 1.
    public float AimShare => cycle.AimShare;
    public bool CanFire => cycle.CanFire;
    // A shot is due this frame: pressed while equipped, or buffered during the draw that has just reached the aim.
    public bool FiresNow { get; private set; }
    // The reload motion (support hand to the magazine, pistol tilted) shows only for a reload that began with the pistol
    // at the aim and has stayed there; any other reload finishes out of sight.
    public bool ShowsReload => showsReload;
    // Drawing or holstering: the holder has the hands.
    public bool PosesHands => cycle.State == DrawState.Drawing || cycle.State == DrawState.Holstering;

    public WeaponReadout Readout {
        get {
            Vector2 hand = rig.FrontArm.HandPosition;
            Vector2 support = hand - view.BarrelDirection * shooting.supportHandOffset;
            float gripError = Vector2.Distance(view.WeaponPivot.position, view.Pistol.position);
            float supportError = Vector2.Distance(rig.BackArm.HandPosition, support);
            return new WeaponReadout(
                (cycle.State, cycle.Progress, cycle.IsQuick),
                (view.IsInHand, view.Parent != null ? view.Parent.name : "-"),
                (gripError, supportError, cycle.SupportWeight),
                (view.Muzzle.position, cycle.FireBuffered));
        }
    }

    // Once per frame after the full-body state: advances the cycle and moves the pistol to the hand or the holster at
    // the grip and the insert (world-preserving, so it does not jump).
    public void Tick(PlayerCommands commands, (bool allowsActions, bool reloading) body, float deltaTime) {
        DrawStep step = cycle.Tick((commands.IsAiming, commands.FirePressed, body.allowsActions), deltaTime);
        if (step.Attached) {
            view.AttachToHand();
        }
        if (step.Detached) {
            view.AttachToHolster();
        }
        FiresNow = step.FireNow;
        showsReload = body.reloading ? showsReload && IsEquipped : IsEquipped;
    }

    // After whatever posed the arms this frame (the swing, a push, the state's own pose): the aim grip the shooting
    // stance worked out (null when not aiming), then, while drawing or holstering, the hands along the draw path.
    // With actions not allowed only the front arm is taken.
    public void PoseHands((Vector2 grip, Vector2 direction)? aim, bool allowsActions, float deltaTime) {
        if (aim.HasValue) {
            Remember(aim.Value);
        }
        if (!PosesHands) {
            view.SeatShare = 0f;
            lastPathTarget = null;
            return;
        }
        (Vector2 grip, Vector2 direction) target = aim ?? Remembered();
        ArmLimb front = rig.FrontArm;
        Vector2 underneath = front.RequestedTarget ?? front.HandPosition;
        // The hand target that puts the weapon pivot (where the grip sits in the hand) on the holstered pistol's grip.
        Vector2 pivotOffset = (Vector2)view.WeaponPivot.position - front.HandPosition;
        Vector2 holsterHand = view.HolsterGrip - pivotOffset;
        Vector2 holsterDirection = view.HolsteredDirection;

        float path = cycle.PathPosition;
        float grip = cycle.GripShare;
        Vector2 hand;
        Vector2 barrel = holsterDirection;
        if (path < grip) {
            hand = Arc((underneath, holsterHand), Smooth(path / grip));
        } else {
            float carry = Smooth((path - grip) / (1f - grip));
            hand = Arc((holsterHand, target.grip), carry);
            barrel = TurnTowardFront((holsterDirection, target.direction), carry);
        }
        front.ReachToward(Lead(hand, deltaTime));
        view.SeatShare = path < grip ? 1f : 1f - Smooth((path - grip) / SeatWindow);
        if (view.IsInHand) {
            view.SetBarrelDirection(barrel);
        }
        if (!allowsActions) {
            return;
        }
        float supportWeight = cycle.SupportWeight;
        if (supportWeight > 0f) {
            ArmLimb back = rig.BackArm;
            Vector2 supportGrip = hand - barrel * shooting.supportHandOffset;
            back.ReachToward(Vector2.Lerp(back.RequestedTarget ?? back.HandPosition, supportGrip, supportWeight));
        }
    }

    // The arm eases toward its request, so a request that moves along a path is followed a few frames late. Leading the
    // request by the path's last step, scaled by how much of the gap the arm closes per frame, keeps the hand on the path
    // (at the holster when it grips and inserts). Not across a turn of the facing, where the path mirrors in one frame.
    private Vector2 Lead(Vector2 target, float deltaTime) {
        int facing = rig.Facing.Sign;
        Vector2 led = target;
        float ease = rig.FrontArm.EaseFraction(deltaTime);
        if (lastPathTarget.HasValue && lastPathTarget.Value.facing == facing && ease > 0f && ease < 1f) {
            Vector2 step = target - lastPathTarget.Value.target;
            led += Vector2.ClampMagnitude(step * ((1f - ease) / ease), MaxLead);
        }
        lastPathTarget = (target, facing);
        return led;
    }

    private void Remember((Vector2 grip, Vector2 direction) aim) {
        int facing = rig.Facing.Sign;
        Vector2 reach = aim.grip - rig.FrontArm.ShoulderPosition;
        aimInFacing = (new Vector2(reach.x * facing, reach.y), new Vector2(aim.direction.x * facing, aim.direction.y));
    }

    private (Vector2 grip, Vector2 direction) Remembered() {
        int facing = rig.Facing.Sign;
        Vector2 reach = new Vector2(aimInFacing.reach.x * facing, aimInFacing.reach.y);
        return (rig.FrontArm.ShoulderPosition + reach, new Vector2(aimInFacing.direction.x * facing, aimInFacing.direction.y));
    }

    // Along the chord from one point to the other, bowed up (on the side of the chord away from the ground) by the arc
    // lift's share of its length at the middle. The same either way along it, so a reversal retraces it.
    private Vector2 Arc((Vector2 from, Vector2 to) chord, float share) {
        Vector2 along = chord.to - chord.from;
        Vector2 across = new Vector2(-along.y, along.x);
        bool flip = Mathf.Abs(across.y) > 1e-4f ? across.y < 0f : across.x * rig.Facing.Sign < 0f;
        if (flip) {
            across = -across;
        }
        float bow = 4f * share * (1f - share) * settings.handArcLift;
        return Vector2.Lerp(chord.from, chord.to, share) + across * bow;
    }

    // Turns the barrel from the holstered direction to the aim through the front (never over the head or behind the back):
    // the angles are taken with x along the facing, where both lie on the front half, and blended linearly.
    private Vector2 TurnTowardFront((Vector2 from, Vector2 to) directions, float share) {
        int facing = rig.Facing.Sign;
        float from = Mathf.Atan2(directions.from.y, directions.from.x * facing);
        float to = Mathf.Atan2(directions.to.y, directions.to.x * facing);
        float angle = Mathf.Lerp(from, to, share);
        return new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));
    }

    private static float Smooth(float t) {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
