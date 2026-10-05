using UnityEngine;

// Ledge climb: reach the corner, hang, pull up while the feet scramble on the wall, mount a foot on top and stand.
public sealed class ClimbState : ICharacterState {
    private enum ClimbPhase { Reach, Hang, PullUp, Mount, Stand }

    private const float ScrambleFootWallGap = 0.15f;
    private const float TrailScrambleOffset = 0.15f;

    private readonly StateContext context;
    private readonly ClimbSettings settings;
    private readonly LedgeDetector detector;

    private Ledge ledge;
    private (Vector2 hang, Vector2 pulled, Vector2 mounted, Vector2 standing) keyframes;
    private ClimbPhase phase;
    private float phaseElapsed;
    private Vector2 phaseStart;
    private (LegStepper lead, LegStepper trail) legs;

    public ClimbState(StateContext context, ClimbSettings settings) {
        this.context = context;
        this.settings = settings;
        detector = new LedgeDetector(settings);
    }

    public bool AllowsUpperBodyActions => false;
    public bool AllowsCrouch => false;

    // Jump pressed on the ground next to a ledge that is too high to step onto.
    public bool TryBeginFromStance() {
        StrideController stride = context.Rig.Stride;
        (float minTop, float maxTop) window = LedgeGrabPolicy.StandingGrabWindow((stride.StanceHeight, stride.MaxStepUp), settings);
        return TryBeginAtLedgeAhead(window);
    }

    // Hands come near a ledge corner in the air.
    public bool TryBeginFromAir() {
        if (!TryFindLedgeInReach(context.Rig.Facing.Sign, out Ledge found)) {
            return false;
        }
        Grab(found);
        return true;
    }

    // A ledge corner within the hands' reach in the air on the given side (+1 right), found without grabbing it: a wall
    // slide asks toward its wall while the character faces away from it.
    public bool TryFindLedgeInReach(int direction, out Ledge found) {
        ArmLimb arm = context.Rig.FrontArm;
        (float minTop, float maxTop) window = LedgeGrabPolicy.AirGrabWindow((arm.ShoulderPosition.y, arm.Reach), settings);
        return detector.TryFindLedge((context.Rig.Body.position.x, direction, window), out found);
    }

    // Starts climbing a ledge found earlier this frame, turning toward it first.
    public void Grab(Ledge found) {
        context.Rig.Facing.FaceToward(found.ClimbDirection);
        ledge = found;
        context.Machine.ChangeTo(this);
    }

    private bool TryBeginAtLedgeAhead((float minTop, float maxTop) window) {
        float bodyX = context.Rig.Body.position.x;
        if (!detector.TryFindLedge((bodyX, context.Rig.Facing.Sign, window), out Ledge found)) {
            return false;
        }

        Grab(found);
        return true;
    }

    public void Enter() {
        Vector2 bodyStart = context.Rig.Body.position;
        keyframes = LedgeGrabPolicy.BodyKeyframes((ledge, bodyStart, context.Rig.Stride.BodyHeight), settings);
        legs = context.Rig.Stride.LegsOrderedByFacing(ledge.ClimbDirection);
        context.Rig.Motor.Velocity = Vector2.zero;
        StartPhase((ClimbPhase.Reach, bodyStart));
    }

    public void Tick(float deltaTime, PlayerCommands commands) {
        phaseElapsed += deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / PhaseDuration());

        MoveBodyAlongPhase(progress);
        PoseHands();
        PoseFeet(FrameRateEase.FractionForFrame(settings.limbPoseEase, deltaTime));
        context.Rig.Stride.PlaceStrideTargetUnderBody();

