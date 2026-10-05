using UnityEngine;

// The pose of a push, blended in and out on a spring over about the blend time: the torso leans into the crate (through
// the rig's push lean, which the locomotion posture adds), both hands press flat on the crate's face a little below the
// shoulders, and the head glances down at them. The gait itself only slows (the push limits its speed), so the steps
// shorten and slow down with the walk's own timing. The arms blend over whatever the arm swing asked for; aiming lets go
// of the push and takes the arms, so the push pose is posed only when the arms are free. The last face pushed is kept
// while the pose blends out, so the hands leave it smoothly.
public sealed class PushPose {
    // Hands reach at most this share of the arm, so the elbow never locks.
    private const float MaxArmExtension = 0.985f;
    // The palms sit this far off the face (on the surface, not in it).
    private const float HandFaceGap = 0.03f;
    // Hands stay at least this far inside the face's top and bottom edges.
    private const float HandEdgeMargin = 0.15f;
    // The back hand presses a little lower than the front one, so the two read as two hands.
    private const float BackHandDrop = 0.12f;
    // The head turns this share of the way from straight ahead toward the hands: a glance down at the crate, not a nod.
    private const float HeadTowardHands = 0.5f;
    // Below this weight the pose leaves the arms and head alone.
    private const float PosedWeight = 0.001f;

    private readonly CharacterRig rig;
    private readonly CratePusher pusher;
    private readonly PushSettings settings;
    private readonly SecondOrderDynamics blend = new SecondOrderDynamics(0f);
    private (float faceX, float bottom, float top) face;
    private int direction;

    public PushPose(CharacterRig rig, (CratePusher pusher, PushSettings settings) push) {
        this.rig = rig;
        pusher = push.pusher;
        settings = push.settings;
    }

    // How far the pose is blended in: 0 not pushing, 1 fully pushing.
    public float Weight { get; private set; }

    // Every frame, before the posture: the blend toward pushing or not, and the lean it holds.
    public void Tick(float deltaTime) {
        if (pusher.IsPushing) {
            face = pusher.VisibleFace;
            direction = pusher.Direction;
        }
        if (deltaTime > 0f) {
            float blended = blend.Update((pusher.IsPushing ? 1f : 0f, deltaTime), PushRules.BlendSpring(settings.blendTime));
            Weight = Mathf.Clamp01(blended);
        }
        rig.PushLean = Weight * settings.lean;
    }

    // After the arm swing, when the arms are free (not aiming): the hands to the face and the head to the hands.
    public void PoseArmsAndHead() {
        // Turned away from the crate it let go of (walking off the other way), the hands would reach behind the back: the
        // arms go straight back to the swing instead, through their own pose easing.
        bool turnedAway = !pusher.IsPushing && rig.Facing.Sign != direction;
        if (Weight <= PosedWeight || direction == 0 || turnedAway) {
            return;
        }
        Vector2 frontHand = HandOnFace(rig.FrontArm, 0f);
        Vector2 backHand = HandOnFace(rig.BackArm, -BackHandDrop);
        BlendArm(rig.FrontArm, frontHand);
        BlendArm(rig.BackArm, backHand);

        Vector2 toHands = (frontHand + backHand) / 2f - rig.Head.CrownPosition;
        Vector2 ahead = Vector2.right * rig.Facing.Sign;
        Vector2 look = toHands.sqrMagnitude > 0f ? toHands.normalized : ahead;
        rig.Head.LookToward(Vector2.Lerp(ahead, look, HeadTowardHands * Weight));
    }

    private void BlendArm(ArmLimb arm, Vector2 pushTarget) {
        Vector2 underneath = arm.RequestedTarget ?? arm.HandPosition;
        arm.ReachToward(Vector2.Lerp(underneath, pushTarget, Weight));
    }

    // On the face at the set height from the shoulder, kept inside the face's edges and within the arm's reach.
    private Vector2 HandOnFace(ArmLimb arm, float drop) {
        Vector2 shoulder = arm.ShoulderPosition;
        float low = face.bottom + HandEdgeMargin;
        float high = Mathf.Max(low, face.top - HandEdgeMargin);
        float y = Mathf.Clamp(shoulder.y + settings.handHeight + drop, low, high);
        Vector2 onFace = new Vector2(face.faceX - direction * HandFaceGap, y);
        return shoulder + Vector2.ClampMagnitude(onFace - shoulder, arm.Reach * MaxArmExtension);
    }
}
