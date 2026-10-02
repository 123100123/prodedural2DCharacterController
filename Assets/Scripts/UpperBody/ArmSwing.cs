using System;
using UnityEngine;

// Poses the arms when they are not aiming. Walking and running, they swing opposite to the legs on the gait cycle, the
// hand dragging a little behind the shoulder and the elbow flexing on the forward swing; the size of the swing and the
// elbow bend come from the gait. Winding up a jump they swing back; in the air they rise for balance.
[Serializable]
public class ArmSwing {
    [SerializeField] private SpringSettings spring = new SpringSettings(3.5f, 0.55f, 0f);
    [Tooltip("Cycle share the arm swing lags the legs: the hand drags behind the shoulder.")]
    [Range(0f, 0.25f)] [SerializeField] private float swingLag = 0.06f;
    [Tooltip("Extra elbow bend while the arm is swinging forward.")]
    [Range(0f, 1f)] [SerializeField] private float forwardElbowFlex = 0.35f;
    [Tooltip("How much a fully bent arm shortens toward the shoulder.")]
    [Range(0f, 0.8f)] [SerializeField] private float bentArmShortening = 0.45f;
    [Tooltip("How far forward (degrees) a fully bent arm is carried.")]
    [SerializeField] private float bentArmForwardAngle = 35f;

    [Header("Jump")]
    [Tooltip("Arms swing back this far (degrees) while crouching to jump.")]
    [SerializeField] private float windupSwing = 40f;
    [Tooltip("Arm raise (degrees forward) while rising and while falling.")]
    [SerializeField] private Vector2 airborneRaise = new Vector2(80f, 45f);
    [SerializeField] private float airborneElbowBend = 0.3f;

    private CharacterRig rig;
    private (Vector2 front, Vector2 back) restHandsInChest;
    private (SecondOrderDynamics front, SecondOrderDynamics back) swing;
    private (SecondOrderDynamics front, SecondOrderDynamics back) bend;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restHandsInChest = (rig.Chest.InverseTransformPoint(rig.FrontArm.HandPosition), rig.Chest.InverseTransformPoint(rig.BackArm.HandPosition));
        swing = (new SecondOrderDynamics(0f), new SecondOrderDynamics(0f));
        bend = (new SecondOrderDynamics(0f), new SecondOrderDynamics(0f));
    }

    public void Tick(float deltaTime) {
        ((float front, float back) angles, (float front, float back) elbows) pose = rig.Stride.IsDrivingGait ? GroundedPose() : AirbornePose();
        float frontAngle = swing.front.Update((pose.angles.front, deltaTime), spring);
        float backAngle = swing.back.Update((pose.angles.back, deltaTime), spring);
        float frontBend = bend.front.Update((pose.elbows.front, deltaTime), spring);
        float backBend = bend.back.Update((pose.elbows.back, deltaTime), spring);

        rig.FrontArm.ReachToward(SwungHand((rig.FrontArm, restHandsInChest.front), (frontAngle, frontBend)));
        rig.BackArm.ReachToward(SwungHand((rig.BackArm, restHandsInChest.back), (backAngle, backBend)));
    }

    // The back arm is opposite the front leg: it reaches forward as the front leg lands; the front arm mirrors it.
    // A held crouch (a jump wind-up) swings both arms back.
    private ((float front, float back) angles, (float front, float back) elbows) GroundedPose() {
        StrideController stride = rig.Stride;
        GaitProfile gait = stride.CurrentGait;
        float contact = GaitWaves.FrontLegContact(gait.dutyFactor);
        float wave = GaitWaves.OppositeArmSwing((stride.GaitPhase, contact, swingLag)) * gait.armSwing * stride.Pace.Moving;
        float windup = stride.HeldCrouch > 0f ? -windupSwing : 0f;

        (float front, float back) angles = (windup - wave, windup + wave);
        float baseBend = gait.armBend * stride.Pace.Moving;
        return (angles, (ElbowBendFor((angles.front, baseBend)), ElbowBendFor((angles.back, baseBend))));
    }

    // Both arms rise as the body rises and settle lower, ready for the landing, as it falls.
    private ((float front, float back) angles, (float front, float back) elbows) AirbornePose() {
        float rising = Mathf.InverseLerp(-6f, 6f, rig.Motor.Velocity.y);
        float raise = Mathf.Lerp(airborneRaise.y, airborneRaise.x, rising);
        return ((raise, raise), (airborneElbowBend, airborneElbowBend));
    }

    // Elbows flex as the arm swings forward and straighten as it swings back.
    private float ElbowBendFor((float swingAngle, float baseBend) arm) {
        float forwardShare = Mathf.Clamp01(arm.swingAngle / Mathf.Max(1f, rig.Stride.CurrentGait.armSwing));
        return Mathf.Clamp01(arm.baseBend + forwardElbowFlex * forwardShare);
    }

    // Turns the hanging rest hand about the shoulder (positive swings toward the facing direction) and draws it in
    // and forward as the elbow bends.
    private Vector2 SwungHand((ArmLimb arm, Vector2 restHandInChest) limb, (float forwardDegrees, float elbowBend) pose) {
        Vector2 shoulder = limb.arm.ShoulderPosition;
        Vector2 restHand = rig.Chest.TransformPoint(limb.restHandInChest);
        float worldDegrees = (pose.forwardDegrees + pose.elbowBend * bentArmForwardAngle) * rig.Facing.Sign;
        Vector2 shortenedArm = (restHand - shoulder) * (1f - pose.elbowBend * bentArmShortening);
        return shoulder + (Vector2)(Quaternion.Euler(0f, 0f, worldDegrees) * shortenedArm);
    }
}