        if (progress >= 1f) {
            AdvancePhase();
        }
    }

    public void Exit() { }

    private void StartPhase((ClimbPhase phase, Vector2 bodyStart) start) {
        phase = start.phase;
        phaseStart = start.bodyStart;
        phaseElapsed = 0f;
    }

    private void AdvancePhase() {
        if (phase == ClimbPhase.Stand) {
            StandOnTop();
            return;
        }
        StartPhase((phase + 1, PhaseEndPosition()));
    }

    private float PhaseDuration() {
        switch (phase) {
            case ClimbPhase.Reach: return Mathf.Max(settings.minReachDuration, Vector2.Distance(phaseStart, keyframes.hang) / settings.reachSpeed);
            case ClimbPhase.Hang: return settings.hangDuration;
            case ClimbPhase.PullUp: return settings.pullDuration;
            case ClimbPhase.Mount: return settings.mountDuration;
            default: return settings.standDuration;
        }
    }

    private Vector2 PhaseEndPosition() {
        switch (phase) {
            case ClimbPhase.Reach:
            case ClimbPhase.Hang: return keyframes.hang;
            case ClimbPhase.PullUp: return keyframes.pulled;
            case ClimbPhase.Mount: return keyframes.mounted;
            default: return keyframes.standing;
        }
    }

    private void MoveBodyAlongPhase(float progress) {
        Transform body = context.Rig.Body;
        Vector2 position = Vector2.Lerp(phaseStart, PhaseEndPosition(), Mathf.SmoothStep(0f, 1f, progress));
        body.position = new Vector3(position.x, position.y, body.position.z);
    }

    // Both hands hold the corner until the character stands.
    private void PoseHands() {
        if (phase == ClimbPhase.Stand) {
            return;
        }
        context.Rig.FrontArm.ReachToward(CornerPoint(settings.frontGrip));
        context.Rig.BackArm.ReachToward(CornerPoint(settings.backGrip));
    }

    private void PoseFeet(float ease) {
        (Vector2 lead, Vector2 trail) targets = FootTargetsForPhase();
        legs.lead.SwingFreelyToward(targets.lead, ease);
        legs.trail.SwingFreelyToward(targets.trail, ease);
    }

    private (Vector2 lead, Vector2 trail) FootTargetsForPhase() {
        int toward = ledge.ClimbDirection;
        Vector2 body = context.Rig.Body.position;
        Vector2 leadOnTop = CornerPoint(new Vector2(settings.mountFootInset, 0f));

        switch (phase) {
            case ClimbPhase.Reach:
            case ClimbPhase.Hang:
                return (body + new Vector2(toward * settings.hangingLeadFoot.x, settings.hangingLeadFoot.y),
                        body + new Vector2(toward * settings.hangingTrailFoot.x, settings.hangingTrailFoot.y));
            case ClimbPhase.PullUp:
                return (ScramblePoint((body.y, 0f)), ScramblePoint((body.y, TrailScrambleOffset)));
            case ClimbPhase.Mount:
                return (leadOnTop, ScramblePoint((body.y, TrailScrambleOffset)));
            default:
                return (leadOnTop, new Vector2(keyframes.standing.x + toward * context.Rig.Stride.StanceHalfWidth, ledge.Corner.y));
        }
    }

    // Feet press against the wall face below the body.
    private Vector2 ScramblePoint((float bodyY, float extraDrop) foot) {
        float wallX = ledge.Corner.x - ledge.ClimbDirection * ScrambleFootWallGap;
        return new Vector2(wallX, foot.bodyY - settings.scrambleFootDrop - foot.extraDrop);
    }

    private Vector2 CornerPoint(Vector2 offsetTowardLedge) {
        return ledge.Corner + new Vector2(ledge.ClimbDirection * offsetTowardLedge.x, offsetTowardLedge.y);
    }

    // Falls back to a stance right past the edge when the planned one does not fit on the top.
    private void StandOnTop() {
        StrideController stride = context.Rig.Stride;
        float edgeStanceX = ledge.Corner.x + ledge.ClimbDirection * (stride.StanceHalfWidth + ScrambleFootWallGap);
        if (stride.TrySettleStance((keyframes.standing.x, ledge.ClimbDirection, ledge.Corner.y))
            || stride.TrySettleStance((edgeStanceX, ledge.ClimbDirection, ledge.Corner.y))) {
            context.States.Walk.Begin();
            return;
        }
        context.States.Airborne.Fall(Vector2.zero);
    }
}
