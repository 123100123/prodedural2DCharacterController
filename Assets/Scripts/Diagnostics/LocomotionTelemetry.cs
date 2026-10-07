using System.Collections.Generic;
using UnityEngine;

// What the grounded locomotion is doing, read once per refresh for the debug tools: input, speed and pace, the gait
// clock, the posture, the support and pelvis, both legs, contact timing and recent events. Read-only; refreshing it changes nothing.
public sealed class LocomotionTelemetry {
    public enum Mode { Idle, Walk, Run, Transition }

    public readonly struct Leg {
        public Leg((bool isGrounded, float stepProgress, float secondsSincePlant) state, (Vector2 footTarget, Vector2 solver, Vector2 hip, float length) geometry) {
            IsGrounded = state.isGrounded;
            StepProgress = state.stepProgress;
            SecondsSincePlant = state.secondsSincePlant;
            FootTarget = geometry.footTarget;
            Solver = geometry.solver;
            Hip = geometry.hip;
            Length = geometry.length;
        }

        public bool IsGrounded { get; }
        public float StepProgress { get; }
        public float SecondsSincePlant { get; }
        // Where the foot is headed (its planned foothold while swinging) and where its IK target is now.
        public Vector2 FootTarget { get; }
        public Vector2 Solver { get; }
        public Vector2 Hip { get; }
        // Measured from the rig's bones.
        public float Length { get; }
        public float HipToFoot => Vector2.Distance(Hip, FootTarget);
        // Hip-to-foot distance as a share of the leg's length (1: fully straight).
        public float Extension => Length > 0f ? HipToFoot / Length : 0f;
    }

    // A walk/run blend this close to either end counts as that gait.
    private const float PureGaitMargin = 0.05f;

    private readonly StrideController stride;
    private readonly (LocomotionPosture posture, TorsoMotion torso, HeadLook head, Transform postureRoot) upperBody;
    private readonly CrouchController crouch;
    private readonly AirborneState airborne;
    private readonly (CharacterStateMachine machine, WalkState walk, RollState roll) fullBody;

    public LocomotionTelemetry(StrideController stride, (LocomotionPosture posture, TorsoMotion torso, HeadLook head, Transform postureRoot) upperBody, (CrouchController crouch, AirborneState airborne) states, (CharacterStateMachine machine, WalkState walk, RollState roll) fullBody = default) {
        this.stride = stride;
        this.upperBody = upperBody;
        crouch = states.crouch;
        airborne = states.airborne;
        this.fullBody = fullBody;
    }

