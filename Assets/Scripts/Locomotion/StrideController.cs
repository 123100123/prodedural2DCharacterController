using System.Collections.Generic;
using UnityEngine;

// Grounded gait: the body moves at the ground speed and the feet follow it. One gait clock (StepScheduler) times both
// legs half a cycle apart and also drives their swings, so steps alternate evenly at any cadence; each foot lands where
// the hip will be at touchdown plus half a stance ahead. Walk and run are blended profiles, so idle, walking and running
// flow into each other. The body rides the feet as a mass on springs and bobs and surges in step with the cycle.
public class StrideController : MonoBehaviour {
    private const float GroundContactTolerance = 0.15f;
    // Walls are looked for at least this far ahead, and as far as a full-speed running step lands.
    private const float MinWallLookAhead = 2.5f;
    // Long frames (hitches) are split into gait steps no longer than this, so the feet keep up with the body.
    private const float MaxGaitStep = 1f / 30f;
    // Feet settle into a stance once the body has come to rest above them (horizontal speed below this), so the stance
    // spots no longer move and one settling step per foot is enough.
    private const float SettledBodySpeed = 0.2f;
    // A gait swing may outlast its forecast by this share before its time ceiling finishes it (the clock stalled).
    private const float SwingCeilingSlack = 1.15f;
    // A landing foot with no ground at its spot looks for it this many even steps in toward the body pivot.
    private const int LandingEdgeSearchSteps = 4;
    // The ground under the body is sampled this far either side of the pelvis, so the slope it reads changes smoothly as
    // the body passes from flat ground onto a ramp.
    private const float TerrainSampleSpread = 0.35f;
    // Seconds the gait takes to blend from flat-ground behaviour to slope behaviour and back.
    private const float SlopeBlendTime = 0.2f;
    // Seconds over which the slope the gait reads follows the measured one.
    private const float SlopeGradientSmoothing = 0.1f;
    // On a slope, a stance foot's ground is also looked for this much further above and below than on flat ground.
    private const float SlopeStanceSearch = 1.5f;
    // A foot's ground is read from this far above it.
    private const float AnkleProbeHeight = 0.15f;

    [SerializeField] private LegStepper frontLeg;
    [SerializeField] private LegStepper backLeg;
    [Tooltip("Debug marker showing where the gait is carrying the body.")]
    [SerializeField] private Transform strideTarget;

    // Standing height of the inverseRobo rig: its legs are 2.34 long and its hips 0.27 below the pivot, so the legs stand
    // at about 0.92 of their length, knees slightly bent.
    public const float DefaultBodyHeight = 2.4f;

    [Tooltip("Height of the body pivot above the feet when standing. Keep the standing legs a little short of straight " +
             "(about 0.9 of their length) so the knees never lock.")]
    [SerializeField] private float bodyHeight = DefaultBodyHeight;
    [SerializeField] private FootholdLimits footholdLimits = FootholdLimits.Default;
    [Tooltip("How far the body may lean past its feet before the character falls.")]
    [SerializeField] private float edgeOverhang = 0.7f;
    [Tooltip("With no movement input, momentum carries the body at most this far past its feet, so stopping near a ledge settles there.")]
    [SerializeField] private float stoppingOverhang = 0.3f;
    [SerializeField] private IdleStanceSettings idle = IdleStanceSettings.Default;

    [Header("Gait")]
    [SerializeField] private GaitProfile walk = GaitProfile.Walk;
    [SerializeField] private GaitProfile run = GaitProfile.Run;
    [SerializeField] private GaitProfile crouchGait = GaitProfile.Crouch;
    [SerializeField] private GaitTransitionSettings transitions = GaitTransitionSettings.Default;
    [SerializeField] private BalanceSettings balance = BalanceSettings.Default;
    [SerializeField] private StepArcShape stepArc = new StepArcShape();

    [SerializeField] private BodyMassSettings body = BodyMassSettings.Default;

