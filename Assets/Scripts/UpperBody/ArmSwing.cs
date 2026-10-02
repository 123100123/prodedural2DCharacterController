using System;
using UnityEngine;

// Swings the arms opposite to the legs (an arm swings forward as the other side's leg steps). Swing size and elbow
// bend come from the gait, so the arms hang loosely when walking and pump with bent elbows when running.
[Serializable]
public class ArmSwing {
    [SerializeField] private SpringSettings spring = new SpringSettings(3f, 0.5f, 0f);
    [Tooltip("How much a fully bent arm shortens toward the shoulder.")]
    [Range(0f, 0.8f)] [SerializeField] private float bentArmShortening = 0.45f;
    [Tooltip("How far forward (degrees) a fully bent arm is carried.")]
    [SerializeField] private float bentArmForwardAngle = 35f;

    private CharacterRig rig;
    private (Vector2 front, Vector2 back) restHandsInChest;
    private SecondOrderDynamics swing;
    private SecondOrderDynamics bend;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restHandsInChest = (rig.Chest.InverseTransformPoint(rig.FrontArm.HandPosition), rig.Chest.InverseTransformPoint(rig.BackArm.HandPosition));
        swing = new SecondOrderDynamics(0f);
        bend = new SecondOrderDynamics(0f);
    }

    public void Tick(float deltaTime) {
        GaitProfile gait = rig.Stride.CurrentGait;
        float swingAngle = swing.Update((TargetSwing(gait), deltaTime), spring);
        float elbowBend = bend.Update((gait.armBend * rig.Stride.SpeedShare, deltaTime), spring);
        rig.FrontArm.ReachToward(SwungHand((rig.FrontArm, restHandsInChest.front), (-swingAngle, elbowBend)));
        rig.BackArm.ReachToward(SwungHand((rig.BackArm, restHandsInChest.back), (swingAngle, elbowBend)));
    }

    // Positive while the front leg steps (the back arm swings forward), negative while the back leg steps.
    private float TargetSwing(GaitProfile gait) {
        float legPhase = Mathf.Sin(Mathf.PI * rig.Stride.FrontLeg.StepProgress) - Mathf.Sin(Mathf.PI * rig.Stride.BackLeg.StepProgress);
        return legPhase * rig.Stride.SpeedShare * gait.armSwing;
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