    public Mode Classification { get; private set; }
    // Crouching: how crouched the character is, whether it is held there, how far the pelvis is sunk and by how much
    // a full crouch would sink it, and whether a low ceiling holds it down.
    public float CrouchShare { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsHeadroomBlocked { get; private set; }
    public float CrouchDrop { get; private set; }
    public float CrouchDepth { get; private set; }
    public Rect Hull { get; private set; }
    // The scene's lighting rig, if any, and the brightness of its player light and muzzle flash right now.
    public ILightingTarget Lighting { get; set; }
    public float PlayerLightIntensity { get; private set; }
    public float MuzzleLightIntensity { get; private set; }
    public float SunLightIntensity { get; private set; }
    public int LampCount { get; private set; }
    public int ShadowCasterCount { get; private set; }
    // How much lower a hard landing is holding the pelvis, while it recovers.
    public float LandingCompression { get; private set; }
    // The flight, as the airborne state last reported it (all zero on the ground).
    public AirSnapshot Air { get; private set; }
    // The gait counts as a crouch once more than half crouched.
    public bool IsCrouched => CrouchShare > 0.5f;
    public GaitPreview Preview { get; private set; }
    public float MoveAxis { get; private set; }
    public bool RunHeld { get; private set; }
    // Run intent: the run input held with a direction; whether the current state could run now (and why not); and
    // whether the intent is waiting on the state (pending: wanted, but the state cannot run yet).
    public bool RunIntent { get; private set; }
    public RunReadiness Readiness { get; private set; }
    public bool RunIntentPending => RunIntent && !Readiness.CanRun;
    // The full-body state and the last switch: its cached names, how long ago, and how many switches so far.
    public string StateName { get; private set; } = "-";
    public string PreviousStateName { get; private set; } = "-";
    public float SecondsSinceStateChange { get; private set; }
    public int StateChanges { get; private set; }
    // The ground speed the walk was handed when it last began: the momentum carried into the walk or run.
    public float CarriedSpeed { get; private set; }
    // The current roll's momentum, or the last roll's (default before any roll).
    public RollReadout Roll { get; private set; }
    public bool IsDrivingGait { get; private set; }

    public float GroundSpeed { get; private set; }
    public float IntendedSpeed { get; private set; }
    public LocomotionPace Pace { get; private set; }
    public GaitProfile Gait { get; private set; }
    public float GaitPhase { get; private set; }
    public float CyclePeriod { get; private set; }
    public float StepsPerSecond => CyclePeriod > 0f ? 2f / CyclePeriod : 0f;
    public float SwingShare { get; private set; }
    public float CatchUp { get; private set; }

    public float PostureTargetLean { get; private set; }
    public float PostureLean { get; private set; }
    public float AccelerationLean { get; private set; }
    public float ChestPitch { get; private set; }
    public float HeadLevelWeight { get; private set; }
    public Vector2 PostureUp { get; private set; }

    public Vector2 BodyPosition { get; private set; }
    // Where the pelvis was at the previous refresh, for drawing its path.
    public Vector2 PreviousBodyPosition { get; private set; }
    public Vector2 BodyTarget { get; private set; }
    public float StrideAnchorX { get; private set; }
    public float StanceHeight { get; private set; }
    public SupportStance Support { get; private set; }
    public bool BothFeetPlanted { get; private set; }
    public (float support, float surge) PelvisOffsets { get; private set; }
    public (float baseline, float gait, float ceiling) BodyHeights { get; private set; }
    public Leg Front { get; private set; }
    public Leg Back { get; private set; }
    public bool LastPlantWasFront { get; private set; }
    public float FrontToBackContact { get; private set; }
    public float BackToFrontContact { get; private set; }
    public IReadOnlyList<Vector2> FootholdCandidates => stride.FootholdCandidates;

    public (int count, float secondsAgo) BalanceSteps { get; private set; }
    public (int count, float secondsAgo) PhaseResyncs { get; private set; }
    public (int count, float secondsAgo) FootholdFailures { get; private set; }

    public static Mode Classify((bool isMoving, float running) gait) {
        if (!gait.isMoving) {
            return Mode.Idle;
        }
        if (gait.running <= PureGaitMargin) {
            return Mode.Walk;
        }
        return gait.running >= 1f - PureGaitMargin ? Mode.Run : Mode.Transition;
    }

    public void Refresh((PlayerCommands commands, float time) frame) {
        ReadInput(frame.commands);
        ReadRunIntent((frame.commands, frame.time));
        ReadGait();
        ReadCrouch();
        Air = airborne.Snapshot;
        ReadUpperBody();
        ReadBodyAndLegs(frame.time);
        ReadEvents(frame.time);
    }

    private void ReadInput(PlayerCommands commands) {
        MoveAxis = commands.MoveAxis;
        RunHeld = commands.RunHeld;
        Preview = stride.Preview;
    }

    // Allocation-free: the readiness is an enum and the state names are constants.
    private void ReadRunIntent((PlayerCommands commands, float time) frame) {
        RunIntent = frame.commands.RunIntent;
        CharacterStateMachine machine = fullBody.machine;
        ICharacterState current = machine?.Current;
        if (current == null) {
            Readiness = RunReadiness.Ready;
            return;
        }
        Readiness = current.RunReadinessFor(frame.commands);
        StateName = current.Name;
        PreviousStateName = machine.Previous?.Name ?? "-";
        SecondsSinceStateChange = frame.time - machine.LastChangeTime;
        StateChanges = machine.ChangeCount;
        CarriedSpeed = fullBody.walk?.CarriedSpeed ?? 0f;
        Roll = fullBody.roll?.Readout ?? default;
    }

    private void ReadGait() {
        IsDrivingGait = stride.IsDrivingGait;
        GroundSpeed = stride.GroundSpeed;
        IntendedSpeed = stride.IntendedSpeed;
        Pace = stride.Pace;
        Gait = stride.CurrentGait;
        GaitPhase = stride.GaitPhase;
        CyclePeriod = stride.CyclePeriod;
        SwingShare = stride.SwingShare;
        CatchUp = stride.Diagnostics.CatchUp;
        Classification = Classify((stride.IsMoving, Pace.Running));
    }

    private void ReadCrouch() {
        CrouchShare = stride.CrouchShare;
        IsCrouching = crouch.IsCrouching;
        IsHeadroomBlocked = crouch.IsHeadroomBlocked;
        CrouchDrop = stride.CrouchDrop;
        CrouchDepth = crouch.Settings.depth;
        Hull = crouch.Hull;
        PlayerLightIntensity = Lighting?.PlayerIntensity ?? 0f;
        MuzzleLightIntensity = Lighting?.MuzzleIntensity ?? 0f;
        SunLightIntensity = Lighting?.SunIntensity ?? 0f;
        LampCount = Lighting?.LampCount ?? 0;
        ShadowCasterCount = Lighting?.ShadowCasterCount ?? 0;
        LandingCompression = stride.LandingCompression;
    }

    private void ReadUpperBody() {
        PostureTargetLean = upperBody.posture.TargetLean;
        PostureLean = upperBody.posture.CurrentLean;
        AccelerationLean = upperBody.posture.AccelerationLean;
        ChestPitch = upperBody.torso.CurrentPitch;
        HeadLevelWeight = upperBody.head.LevelWeight;
        PostureUp = upperBody.postureRoot != null ? (Vector2)upperBody.postureRoot.up : Vector2.up;
    }

    private void ReadBodyAndLegs(float time) {
        GaitDiagnostics diagnostics = stride.Diagnostics;
        PreviousBodyPosition = BodyPosition;
        BodyPosition = stride.transform.position;
        BodyTarget = stride.BodyTarget;
        StrideAnchorX = stride.StrideAnchorX;
        StanceHeight = stride.StanceHeight;
        Support = stride.Support;
        BothFeetPlanted = stride.FrontLeg.IsGrounded && stride.BackLeg.IsGrounded;
        PelvisOffsets = stride.PelvisOffsets;
        BodyHeights = stride.BodyHeights;
        Front = ReadLeg((stride.FrontLeg, time - diagnostics.LastFrontPlant));
        Back = ReadLeg((stride.BackLeg, time - diagnostics.LastBackPlant));
        LastPlantWasFront = diagnostics.LastPlantWasFront;
        FrontToBackContact = diagnostics.FrontToBackContact;
        BackToFrontContact = diagnostics.BackToFrontContact;
    }

    private static Leg ReadLeg((LegStepper leg, float secondsSincePlant) read) {
        LegStepper leg = read.leg;
        return new Leg((leg.IsGrounded, leg.StepProgress, read.secondsSincePlant), (leg.FootTarget, leg.transform.position, leg.HipPosition, leg.LegLength));
    }

    private void ReadEvents(float time) {
        GaitDiagnostics diagnostics = stride.Diagnostics;
        BalanceSteps = (diagnostics.BalanceSteps, time - diagnostics.LastBalanceStep);
        PhaseResyncs = (diagnostics.PhaseResyncs, time - diagnostics.LastPhaseResync);
        FootholdFailures = (diagnostics.FootholdFailures, time - diagnostics.LastFootholdFailure);
    }
}