    private readonly List<Vector2> footholdCandidates = new List<Vector2>();
    private readonly StepScheduler scheduler = new StepScheduler();
    private readonly GaitDiagnostics diagnostics = new GaitDiagnostics();
    private readonly SupportLegResolver supportResolver = new SupportLegResolver();
    private float supportCatchUp = 1f;
    private float lastBalanceStepTime = float.NegativeInfinity;
    // The intended speed each leg's step in the air was planned for.
    private (float front, float back) plannedIntent;
    private Vector2 bodyTarget;
    private (float support, float surge) pelvisOffsets;
    private (float baseline, float gait, float ceiling) bodyHeights;
    private bool lastStepFoundNoFoothold;
    private int lastGaitFrame = -10;
    private BodyCarriage carriage;
    private float groundSpeed;
    private float intendedSpeed;
    // How crouched the character is and how the crouch shapes the body; set every frame by the crouch controller.
    private float crouchShare;
    private CrouchSettings crouchSettings = CrouchSettings.Default;
    // How much lower a hard landing holds the pelvis, recovering through its own spring.
    private readonly SecondOrderDynamics landingCompression = new SecondOrderDynamics(0f);
    private SpringSettings landingCompressionSpring = new SpringSettings(3f, 0.8f, 0f);
    // After a landing in motion: which leg steps first when the gait picks up (null: the gait starts afresh from a stance).
    private bool? pendingFrontLegStepsFirst;
    private float anchorX;
    private CharacterEvents events;
    private CharacterFacing facing;
    // The one-way platforms the character is dropping through; the composition hands it the character's settings.
    private OneWayPassage oneWay = new OneWayPassage(new OneWaySettings());
    // How walkable slopes shape the gait; the composition hands it the character's settings.
    private SlopeSettings slope = new SlopeSettings();
    // The ground under the pelvis this gait step (its height and slope), and how far the gait has blended into slope
    // behaviour (0 on flat ground) with the slope it reads.
    private TerrainSample terrain;
    private (float share, float gradient) slopeBlend;
    // Where each foot's ground was last read, so it is read once per foothold.
    private (Vector2 front, Vector2 back) ankleGroundSpots = (new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN));

    public LegStepper FrontLeg => frontLeg;
    public LegStepper BackLeg => backLeg;
    public float BodyHeight { get => bodyHeight; set => bodyHeight = value; }
    // Far enough that open ground never shortens a step: a shorter look-ahead would land running feet under the hip.
    public float WallProbeDistance => Mathf.Max(MinWallLookAhead, GaitTiming.LandingReach(run.speed, run));
    // Crouched, the feet stand a little wider apart.
    public float StanceHalfWidth => idle.stanceHalfWidth * Mathf.Lerp(1f, crouchSettings.stanceWidthScale, crouchShare);
    public float MaxStepUp => footholdLimits.maxStepUp;
    public float MaxStepDown => footholdLimits.maxStepDown;
    public float StanceHeight => (frontLeg.FootTarget.y + backLeg.FootTarget.y) / 2f;
    public LayerMask GroundMask => footholdLimits.groundMask;
    public OneWayPassage OneWay { get => oneWay; set => oneWay = value ?? oneWay; }
    public SlopeSettings Slope { get => slope; set => slope = value ?? slope; }
    // How far the gait walks as on a slope (0 flat ground, 1 a slope) and the gradient it walks on (rise per unit of x,
    // positive rising to the right; 0 on flat ground).
    public float SlopeShare => slopeBlend.share;
    public float SlopeGradient => slopeBlend.gradient * slopeBlend.share;
    // The ground height the body rides above: the mean height of the feet on flat ground, the surface under the pelvis on
    // a slope (which rises smoothly as the body moves, where the heights of the feet change a step at a time).
    public float GroundUnderBody => slopeBlend.share > 0f && terrain.Found ? Mathf.Lerp(StanceHeight, terrain.Height, slopeBlend.share) : StanceHeight;
    // The one-way platforms that hold feet standing at the given height: those whose top is at or below it.
    public OneWaySupport SupportAt(float feetY) => new OneWaySupport(feetY + OneWayFilter.StandingTolerance, (oneWay, Time.time));
    // In the air: the one-way platforms a body whose feet move down by fallDistance this frame (negative: up) would stand
    // on. Every top the feet cross from above holds them, however far they fall, and so does one they only just cleared
    // (the landing catch); a body that is rising passes up through all of them (OneWaySupport.Sweeping).
    public OneWaySupport AirSupport(float fallDistance) {
        float feetBefore = transform.position.y - bodyHeight;
        return OneWaySupport.Sweeping((feetBefore, feetBefore - fallDistance), oneWay.Settings.landingCatch, (oneWay, Time.time));
    }
    // The ground and support for a ray standing for feet at the given height.
    public (LayerMask mask, OneWaySupport support) GroundAt(float feetY) => (footholdLimits.groundMask, SupportAt(feetY));
    public Vector2 BodyVelocity => Carriage.Velocity;

    // The walk, run and crouch profiles with how crouched the character is: asks "the gait at a speed".
    // On a slope the gait set carries it: slower uphill, a little faster downhill, shorter steps (GaitSet.Slope).
    public GaitSet Gaits => new GaitSet((walk, run, crouchGait), transitions, crouchShare).OnSlope(GaitSlope);
    private SlopeGait GaitSlope => IsDrivingGait && SlopeGradient != 0f ? new SlopeGait(SlopeGradient, slope) : SlopeGait.Flat;
    // How crouched the character is (0 standing, 1 crouched), what a crouch does to the body, and how far it lowers the pelvis.
    public float CrouchShare => crouchShare;
    public CrouchSettings CrouchSettings => crouchSettings;
    public float CrouchDrop => crouchSettings.depth * crouchShare;
    // How much lower a hard landing is still holding the pelvis.
    public float LandingCompression => landingCompression.Value;

    // The blend of the walk, run and crouch profiles currently in use.
    public GaitProfile CurrentGait { get; private set; } = GaitProfile.Walk;
    // How far the gait is from standing toward walking and from walking toward running, from the ground speed alone.
    public LocomotionPace Pace { get; private set; } = LocomotionPace.Standing;
    // Signed ground speed the gait is carrying the body at (positive toward world right).
    public float GroundSpeed => groundSpeed;
    // Where the legs are in their shared cycle; the torso and arms move in step with it.
    public float GaitPhase => scheduler.Phase;
    // What the gait did recently (contacts, balance steps, resyncs), for inspection only.
    public GaitDiagnostics Diagnostics => diagnostics;

    // Tuning: each group of settings as a value; a new value takes effect on the next gait tick.
    public GaitProfile Walk { get => walk; set => walk = value; }
    public GaitProfile Run { get => run; set => run = value; }
    public GaitProfile CrouchGait { get => crouchGait; set => crouchGait = value; }
    public GaitTransitionSettings Transitions { get => transitions; set => transitions = value; }
    public BalanceSettings Balance { get => balance; set => balance = value; }
    public BodyMassSettings BodyMass { get => body; set => body = value; }
    public IdleStanceSettings IdleStance { get => idle; set => idle = value; }
    // Debug override of the target speed (GaitPreview.Off in normal play).
    public GaitPreview Preview { get; set; } = GaitPreview.Off;

    // Inspection: what the gait is doing this frame.
    public float IntendedSpeed => intendedSpeed;
    public bool IsMoving => scheduler.IsMoving;
    public float CyclePeriod => GaitTiming.CyclePeriod(groundSpeed, CurrentGait);
    public float SwingShare => scheduler.SwingShare;
    public float StrideAnchorX => anchorX;
    public Vector2 BodyTarget => bodyTarget;
    // The leg carrying the body and how far through its stance the body is.
    public SupportStance Support => supportResolver.Current;
    // The pelvis target's horizontal shifts from the anchor: toward a stance centred over the support foot, and the
    // body's speed change over its support.
    public (float support, float surge) PelvisOffsets => pelvisOffsets;
    // The pelvis heights this frame: the gait's baseline (standing height less the gait's drop), the gait height with its
    // bob, and the ceiling the planted legs' reach allows.
    public (float baseline, float gait, float ceiling) BodyHeights => bodyHeights;
    // Ground spots the last planned step chose its foothold from.
    public IReadOnlyList<Vector2> FootholdCandidates => footholdCandidates;
    // True while a grounded state ticks the gait this frame (false in the air, rolling or climbing).
    public bool IsDrivingGait => Time.frameCount - lastGaitFrame <= 1;

    // Held by the jump wind-up to lower the body (the load before a jump). Crouching is a separate share of the gait.
    public float WindupCrouch { get; set; }
    // Carries the body at the ground speed it has, whatever the move input (the jump wind-up of a moving character).
    public bool HoldGroundSpeed { get; set; }
    // The fastest (u/s, either way) the gait heads for, e.g. while pushing a crate; infinity when nothing limits it. Not
    // serialized: a script reload clears it, and the push sets it again every frame it lasts.
    public float SpeedLimit { get; set; } = float.PositiveInfinity;

    // The body leans past both feet toward a spot where no foothold was found, e.g. walking off a ledge. On solid ground
    // balance steps keep the feet under the body, so leaning ahead alone is never a fall.
    public bool IsPastSupportEdge => lastStepFoundNoFoothold && SupportOverhang() > edgeOverhang;

    // Neither foot stands on ground any more (e.g. spawned in the air or the floor moved away).
    public bool HasLostGround => !IsFootOnGround(frontLeg) && !IsFootOnGround(backLeg);

    // Called every frame by the crouch controller.
    public void ApplyCrouch((float share, CrouchSettings settings) crouched) {
        crouchShare = Mathf.Clamp01(crouched.share);
        crouchSettings = crouched.settings;
    }

    public void Connect((CharacterEvents events, CharacterFacing facing) character) {
        events = character.events;
        facing = character.facing;
    }

    private BodyCarriage Carriage => carriage ?? (carriage = new BodyCarriage(transform));

    // Hands the body to the gait with its current momentum, e.g. after a landing (a downward velocity becomes the squash).
    public void BeginGroundedMotion(Vector2 velocity) {
        Carriage.Restart(velocity);
        groundSpeed = Mathf.Clamp(velocity.x, -run.speed, run.speed);
        anchorX = transform.position.x;
        TrackTerrain(float.PositiveInfinity);
        BlendGaitWithSpeed(float.PositiveInfinity);
    }

    // The feet were just placed in a stance (spawning, landing, after a roll or a climb): the gait starts afresh from it.
    public void RestartGaitFromStance() {
        scheduler.RestartFromStance();
        supportResolver.Reset();
        if (pendingFrontLegStepsFirst.HasValue) {
            scheduler.ContinueRhythm(pendingFrontLegStepsFirst.Value);
            pendingFrontLegStepsFirst = null;
        }
    }

    // A hard landing holds the pelvis this far lower at first (on top of the squash from the landing speed).
    public void ApplyLandingCompression((float depth, SpringSettings spring) landing) {
        landingCompression.ResetWithVelocity((landing.depth, 0f));
        landingCompressionSpring = landing.spring;
    }

    // feetSpan bounds where feet may go (up to wall faces); bodySpan bounds the body pivot so its hull stays out of walls.
    public void TickWalk(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        lastGaitFrame = Time.frameCount;
        int gaitSteps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / MaxGaitStep));
        for (int step = 0; step < gaitSteps; step++) {
            TickGaitStep(deltaTime / gaitSteps, intent);
        }
    }

    private void TickGaitStep(float deltaTime, (float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        TrackTerrain(deltaTime);
        AccelerateGround((intent.direction, intent.running, deltaTime));
        landingCompression.Update((0f, deltaTime), landingCompressionSpring);
        AdvanceAnchor((deltaTime, AnchorSpan(intent)));
        BlendGaitWithSpeed(deltaTime);
        AdvanceGaitClock(deltaTime);
        RecoverSupport();
        LiftFeetOnCue(intent.feetSpan);
        ReplanSwingsOnIntentChange(intent.feetSpan);
        SettleFeetWhenIdle();
        TickLegs(deltaTime);
        AlignAnklesToGround();
        TrackSupport();
        CarryBodyOverFeet((deltaTime, intent.bodySpan));
        strideTarget.position = new Vector2(anchorX, StanceHeight);
    }

    // The leg whose foot is further along the facing direction leads.
    public (LegStepper lead, LegStepper trail) LegsOrderedByFacing(int facingSign) {
        bool frontLeads = frontLeg.FootTarget.x * facingSign >= backLeg.FootTarget.x * facingSign;
        return frontLeads ? (frontLeg, backLeg) : (backLeg, frontLeg);
    }

    public void PlaceStrideTargetUnderBody() => strideTarget.position = new Vector2(transform.position.x, StanceHeight);

    // Picks ground under both feet around centerX, within a step of groundY, and lets the legs settle onto it.
    public bool TrySettleStance((float centerX, int facingSign, float groundY) stance) {
        float frontX = stance.centerX + stance.facingSign * StanceHalfWidth;
        float backX = stance.centerX - stance.facingSign * StanceHalfWidth;
        if (!TryFindStanceGround((frontX, stance.groundY), out Vector2 frontGround)
            || !TryFindStanceGround((backX, stance.groundY), out Vector2 backGround)) {
            return false;
        }

        frontLeg.SettleOnto(frontGround);
        backLeg.SettleOnto(backGround);
        lastStepFoundNoFoothold = false;
        pendingFrontLegStepsFirst = null;
        PlaceStrideTargetUnderBody();
        return true;
    }

    // The feet take where the gait has them at this speed: either side of the body standing, or the leading foot ahead
    // and the other trailing behind when moving, with the trailing foot to step first once the gait picks up. leadIsFront:
    // the leg that was reaching forward in the air, which lands first. A foot whose spot is past the edge of the ground
    // (landing near a platform's end, or on one narrower than the stance) takes the last ground between its spot and the
    // body instead, so the body lands on what is under it. False if a foot finds no ground even under the body.
    public bool TrySettleLanding((float centerX, int facingSign, float groundY) landing, (float speedX, bool leadIsFront, float movingShare) motion) {
        LandingStance.Columns columns = LandingStance.Of((landing.centerX, landing.facingSign, motion.speedX), (StanceHalfWidth, Gaits, motion.movingShare));
        if (!TryFindLandingGround((columns.Lead, landing.centerX, landing.groundY), out Vector2 leadGround)
            || !TryFindLandingGround((columns.Trail, landing.centerX, landing.groundY), out Vector2 trailGround)) {
            return false;
        }

        (LegStepper lead, LegStepper trail) legs = motion.leadIsFront ? (frontLeg, backLeg) : (backLeg, frontLeg);
        legs.lead.SettleOnto(leadGround);
        legs.trail.SettleOnto(trailGround);
        lastStepFoundNoFoothold = false;
        pendingFrontLegStepsFirst = columns.IsMoving ? (bool?)(legs.trail == frontLeg) : null;
        PlaceStrideTargetUnderBody();
        return true;
    }

    // Ground below the body within standing height plus extraDepth (e.g. this frame's fall distance).
    public bool TryFindGroundBelowBody(float extraDepth, out Vector2 ground) {
        Vector2 origin = transform.position;
        return FootholdProbe.TryFindGroundBelow((origin, bodyHeight + extraDepth), (footholdLimits.groundMask, AirSupport(extraDepth)), out ground);
    }

    // The collider under a foot it stands on, if any.
    public bool TryFindGroundUnderFoot(LegStepper leg, out Collider2D ground) {
        Vector2 origin = leg.FootTarget + Vector2.up * GroundContactTolerance;
        bool found = FootholdProbe.TryFindGroundHitBelow((origin, 2f * GroundContactTolerance), GroundAt(leg.FootTarget.y), out RaycastHit2D hit);
        ground = found ? hit.collider : null;
        return found;
    }

    // Ground for a landing foot at footX or, past an edge, the outermost ground on the way in to the body's column.
    private bool TryFindLandingGround((float footX, float centerX, float groundY) column, out Vector2 ground) {
        for (int step = 0; step <= LandingEdgeSearchSteps; step++) {
            float x = Mathf.Lerp(column.footX, column.centerX, step / (float)LandingEdgeSearchSteps);
            if (TryFindStanceGround((x, column.groundY), out ground)) {
                return true;
            }
        }
        ground = default;
        return false;
    }

    // Within a step of groundY; failing that, on a slope (feet either side of the body stand higher and lower than a step
    // allows on steep ground), further above and below.
    private bool TryFindStanceGround((float x, float groundY) column, out Vector2 ground) {
        Vector2 origin = new Vector2(column.x, column.groundY + footholdLimits.maxStepUp);
        float depth = footholdLimits.maxStepUp + footholdLimits.maxStepDown;
        if (FootholdProbe.TryFindGroundBelow((origin, depth), GroundAt(column.groundY), out ground)) {
            return true;
        }
        Vector2 widerOrigin = origin + Vector2.up * SlopeStanceSearch;
        bool found = FootholdProbe.TryFindGroundBelow((widerOrigin, depth + 2f * SlopeStanceSearch), GroundAt(column.groundY), out ground, out Vector2 normal);
        return found && SlopeMath.IsSloped(normal, slope.maxSlopeAngle);
    }

    private bool IsFootOnGround(LegStepper leg) {
        Vector2 origin = leg.FootTarget + Vector2.up * GroundContactTolerance;
        return FootholdProbe.TryFindGroundBelow((origin, 2f * GroundContactTolerance), GroundAt(leg.FootTarget.y), out _);
    }

    // ---- Speed -----------------------------------------------------------------------------------------------

    // The run input only picks the target speed; the gait itself follows the speed the body reaches.
    private void AccelerateGround((float direction, bool running, float deltaTime) input) {
        if (HoldGroundSpeed) {
            intendedSpeed = groundSpeed;
            return;
        }
        intendedSpeed = Preview.IsActive
            ? Preview.IntendedSpeed((input.direction, facing?.Sign ?? 1, walk.speed, run.speed))
            : Gaits.TargetSpeed((input.direction, input.running));
        float limit = Mathf.Max(0f, SpeedLimit);
        intendedSpeed = Mathf.Clamp(intendedSpeed, -limit, limit);
        groundSpeed = GroundSpeedRamp.Step((groundSpeed, intendedSpeed, CurrentGait), input.deltaTime);
    }

    // The walk/run blend follows the ground speed, rate-limited so a sudden stop at a wall does not snap the gait; the
    // crouch gait is laid over it as far as the character is crouched and slow.
    private void BlendGaitWithSpeed(float deltaTime) {
        GaitSet gaits = Gaits;
        float running = Mathf.MoveTowards(Pace.Running, gaits.RunningAt(groundSpeed), transitions.runBlendRate * deltaTime);
        Pace = new LocomotionPace((gaits.MovingAt(groundSpeed), running));
        CurrentGait = gaits.At(groundSpeed, running);
    }

    // Walls always bound the body; while stopping (no target speed), so does the stopping margin past the leading foot. Only the
    // side the body moves toward is bounded: a running body is often still behind feet landing ahead of it.
    private (float min, float max) AnchorSpan((float direction, bool running, (float min, float max) feetSpan, (float min, float max) bodySpan) intent) {
        if (!IsStopping || Mathf.Approximately(groundSpeed, 0f)) {
            return intent.bodySpan;
        }
        if (groundSpeed > 0f) {
            float supportMax = Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x) + stoppingOverhang;
            return (intent.bodySpan.min, Mathf.Min(intent.bodySpan.max, supportMax));
        }
        float supportMin = Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x) - stoppingOverhang;
        return (Mathf.Max(intent.bodySpan.min, supportMin), intent.bodySpan.max);
    }

    // The point the body is carried toward moves at the ground speed and stops at its span's ends.
    private void AdvanceAnchor((float deltaTime, (float min, float max) bodySpan) frame) {
        float advanced = anchorX + groundSpeed * frame.deltaTime;
        anchorX = Mathf.Clamp(advanced, frame.bodySpan.min, frame.bodySpan.max);
        if (!Mathf.Approximately(anchorX, advanced)) {
            groundSpeed = 0f;
        }
    }

    // ---- Steps -----------------------------------------------------------------------------------------------

    // The clock advances by distance over the stride, quickened while the feet catch up. From a stance the trailing
    // foot steps first.
    private void AdvanceGaitClock(float deltaTime) {
        bool frontLegTrails = LegsOrderedByFacing((int)Mathf.Sign(groundSpeed)).trail == frontLeg;
        float strideLength = GaitTiming.StrideLength(groundSpeed, CurrentGait) / supportCatchUp;
        float swingShare = SwingTiming.CycleShareAt(SwingTimingSpeed(), CurrentGait);
        scheduler.Advance((groundSpeed, strideLength, swingShare, deltaTime, frontLegTrails), transitions);
    }

    // Speeding up, swings are timed for at least part of the intended speed; slowing down, for the current speed.
    private float SwingTimingSpeed() {
        bool isSpeedingUp = intendedSpeed * groundSpeed > 0f && Mathf.Abs(intendedSpeed) > Mathf.Abs(groundSpeed);
        float speed = Mathf.Abs(groundSpeed);
        return isSpeedingUp ? Mathf.Max(speed, transitions.settingOffShare * Mathf.Abs(intendedSpeed)) : speed;
    }

    // A planted foot trailing the hip further than a normal stance ends quickens the cadence; one far behind takes an
    // emergency balance step out of turn.
    private void RecoverSupport() {
        if (!IsMoving) {
            supportCatchUp = 1f;
            diagnostics.RecordCatchUp(supportCatchUp);
            return;
        }

        (float excess, LegStepper leg) trailing = FurthestTrailingPlantedFoot();
        SupportRecovery.Response response = SupportRecovery.Assess((trailing.excess, ProjectedTrailExcess(), Time.time - lastBalanceStepTime), balance);
        supportCatchUp = response.CatchUp;
        diagnostics.RecordCatchUp(supportCatchUp);

        bool isFrontLeg = trailing.leg == frontLeg;
        if (response.NeedsBalanceStep && !scheduler.IsCued(isFrontLeg)) {
            scheduler.CueOutOfTurn(isFrontLeg);
            lastBalanceStepTime = Time.time;
            diagnostics.RecordBalanceStep(Time.time);
        }
    }

    // How much further behind the hip than a normal stance ends the furthest planted foot is (negative: within it).
    private (float excess, LegStepper leg) FurthestTrailingPlantedFoot() {
        float normalLag = GaitTiming.ToeOffLag(groundSpeed, CurrentGait);
        float frontExcess = TrailingLag(frontLeg) - normalLag;
        float backExcess = TrailingLag(backLeg) - normalLag;
        return frontExcess >= backExcess ? (frontExcess, frontLeg) : (backExcess, backLeg);
    }

    // How far beyond its leg's reach a planted foot will trail when the clock cues it, if the cadence does not change;
    // the larger of the two feet. A stance only a little long is left to the measured trail.
    private float ProjectedTrailExcess() {
        float strideLength = GaitTiming.StrideLength(groundSpeed, CurrentGait);
        return Mathf.Max(ProjectedTrailBeyondReach((frontLeg, true), strideLength), ProjectedTrailBeyondReach((backLeg, false), strideLength));
    }

    private float ProjectedTrailBeyondReach((LegStepper leg, bool isFront) planted, float strideLength) {
        if (!planted.leg.IsGrounded) {
            return float.NegativeInfinity;
        }
        float projectedLag = TrailingLag(planted.leg) + scheduler.StridesUntilCue(planted.isFront) * strideLength;
        float hipHeight = planted.leg.HipPosition.y - planted.leg.FootTarget.y;
        float reachableLag = Mathf.Sqrt(Mathf.Max(0f, planted.leg.LegLength * planted.leg.LegLength - hipHeight * hipHeight));
        return projectedLag - reachableLag;
    }

    private float TrailingLag(LegStepper leg) {
        return leg.IsGrounded ? (leg.HipPosition.x - leg.FootTarget.x) * Mathf.Sign(groundSpeed) : float.NegativeInfinity;
    }

    // How far the leg's hip joint sits ahead of the body pivot; the stance is measured from the hip, and leaning the
    // pelvis swings the hips behind the pivot.
    private float HipOffsetX(LegStepper leg) => leg.HipPosition.x - transform.position.x;

    // A cued leg lifts as soon as it is planted and the other foot lets it; until then the cue stays pending.
    private void LiftFeetOnCue((float min, float max) feetSpan) {
        if (!IsMoving) {
            return;
        }
        TryLiftOnCue((frontLeg, backLeg), feetSpan);
        TryLiftOnCue((backLeg, frontLeg), feetSpan);
    }

    // A cue with no foothold ahead is used up, so at a ledge the body moves on past the planted feet and falls.
    private void TryLiftOnCue((LegStepper stepping, LegStepper other) legs, (float min, float max) feetSpan) {
        bool isFrontLeg = legs.stepping == frontLeg;
        if (!scheduler.MayLiftNow(isFrontLeg, (legs.stepping.IsGrounded, legs.other.LetsOtherLegLift))) {
            return;
        }
        scheduler.ConsumeCue(isFrontLeg);

        float direction = Mathf.Sign(groundSpeed);
        (float landingHipX, float desiredX, float duration) landing = ForecastLanding((direction, 0f, (legs.other.FootTarget.x, HipOffsetX(legs.stepping)), feetSpan));
        SwingTiming timing = GaitSwingTiming(landing.duration);
        lastStepFoundNoFoothold = !TryStepToward((legs.stepping, legs.other, landing.desiredX, direction), (landing.landingHipX, timing, CurrentGait.liftHeight, Pace.Running));
        if (lastStepFoundNoFoothold) {
            diagnostics.RecordFootholdFailure(Time.time);
            return;
        }
        RememberPlannedIntent(isFrontLeg);
        Carriage.Push(CurrentGait.pushOffImpulse * Pace.Moving);
    }

    // When the intended speed changes mid-stride (stopping, turning, speeding up), a gait step still in the air is
    // re-planned for the new motion, so a running foot does not land where a running hip would have been.
    private void ReplanSwingsOnIntentChange((float min, float max) feetSpan) {
        ReplanSwing((frontLeg, backLeg), feetSpan);
        ReplanSwing((backLeg, frontLeg), feetSpan);
    }

    private void ReplanSwing((LegStepper stepping, LegStepper other) legs, (float min, float max) feetSpan) {
        bool isFrontLeg = legs.stepping == frontLeg;
        float planned = isFrontLeg ? plannedIntent.front : plannedIntent.back;
        if (!legs.stepping.IsSwingingOnClock || Mathf.Approximately(planned, intendedSpeed) || Mathf.Approximately(groundSpeed, 0f)) {
            return;
        }
        RememberPlannedIntent(isFrontLeg);

        float progress = legs.stepping.StepProgress;
        (float landingHipX, float desiredX, float duration) landing = ForecastLanding((Mathf.Sign(groundSpeed), progress, (legs.other.FootTarget.x, HipOffsetX(legs.stepping)), feetSpan));
        if (!TryPlanFoothold((legs.stepping, legs.other, landing.desiredX), landing.landingHipX, out Vector2 foothold)) {
            return;
        }
        // The ceiling is a whole-swing duration: the remaining share of it must cover the forecast rest of the swing.
        float longestDuration = SwingCeilingSlack * landing.duration / Mathf.Max(0.05f, 1f - progress);
        SwingTiming timing = SwingTiming.OnClock((longestDuration, legs.stepping.SwingTiming.ReleaseProgress));
        legs.stepping.RetargetStep((foothold, timing), stepArc);
    }

    private void RememberPlannedIntent(bool frontLeg) {
        plannedIntent = frontLeg ? (intendedSpeed, plannedIntent.back) : (plannedIntent.front, intendedSpeed);
    }

    private bool IsStopping => Mathf.Approximately(intendedSpeed, 0f);

    // Where the hip will be when the rest of the swing ends, where the foot should land and how long that takes. Moving
    // on, the foot lands its contact lead ahead of the hip; stopping, on a stance spot where the hip stops.
    // legs: where the other foot stands, and how far the stepping leg's hip sits ahead of the body pivot.
    private (float landingHipX, float desiredX, float duration) ForecastLanding((float direction, float startProgress, (float otherFootX, float hipOffsetX) legs, (float min, float max) feetSpan) step) {
        // A stopping clock would stretch the last step into slow motion: it is held to a natural swing at the current speed.
        float longestDuration = IsStopping ? GaitTiming.SwingDuration(groundSpeed, CurrentGait) : CurrentGait.maxSwingDuration;
        SwingForecast.Request request = new SwingForecast.Request((groundSpeed, intendedSpeed, scheduler.SwingShare, longestDuration, step.startProgress), Gaits);
        SwingForecast.Prediction swing = SwingForecast.Predict(request);
        float landingHipX = PelvisRidingAnchor((anchorX + swing.Travel, swing.LandingSpeed)) + step.legs.hipOffsetX;
        float landingX = IsStopping
            ? StoppingSpot((landingHipX, step.direction, step.legs.otherFootX))
            : landingHipX + step.direction * GaitTiming.ContactLead(swing.LandingSpeed, SwingForecast.GaitAt(swing.LandingSpeed, Gaits));
        return (landingHipX, Mathf.Clamp(landingX, step.feetSpan.min, step.feetSpan.max), swing.Duration);
    }

    // Of the two stance spots around where the hip stops, the one away from the other foot: the stopping step lands in
    // the stance rather than beside a foot that already took the other spot.
    private float StoppingSpot((float stopHipX, float direction, float otherFootX) stop) {
        float center = stop.stopHipX + stop.direction * idle.stanceLead;
        float frontSpot = center + stop.direction * StanceHalfWidth;
        float backSpot = center - stop.direction * StanceHalfWidth;
        return Mathf.Abs(stop.otherFootX - frontSpot) >= Mathf.Abs(stop.otherFootX - backSpot) ? frontSpot : backSpot;
    }

    // The swing runs on the gait clock, finishing within a little more than its forecast duration if the clock stalls;
    // the other foot waits for it to be far enough along: late when walking, mid-swing when running.
    private SwingTiming GaitSwingTiming(float forecastDuration) {
        float releaseProgress = Mathf.Lerp(transitions.walkLiftOverlap, transitions.runLiftOverlap, Pace.Running);
        return SwingTiming.OnClock((SwingCeilingSlack * forecastDuration, releaseProgress));
    }

    // Standing still, each foot steps toward its spot under the torso, the furthest first, finishing in a stance.
    private void SettleFeetWhenIdle() {
        bool isIdle = !IsMoving && frontLeg.IsGrounded && backLeg.IsGrounded && Mathf.Abs(Carriage.Velocity.x) < SettledBodySpeed;
        if (!isIdle) {
            return;
        }

        float centerX = StanceCenterX();
        bool frontIsLeft = frontLeg.FootTarget.x <= backLeg.FootTarget.x;
        float frontSpot = centerX + (frontIsLeft ? -StanceHalfWidth : StanceHalfWidth);
        float backSpot = centerX + (frontIsLeft ? StanceHalfWidth : -StanceHalfWidth);
        float frontError = Mathf.Abs(frontLeg.FootTarget.x - frontSpot);
        float backError = Mathf.Abs(backLeg.FootTarget.x - backSpot);
        if (Mathf.Max(frontError, backError) <= idle.footTolerance) {
            return;
        }

        // With both feet off their spots, the leg that did not step last goes first, so the legs keep alternating.
        bool bothOff = Mathf.Min(frontError, backError) > idle.footTolerance;
        bool frontSteps = bothOff ? !scheduler.LastLiftWasFront : frontError >= backError;
        (LegStepper stepping, LegStepper planted, float spot) settle = frontSteps
            ? (frontLeg, backLeg, frontSpot)
            : (backLeg, frontLeg, backSpot);
        float towardSpot = Mathf.Sign(settle.spot - settle.stepping.FootTarget.x);
        TryStepToward((settle.stepping, settle.planted, settle.spot, towardSpot), (transform.position.x, SwingTiming.Timed(idle.settleStepDuration), idle.settleLiftHeight, 0f));
    }

    // The torso leans a little ahead of the pelvis, so the standing feet are centred slightly ahead of it.
    private float StanceCenterX() {
        int facingSign = facing?.Sign ?? 0;
        return transform.position.x + facingSign * idle.stanceLead;
    }

    // A step must carry the foot at least a minimum step along its direction; otherwise (e.g. at a ledge whose
    // nearest ground is under the foot already) no step is taken, rather than stepping on the spot forever.
    private bool TryStepToward(
        (LegStepper stepping, LegStepper planted, float desiredX, float direction) step,
        (float landingHipX, SwingTiming timing, float liftHeight, float runShare) motion) {
        if (!TryPlanFoothold((step.stepping, step.planted, step.desiredX), motion.landingHipX, out Vector2 foothold)) {
            return false;
        }
        if ((foothold.x - step.stepping.FootTarget.x) * step.direction < footholdLimits.minStepLength) {
            return false;
        }
        step.stepping.BeginStep((foothold, motion.timing), (motion.liftHeight, motion.runShare));
        if (scheduler.RecordLift(step.stepping == frontLeg)) {
            diagnostics.RecordPhaseResync(Time.time);
        }
        return true;
    }

    private bool TryPlanFoothold((LegStepper stepping, LegStepper planted, float desiredX) step, float landingHipX, out Vector2 foothold) {
        FootholdRequest request = new FootholdRequest {
            DesiredX = step.desiredX,
            LandingHipX = landingHipX,
            PlantedFoot = step.planted.FootTarget,
            HipHeight = step.stepping.HipPosition.y - StanceHeight,
            LegLength = step.stepping.LegLength,
            ForwardSign = Mathf.Sign(step.desiredX - transform.position.x),
        };

        FootholdLimits limits = StepLimitsOnSlope((Mathf.Abs(request.DesiredX - request.PlantedFoot.x), SlopeAhead(request.DesiredX, request.PlantedFoot)));
        FootholdProbe.CollectCandidates((request.DesiredX, request.PlantedFoot.y, step.stepping.FootTarget), (limits, SupportAt(request.PlantedFoot.y)), footholdCandidates);
        return FootholdPlanner.TryChooseFoothold(request, (footholdCandidates, limits), out foothold);
    }

    // On a slope a step rises or drops by its length times the gradient: the step window grows to take footholds as far up
    // or down as the slope carries a step of this length (and the search beyond it). The steeper of the slope underfoot
    // and the slope at the landing spot counts, so a step off a crest onto a ramp finds it. Flat ground keeps the limits.
    private FootholdLimits StepLimitsOnSlope((float length, float gradientAhead) step) {
        FootholdLimits limits = footholdLimits;
        float gradient = Mathf.Max(Mathf.Abs(SlopeGradient), Mathf.Abs(step.gradientAhead));
        if (gradient <= 0f) {
            return limits;
        }
        float rise = gradient * (step.length + limits.searchRadius) + limits.searchStep;
        limits.maxStepUp = Mathf.Max(limits.maxStepUp, rise);
        limits.maxStepDown = Mathf.Max(limits.maxStepDown, rise);
        return limits;
    }

    // Measured in the direction of motion only: a running body is often behind feet that are landing ahead of it,
    // which is not an edge. An idle body is held over its feet by the stopping margin, so it has no overhang.
    private float SupportOverhang() {
        if (!IsMoving) {
            return 0f;
        }
        float bodyPositionX = transform.position.x;
        float supportFront = groundSpeed > 0f
            ? Mathf.Max(frontLeg.FootTarget.x, backLeg.FootTarget.x)
            : Mathf.Min(frontLeg.FootTarget.x, backLeg.FootTarget.x);
        return (bodyPositionX - supportFront) * Mathf.Sign(groundSpeed);
    }

    private void TickLegs(float deltaTime) {
        frontLeg.TickStep((deltaTime, scheduler.SwingProgressThisTick), stepArc);
        backLeg.TickStep((deltaTime, scheduler.SwingProgressThisTick), stepArc);
    }

    // ---- Body ------------------------------------------------------------------------------------------------

    private void CarryBodyOverFeet((float deltaTime, (float min, float max) bodySpan) frame) {
        if (Carriage.NeedsRestart) {
            BeginGroundedMotion(Vector2.zero);
        }

        AbsorbFootPlant(frontLeg);
        AbsorbFootPlant(backLeg);

        float gaitHeight = GaitHeight();
        (float target, float ceiling) height = BodyHeightLimits(gaitHeight);
        bodyHeights = (BaselineHeight, gaitHeight, height.ceiling);
        bodyTarget = new Vector2(BodyTargetX(), height.target);
        Carriage.Carry((bodyTarget, frame.deltaTime), (body.horizontalSpring, body.verticalSpring, frame.bodySpan, height.ceiling));
    }

    // The body takes a planted foot's impact as a downward push, like a knee bending under the weight.
    private void AbsorbFootPlant(LegStepper leg) {
        if (!leg.TryConsumePlant(out float impactSpeed)) {
            return;
        }
        Carriage.Push(-Mathf.Min(body.maxPlantImpulse, impactSpeed * CurrentGait.plantImpulse));
        diagnostics.RecordPlant(leg == frontLeg, Time.time);
        events?.RaiseFootPlanted((leg.FootTarget, impactSpeed));
    }

    // The moving anchor carries the body through the world; the support foot shifts the pelvis around it, so the pelvis
    // travels through the legs instead of on a rail above them (PelvisSupportMotion). Both shifts fade out as the
    // character slows to a stand.
    private float BodyTargetX() {
        SupportStance stance = supportResolver.Current;
        float supportHipX = PelvisRidingAnchor((anchorX, groundSpeed)) + SupportHipOffsetX(stance);
        float support = PelvisSupportMotion.SupportOffset((stance, supportHipX), (body.supportCoupling, body.maxSupportOffset));
        float surge = PelvisSupportMotion.Surge(stance, CurrentGait.surge);
        pelvisOffsets = (support * Pace.Moving, surge * Pace.Moving);
        return anchorX + pelvisOffsets.support + pelvisOffsets.surge;
    }

    // Where the pelvis rides an anchor at the given speed: it trails it by its spring's steady tracking lag. The support
    // offset and surge are zero at contact, so this is also where the pelvis lands a step. Comparing feet with the
    // pelvis rather than with the target it chases keeps the stance where the gait plans it.
    private float PelvisRidingAnchor((float anchorX, float speed) motion) {
        return motion.anchorX - Mathf.Sign(groundSpeed) * body.horizontalSpring.TrackingLag(Mathf.Abs(motion.speed));
    }

    private float SupportHipOffsetX(SupportStance stance) {
        if (!stance.HasSupport) {
            return 0f;
        }
        return HipOffsetX(stance.SupportLeg == SupportStance.Leg.Front ? frontLeg : backLeg);
    }

    private void TrackSupport() {
        float stanceTravel = GaitTiming.StanceTravel(groundSpeed, CurrentGait);
        supportResolver.Track((frontLeg.IsGrounded, backLeg.IsGrounded), (frontLeg.FootTarget.x, backLeg.FootTarget.x), (anchorX, Mathf.Sign(groundSpeed), (stanceTravel, CurrentGait.contactLeadShare)));
    }

    // The gait's height, never higher than the planted legs reach: the spring's target, and the ceiling it may not
    // overshoot. The reach is measured from where the body is: its springs trail the anchor a little at speed, and
    // measuring from the anchor would make a trailing foot seem further behind than it is. A foot so far away that
    // reaching it would sink the body more than the reach drop is about to step anyway, and does not hold it down.
    private (float target, float ceiling) BodyHeightLimits(float gaitHeight) {
        float pelvisX = transform.position.x;
        float reachable = Mathf.Min(HighestPelvisOver((frontLeg, pelvisX)), HighestPelvisOver((backLeg, pelvisX)));
        float ceiling = Mathf.Max(reachable, gaitHeight - body.maxReachDrop);
        return (Mathf.Min(ceiling, gaitHeight), ceiling);
    }

    // Standing height over the feet with the step's bob, sunk lower the faster the gait moves, by the crouch and by any
    // jump wind-up.
    // The bob is aimed ahead by the vertical spring's lag at the bob's rate, so the body's low point lands where the gait
    // puts it (just after contact walking, mid-stance running) rather than late.
    private float GaitHeight() {
        float bobRate = 2f / Mathf.Max(0.05f, CyclePeriod);
        float aimedPhase = scheduler.Phase + body.verticalSpring.PhaseLagCycles(bobRate) / 2f;
        float bob = GaitWaves.BodyBob((aimedPhase, LowestPhase)) * CurrentGait.bobAmplitude * Pace.Moving;
        return BaselineHeight + bob;
    }

    private float BaselineHeight => GroundUnderBody + SlopeLead + bodyHeight - CurrentGait.pelvisDrop * Pace.Moving - CrouchDrop - WindupCrouch - landingCompression.Value - SlopeDrop;

    // On a slope the pelvis rides lower: the feet ahead and behind it stand higher and lower than the ground under it.
    private float SlopeDrop => slope.bodyDrop * SlopeMath.Steepness(SlopeGradient) * slopeBlend.share;

    // On a slope the ground under the body rises or falls steadily as it moves; the target is aimed ahead by the vertical
    // spring's tracking lag, so the body rides the surface instead of trailing it.
    private float SlopeLead => SlopeGradient * body.verticalSpring.TrackingLag(groundSpeed);

    // ---- Slopes ----------------------------------------------------------------------------------------------

    // Reads the ground under where the pelvis rides (not the pelvis itself, whose support shifts and surge would make the
    // read height bob on a slope) and blends the gait toward slope behaviour where the ground is sloped, under the body or
    // under either foot (a foot stepping onto a ramp ahead must not lift the body off the flat ground it still stands
    // over, as the mean height of the feet would). A long deltaTime (a restart) takes the reading at once.
    private void TrackTerrain(float deltaTime) {
        terrain = SampleTerrain(PelvisRidingAnchor((anchorX, groundSpeed)));
        bool footOnSlope = SlopeMath.IsSloped(frontLeg.GroundNormal, slope.maxSlopeAngle) || SlopeMath.IsSloped(backLeg.GroundNormal, slope.maxSlopeAngle);
        float targetShare = terrain.Found && (terrain.IsSloped || footOnSlope) ? 1f : 0f;
        float targetGradient = terrain.Found ? terrain.Gradient : 0f;
        if (float.IsPositiveInfinity(deltaTime)) {
            slopeBlend = (targetShare, targetGradient);
            return;
        }
        float share = Mathf.MoveTowards(slopeBlend.share, targetShare, deltaTime / SlopeBlendTime);
        float gradient = Mathf.Lerp(slopeBlend.gradient, targetGradient, 1f - Mathf.Exp(-deltaTime / SlopeGradientSmoothing));
        slopeBlend = (share, share > 0f ? gradient : 0f);
    }

    // The gradient of walkable sloped ground at x, looked for as far above and below the planted foot as the steepest
    // walkable slope could carry the ground over the distance; 0 where the ground there is flat (or none is found).
    private float SlopeAhead(float x, Vector2 plantedFoot) {
        float slopeRise = Mathf.Abs(x - plantedFoot.x) * Mathf.Tan(Mathf.Clamp(slope.maxSlopeAngle, 0f, 80f) * Mathf.Deg2Rad) + footholdLimits.searchStep;
        Vector2 origin = new Vector2(x, plantedFoot.y + footholdLimits.maxStepUp + slopeRise);
        float reach = footholdLimits.maxStepUp + footholdLimits.maxStepDown + 2f * slopeRise;
        if (!FootholdProbe.TryFindGroundBelow((origin, reach), GroundAt(plantedFoot.y), out _, out Vector2 normal)) {
            return 0f;
        }
        return SlopeMath.IsSloped(normal, slope.maxSlopeAngle) ? SlopeMath.Gradient(normal) : 0f;
    }

    // Three rays down, under the given x and a little either side: the mean height of the ground, its mean gradient (flat
    // and too steep hits count as level) and whether any of it is a walkable slope. Found only when all three hit.
    private TerrainSample SampleTerrain(float x) {
        (float height, float gradient, bool sloped, int hits) sum = (0f, 0f, false, 0);
        for (int sample = -1; sample <= 1; sample++) {
            Vector2 origin = new Vector2(x + sample * TerrainSampleSpread, transform.position.y);
            float depth = bodyHeight + footholdLimits.maxStepDown + SlopeStanceSearch;
            if (!FootholdProbe.TryFindGroundBelow((origin, depth), GroundAt(StanceHeight), out Vector2 point, out Vector2 normal)) {
                continue;
            }
            bool isSloped = SlopeMath.IsSloped(normal, slope.maxSlopeAngle);
            sum = (sum.height + point.y, sum.gradient + (isSloped ? SlopeMath.Gradient(normal) : 0f), sum.sloped || isSloped, sum.hits + 1);
        }
        return sum.hits == 3 ? new TerrainSample(sum.height / 3f, sum.gradient / 3f, sum.sloped) : default;
    }

    // Each foot turns to lie along the ground it stands on or is stepping onto (read once per foothold; LegStepper turns it).
    private void AlignAnklesToGround() {
        ankleGroundSpots.front = AlignAnkle(frontLeg, ankleGroundSpots.front);
        ankleGroundSpots.back = AlignAnkle(backLeg, ankleGroundSpots.back);
    }

    private Vector2 AlignAnkle(LegStepper leg, Vector2 readAt) {
        leg.AnkleBlend = (slope.ankleAlignWeight, slope.ankleBlendTime);
        if (leg.FootTarget == readAt) {
            return readAt;
        }
        Vector2 origin = leg.FootTarget + Vector2.up * AnkleProbeHeight;
        bool found = FootholdProbe.TryFindGroundBelow((origin, 2f * AnkleProbeHeight), GroundAt(leg.FootTarget.y), out _, out Vector2 normal);
        leg.GroundNormal = found && SlopeMath.IsWalkable(normal, slope.maxSlopeAngle) ? normal : Vector2.up;
        return leg.FootTarget;
    }

    private readonly struct TerrainSample {
        public TerrainSample(float height, float gradient, bool isSloped) {
            Height = height;
            Gradient = gradient;
            IsSloped = isSloped;
            Found = true;
        }

        public float Height { get; }
        public float Gradient { get; }
        public bool IsSloped { get; }
        public bool Found { get; }
    }

    private float LowestPhase => GaitWaves.LowestPhase((CurrentGait.dutyFactor, CurrentGait.lowAfterContact));

    private float HighestPelvisOver((LegStepper leg, float pelvisX) support) {
        if (!support.leg.IsGrounded) {
            return float.PositiveInfinity;
        }
        Vector2 hipOffset = support.leg.HipPosition - (Vector2)transform.position;
        float reach = support.leg.LegLength * body.legExtension;
        return GaitPose.HighestPelvisY((support.leg.FootTarget, support.pelvisX), (hipOffset, reach));
    }
}
