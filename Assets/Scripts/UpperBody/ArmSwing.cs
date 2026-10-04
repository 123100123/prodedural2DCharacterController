using System;
using UnityEngine;

// Poses the arms when they are not aiming. Walking and running, they swing opposite to the legs on the gait cycle, the
// hand dragging a little behind the shoulder and the elbow flexing on the forward swing; the size of the swing and the
// elbow bend come from the gait. They swing about plumb, like pendulums, so leaning the torso into a run does not carry
// the swing back with it. Winding up a jump they swing back; in the air they take the arms of the flight pose.
[Serializable]
public class ArmSwing {
    [SerializeField] private ArmSwingSettings settings = ArmSwingSettings.Default;

    [Header("Jump")]
    [Tooltip("Arms swing back this far (degrees) while crouching to jump.")]
    [SerializeField] private float windupSwing = 40f;

    private CharacterRig rig;

    public ArmSwingSettings Settings { get => settings; set => settings = value; }
    private (Vector2 front, Vector2 back) restHandsInChest;
    // Degrees each rest arm hangs forward of plumb with the torso upright.
    private (float front, float back) uprightHang;
    private (SecondOrderDynamics front, SecondOrderDynamics back) swing;
    private (SecondOrderDynamics front, SecondOrderDynamics back) bend;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restHandsInChest = (rig.Chest.InverseTransformPoint(rig.FrontArm.HandPosition), rig.Chest.InverseTransformPoint(rig.BackArm.HandPosition));
        uprightHang = (ForwardOfPlumb(rig.FrontArm.HandPosition - rig.FrontArm.ShoulderPosition), ForwardOfPlumb(rig.BackArm.HandPosition - rig.BackArm.ShoulderPosition));
        swing = (new SecondOrderDynamics(0f), new SecondOrderDynamics(0f));
        bend = (new SecondOrderDynamics(0f), new SecondOrderDynamics(0f));
    }

    public void Tick(float deltaTime) {
        bool isFlying = !rig.Stride.IsDrivingGait && rig.AirPose.IsActive;
        ((float front, float back) angles, (float front, float back) elbows) pose = isFlying ? AirbornePose() : GroundedPose();
        float frontAngle = swing.front.Update((pose.angles.front, deltaTime), settings.spring);
        float backAngle = swing.back.Update((pose.angles.back, deltaTime), settings.spring);
        float frontBend = bend.front.Update((pose.elbows.front, deltaTime), settings.spring);
        float backBend = bend.back.Update((pose.elbows.back, deltaTime), settings.spring);

        rig.FrontArm.ReachToward(SwungHand((rig.FrontArm, restHandsInChest.front, uprightHang.front), (frontAngle, frontBend)));
        rig.BackArm.ReachToward(SwungHand((rig.BackArm, restHandsInChest.back, uprightHang.back), (backAngle, backBend)));
    }

    // The back arm is opposite the front leg: it reaches forward as the front leg lands; the front arm mirrors it.
    // A jump wind-up swings both arms back.
    private ((float front, float back) angles, (float front, float back) elbows) GroundedPose() {
        StrideController stride = rig.Stride;
        GaitProfile gait = stride.CurrentGait;
        float contact = GaitWaves.FrontLegContact(gait.dutyFactor);
        float wave = GaitWaves.OppositeArmSwing((stride.GaitPhase, contact, settings.swingLag)) * gait.armSwing * stride.Pace.Moving;
        float windup = stride.WindupCrouch > 0f ? -windupSwing : 0f;

        (float front, float back) angles = (windup - wave, windup + wave);
        float baseBend = Mathf.Clamp01(gait.armBend * stride.Pace.Moving + stride.CrouchSettings.idleArmBend * stride.CrouchShare);
        return (angles, (ElbowBendFor((angles.front, baseBend)), ElbowBendFor((angles.back, baseBend))));
    }

    // The arms of the flight pose: thrown up with the push-off, loosening at the top, settling for the landing.
    private ((float front, float back) angles, (float front, float back) elbows) AirbornePose() {
        AirPose pose = rig.AirPose.Pose;
        return ((pose.armFront, pose.armBack), (pose.elbowBend, pose.elbowBend));
    }

    // Elbows flex as the arm swings forward and straighten as it swings back.
    private float ElbowBendFor((float swingAngle, float baseBend) arm) {
        float forwardShare = Mathf.Clamp01(arm.swingAngle / Mathf.Max(1f, rig.Stride.CurrentGait.armSwing));
        return Mathf.Clamp01(arm.baseBend + settings.forwardElbowFlex * forwardShare);
    }

    // Turns the hanging rest hand about the shoulder (positive swings toward the facing direction) and draws it in
    // and forward as the elbow bends. The torso's tilt is turned out of the rest arm first, so the swing centres on
    // plumb whatever the lean.
    private Vector2 SwungHand((ArmLimb arm, Vector2 restHandInChest, float uprightHang) limb, (float forwardDegrees, float elbowBend) pose) {
        Vector2 shoulder = limb.arm.ShoulderPosition;
        Vector2 restArm = (Vector2)rig.Chest.TransformPoint(limb.restHandInChest) - shoulder;
        float torsoTilt = ForwardOfPlumb(restArm) - limb.uprightHang;
        float forwardDegrees = pose.forwardDegrees + pose.elbowBend * settings.bentArmForwardAngle - torsoTilt;
        Vector2 shortenedArm = restArm * (1f - pose.elbowBend * settings.bentArmShortening);
        return shoulder + (Vector2)(Quaternion.Euler(0f, 0f, forwardDegrees * rig.Facing.Sign) * shortenedArm);
    }

    // Degrees an arm points forward (toward the facing) of straight down.
    private float ForwardOfPlumb(Vector2 arm) => Mathf.Atan2(arm.x * rig.Facing.Sign, -arm.y) * Mathf.Rad2Deg;
}
