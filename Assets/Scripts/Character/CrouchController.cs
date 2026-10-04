using System;
using UnityEngine;

// Crouching as a modifier on every state that can crouch rather than a state of its own: the crouch share rises and
// falls through a spring while the feet stay planted, and the share lowers the pelvis, selects the crouch gait, shrinks
// the collision hull and shapes the stance, torso and arms (each reads the share where it needs it). Standing back up is
// held while a ceiling is too low to stand under.
[Serializable]
public class CrouchController {
    // Below this share the character counts as standing: the ceiling is not queried and cannot hold a crouch.
    private const float CrouchedShare = 0.01f;

    [SerializeField] private CrouchSettings settings = CrouchSettings.Default;

    private CharacterRig rig;
    private SecondOrderDynamics share;

    public CrouchSettings Settings { get => settings; set => settings = value; }
    // How crouched the character is, 0 standing to 1 crouched.
    public float Share { get; private set; }
    // Crouching is wanted (the input, or a ceiling that does not allow standing).
    public bool IsCrouching { get; private set; }
    // A ceiling is too low to stand under right now (only measured while crouched).
    public bool IsHeadroomBlocked { get; private set; }
    // The collision hull as it is now, for drawing.
    public Rect Hull => rig.Motor.CurrentHull;

    public void Initialize(CharacterRig characterRig) {
        rig = characterRig;
        share = new SecondOrderDynamics(0f);
    }

    public void Tick(float deltaTime, (bool requested, bool allowed) input) {
        IsHeadroomBlocked = Share > CrouchedShare && !HasRoomToStand();
        IsCrouching = CrouchIntent.Resolve((input.requested, Share > CrouchedShare, IsHeadroomBlocked), input.allowed);

        Share = Mathf.Clamp01(share.Update((IsCrouching ? 1f : 0f, deltaTime), settings.spring));
        rig.Motor.CrouchHullDrop = settings.depth * Share;
        rig.Stride.ApplyCrouch((Share, settings));
    }

    // The hull standing on the feet, where the pelvis would be at full standing height.
    private bool HasRoomToStand() {
        StrideController stride = rig.Stride;
        Vector2 standingPivot = new Vector2(rig.Body.position.x, stride.StanceHeight + stride.BodyHeight);
        return rig.Motor.HasHeadroom(standingPivot);
    }
}
