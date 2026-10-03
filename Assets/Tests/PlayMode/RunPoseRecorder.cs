using System.Collections.Generic;
using UnityEngine;

// Measures the running pose from the rig's bones once per frame, after the IK, in the terms the reference sprinter is
// described in: the torso's forward line, the waist's bend, each thigh's swing forward or back of straight down, how far
// each foot is behind its hip and how straight its leg is, and where the hands are relative to the body.
public sealed class RunPoseRecorder {
    public readonly struct Leg {
        public Leg((bool isGrounded, float thighForward, float footAheadOfHip, float extension, float footHeight) leg) {
            IsGrounded = leg.isGrounded;
            ThighForward = leg.thighForward;
            FootAheadOfHip = leg.footAheadOfHip;
            Extension = leg.extension;
            FootHeight = leg.footHeight;
        }

        public bool IsGrounded { get; }
        // Degrees the thigh points forward of straight down (negative: behind).
        public float ThighForward { get; }
        // Along the facing, in leg lengths (negative: behind the hip).
        public float FootAheadOfHip { get; }
        // Hip-to-foot distance as a share of the leg.
        public float Extension { get; }
        // Above the stance, in leg lengths.
        public float FootHeight { get; }
    }

    public readonly struct Pose {
        public Pose((float torsoLean, float waistBend) torso, (Leg front, Leg back) legs, (float front, float back) handsAhead) {
            TorsoLean = torso.torsoLean;
            WaistBend = torso.waistBend;
            Front = legs.front;
            Back = legs.back;
            HandsAhead = handsAhead;
        }

        // Degrees the line from the pelvis to the shoulders leans forward of vertical.
        public float TorsoLean { get; }
        // Degrees between the pelvis bone's and the chest bone's directions.
        public float WaistBend { get; }
        public Leg Front { get; }
        public Leg Back { get; }
        // How far each hand is ahead of the body pivot along the facing.
        public (float front, float back) HandsAhead { get; }
    }

    private const float ForearmLength = 0.9f;

    private readonly StrideController stride;
    private readonly Transform characterRoot;
    private readonly (Transform pelvis, Transform chest, Transform shoulder) torso;
    private readonly (Transform front, Transform back) knees;
    private readonly (Transform front, Transform back) forearms;
    private readonly List<Pose> poses = new List<Pose>();

    public RunPoseRecorder((CharacterDriver character, StrideController stride) rig) {
        stride = rig.stride;
        characterRoot = rig.character.transform;
        torso = (Find("bone_abs"), Find("bone_chest"), Find("bone_shoulderF"));
        knees = (Find("bone_calfF"), Find("bone_calfB"));
        forearms = (Find("bone_forearmF"), Find("bone_forearmB"));
    }

    public IReadOnlyList<Pose> Poses => poses;
    public void Clear() => poses.Clear();

    public void Record() {
        Vector2 pelvis = torso.pelvis.position;
        Vector2 shoulders = torso.shoulder.position;
        float torsoLean = Mathf.Atan2((shoulders.x - pelvis.x) * Facing, shoulders.y - pelvis.y) * Mathf.Rad2Deg;
        float waistBend = Mathf.DeltaAngle(BoneAngle(torso.pelvis), BoneAngle(torso.chest));
        Leg front = Measure((stride.FrontLeg, knees.front));
        Leg back = Measure((stride.BackLeg, knees.back));
        poses.Add(new Pose((torsoLean, waistBend), (front, back), (HandAhead(forearms.front), HandAhead(forearms.back))));
    }

    // The thigh runs from the hip joint to the knee joint (the shin bone's pivot).
    private Leg Measure((LegStepper leg, Transform knee) limb) {
        float length = limb.leg.LegLength;
        Vector2 hip = limb.leg.HipPosition;
        Vector2 foot = limb.leg.transform.position;
        Vector2 thighDirection = (Vector2)limb.knee.position - hip;
        float thighForward = Mathf.Atan2(thighDirection.x * Facing, -thighDirection.y) * Mathf.Rad2Deg;
        return new Leg((limb.leg.IsGrounded, thighForward, (foot.x - hip.x) * Facing / length, Vector2.Distance(hip, foot) / length, (foot.y - stride.StanceHeight) / length));
    }

    private float HandAhead(Transform forearm) {
        Vector2 hand = forearm.TransformPoint(new Vector3(ForearmLength, 0f, 0f));
        return (hand.x - stride.transform.position.x) * Facing;
    }

    // A bone's visible direction, measured toward the facing side.
    private float BoneAngle(Transform bone) {
        Vector3 direction = bone.TransformVector(Vector3.right);
        return Mathf.Atan2(direction.y, direction.x * Facing) * Mathf.Rad2Deg;
    }

    private int Facing => characterRoot.localScale.x < 0f ? -1 : 1;

    private Transform Find(string boneName) {
        foreach (Transform bone in characterRoot.GetComponentsInChildren<Transform>()) {
            if (bone.name == boneName) {
                return bone;
            }
        }
        throw new KeyNotFoundException(boneName);
    }
}
