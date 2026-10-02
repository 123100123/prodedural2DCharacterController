using System;
using UnityEngine;

// Swings the arms opposite to the legs while walking (an arm swings forward as the other side's leg steps),
// scaled by walking speed and smoothed by a spring so the arms carry a little momentum.
[Serializable]
public class ArmSwing {
    [Tooltip("Largest swing angle in degrees at full walking speed.")]
    [SerializeField] private float maxSwing = 28f;
    [Tooltip("Walking speed at which the swing reaches its full angle.")]
    [SerializeField] private float fullSwingSpeed = 8f;
    [SerializeField] private SpringSettings spring = new SpringSettings(3f, 0.5f, 0f);

    private CharacterRig rig;
    private (Vector2 front, Vector2 back) restHandsInChest;
    private SecondOrderDynamics swing;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        restHandsInChest = (rig.Chest.InverseTransformPoint(rig.FrontArm.HandPosition), rig.Chest.InverseTransformPoint(rig.BackArm.HandPosition));
        swing = new SecondOrderDynamics(0f);
    }

    public void Tick(float deltaTime) {
        float swingAngle = swing.Update((TargetSwing(), deltaTime), spring);
        rig.FrontArm.ReachToward(RotatedRestHand((rig.FrontArm, restHandsInChest.front), -swingAngle));
        rig.BackArm.ReachToward(RotatedRestHand((rig.BackArm, restHandsInChest.back), swingAngle));
    }

    // Positive while the front leg steps (the back arm swings forward), negative while the back leg steps.
    private float TargetSwing() {
        float legPhase = Mathf.Sin(Mathf.PI * rig.Stride.FrontLeg.StepProgress) - Mathf.Sin(Mathf.PI * rig.Stride.BackLeg.StepProgress);
        float speedShare = Mathf.Clamp01(Mathf.Abs(rig.Stride.BodyVelocity.x) / fullSwingSpeed);
        return legPhase * speedShare * maxSwing;
    }

    // Turns the hanging rest hand about the shoulder; positive angles swing it toward the facing direction.
    private Vector2 RotatedRestHand((ArmLimb arm, Vector2 restHandInChest) limb, float forwardDegrees) {
        Vector2 shoulder = limb.arm.ShoulderPosition;
        Vector2 restHand = rig.Chest.TransformPoint(limb.restHandInChest);
        float worldDegrees = forwardDegrees * rig.Facing.Sign;
        return shoulder + (Vector2)(Quaternion.Euler(0f, 0f, worldDegrees) * (restHand - shoulder));
    }
}
