using System.Collections.Generic;
using UnityEngine;

// What the grounded locomotion is doing, read once per refresh for the debug tools: input, speed and pace, the gait
// clock, the posture, both legs, contact timing and recent events. Read-only; refreshing it changes nothing.
public sealed class LocomotionTelemetry {
    public enum Mode { Idle, Walk, Run, Transition }

    public readonly struct Leg {
        public Leg((bool isGrounded, float stepProgress, float secondsSincePlant, Vector2 footTarget, Vector2 solver) leg) {
            IsGrounded = leg.isGrounded;
            StepProgress = leg.stepProgress;
            SecondsSincePlant = leg.secondsSincePlant;
            FootTarget = leg.footTarget;
            Solver = leg.solver;
        }

        public bool IsGrounded { get; }
        public float StepProgress { get; }
        public float SecondsSincePlant { get; }
        // Where the foot is headed (its planned foothold while swinging) and where its IK target is now.
        public Vector2 FootTarget { get; }
        public Vector2 Solver { get; }
    }

    // A walk/run blend this close to either end counts as that gait.
    private const float PureGaitMargin = 0.05f;

    private readonly StrideController stride;
    private readonly (LocomotionPosture posture, TorsoMotion torso, HeadLook head, Transform postureRoot) upperBody;

    public LocomotionTelemetry(StrideController stride, (LocomotionPosture posture, TorsoMotion torso, HeadLook head, Transform postureRoot) upperBody) {
        this.stride = stride;
        this.upperBody = upperBody;
    }

    public Mode Classification { get; private set; }
    public GaitPreview Preview { get; private set; }
    public float MoveAxis { get; private set; }
    public bool RunHeld { get; private set; }
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
    public Vector2 BodyTarget { get; private set; }
    public float StrideAnchorX { get; private set; }
    public float StanceHeight { get; private set; }
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
        ReadGait();
        ReadUpperBody();
        ReadBodyAndLegs(frame.time);
        ReadEvents(frame.time);
    }

    private void ReadInput(PlayerCommands commands) {
        MoveAxis = commands.MoveAxis;
        RunHeld = commands.RunHeld;
        Preview = stride.Preview;
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
        BodyPosition = stride.transform.position;
        BodyTarget = stride.BodyTarget;
        StrideAnchorX = stride.StrideAnchorX;
        StanceHeight = stride.StanceHeight;
        Front = ReadLeg((stride.FrontLeg, time - diagnostics.LastFrontPlant));
        Back = ReadLeg((stride.BackLeg, time - diagnostics.LastBackPlant));
        LastPlantWasFront = diagnostics.LastPlantWasFront;
        FrontToBackContact = diagnostics.FrontToBackContact;
        BackToFrontContact = diagnostics.BackToFrontContact;
    }

    private static Leg ReadLeg((LegStepper leg, float secondsSincePlant) read) {
        return new Leg((read.leg.IsGrounded, read.leg.StepProgress, read.secondsSincePlant, read.leg.FootTarget, read.leg.transform.position));
    }

    private void ReadEvents(float time) {
        GaitDiagnostics diagnostics = stride.Diagnostics;
        BalanceSteps = (diagnostics.BalanceSteps, time - diagnostics.LastBalanceStep);
        PhaseResyncs = (diagnostics.PhaseResyncs, time - diagnostics.LastPhaseResync);
        FootholdFailures = (diagnostics.FootholdFailures, time - diagnostics.LastFootholdFailure);
    }
}
