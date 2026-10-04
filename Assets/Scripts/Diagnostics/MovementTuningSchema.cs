using System;
using System.Collections.Generic;
using System.Linq;
using static TuningUnits;
using Parameter = TuningParameter<MovementPreset>;
using Section = TuningSection<MovementPreset>;

// Every tunable movement value the runtime panel offers, by section (one per tab): what it is called, its unit,
// a range that makes sense for it, and the preset field it reads and writes. Declarative, so the panel needs no
// knowledge of the settings and the sections can be tested. Each preset field is offered in exactly one section.
public static class MovementTuningSchema {
    private delegate void GaitWriter(ref GaitProfile gait, float value);

    // Which of the preset's gait profiles a gait section edits.
    private enum GaitSlot { Walk, Run, Crouch }

    public static IReadOnlyList<Section> Build() {
        return new[] {
            new Section("IDLE", Idle()),
            new Section("WALK", Gait(GaitSlot.Walk)),
            new Section("RUN", Gait(GaitSlot.Run)),
            new Section("CROUCH", Crouch()),
            new Section("JUMP", Jump()),
            new Section("FALL", Fall()),
            new Section("TRANSITIONS", Transitions()),
            new Section("ADVANCED", Advanced()),
        };
    }

    public static TuningDomain<MovementPreset> CreateDomain(MovementTuning tuning) {
        PresetStore<MovementPreset> store = new PresetStore<MovementPreset>(("LocomotionPresets", MovementPreset.FromJson));
        return new TuningDomain<MovementPreset>(("Movement", Build()), (tuning.Capture, tuning.Apply, MovementPreset.CodeDefaults), store);
    }

    private static List<Parameter> Idle() {
        return new List<Parameter> {
            new Parameter(("Stance", "Stance half width", Units), (0f, 0.6f), (p => p.idle.stanceHalfWidth, (p, v) => p.idle.stanceHalfWidth = v)),
            new Parameter(("Stance", "Stance lead", Units), (-0.3f, 0.5f), (p => p.idle.stanceLead, (p, v) => p.idle.stanceLead = v)),
            new Parameter(("Stance", "Foot tolerance", Units), (0.02f, 0.6f), (p => p.idle.footTolerance, (p, v) => p.idle.footTolerance = v)),
            new Parameter(("Stance", "Settle step duration", Seconds), (0.05f, 1f), (p => p.idle.settleStepDuration, (p, v) => p.idle.settleStepDuration = v)),
            new Parameter(("Stance", "Settle lift height", Units), (0f, 0.8f), (p => p.idle.settleLiftHeight, (p, v) => p.idle.settleLiftHeight = v)),
            new Parameter(("Posture", "Idle lean", Degrees), (-10f, 20f), (p => p.posture.idleLean, (p, v) => p.posture.idleLean = v)),
            new Parameter(("Breathing", "Breath period", Seconds), (1f, 10f), (p => p.breathing.period, (p, v) => p.breathing.period = v)),
            new Parameter(("Breathing", "Breath pitch", Degrees), (0f, 10f), (p => p.breathing.pitch, (p, v) => p.breathing.pitch = v)),
            new Parameter(("Breathing", "Breath rise", Units), (0f, 0.3f), (p => p.breathing.rise, (p, v) => p.breathing.rise = v)),
        };
    }

    // The crouch's own settings first, then its gait profile (the same values as the walk and run sections).
    private static List<Parameter> Crouch() {
        List<Parameter> parameters = new List<Parameter> {
            new Parameter(("Height", "Crouch depth", Units), (0f, 1.3f), (p => p.crouch.depth, (p, v) => p.crouch.depth = v)),
            new Parameter(("Height", "Stance width scale", Multiple), (0.8f, 2f), (p => p.crouch.stanceWidthScale, (p, v) => p.crouch.stanceWidthScale = v)),
            new Parameter(("Posture", "Idle lean", Degrees), (-10f, 30f), (p => p.crouch.idleLean, (p, v) => p.crouch.idleLean = v)),
            new Parameter(("Posture", "Chest fold", Degrees), (0f, 30f), (p => p.crouch.chestFold, (p, v) => p.crouch.chestFold = v)),
            new Parameter(("Posture", "Arm bend held in", Share), (0f, 1f), (p => p.crouch.idleArmBend, (p, v) => p.crouch.idleArmBend = v)),
        };
        parameters.AddRange(Spring(("Crouch transition", (p => p.crouch.spring, (p, s) => p.crouch.spring = s))));
        parameters.AddRange(Gait(GaitSlot.Crouch));
        return parameters;
    }

    private static List<Parameter> Jump() {
        return new List<Parameter> {
            new Parameter(("Windup", "Windup duration (standing)", Seconds), (0f, 0.6f), (p => p.jump.windupDuration, (p, v) => p.jump.windupDuration = v)),
            new Parameter(("Windup", "Windup crouch depth", Units), (0f, 1.2f), (p => p.jump.crouchDepth, (p, v) => p.jump.crouchDepth = v)),
            new Parameter(("Windup", "Shortest windup", Seconds), (0f, 0.3f), (p => p.jump.minWindup, (p, v) => p.jump.minWindup = v)),
            new Parameter(("Windup", "Windup starts shrinking at", "share of run speed"), (0f, 1f), (p => p.jump.windupSpeedBand.x, (p, v) => p.jump.windupSpeedBand.x = v)),
            new Parameter(("Windup", "Windup gone at", "share of run speed"), (0f, 1f), (p => p.jump.windupSpeedBand.y, (p, v) => p.jump.windupSpeedBand.y = v)),
            new Parameter(("Launch", "Jump height (held)", Units), (0.5f, 6f), (p => p.jump.jumpHeight, (p, v) => p.jump.jumpHeight = v)),
            new Parameter(("Launch", "Crouch jump height", "× full height"), (0.3f, 1f), (p => p.jump.crouchJumpScale, (p, v) => p.jump.crouchJumpScale = v)),
            new Parameter(("Launch", "Hop height (tap)", Units), (0.1f, 3f), (p => p.jump.minHopHeight, (p, v) => p.jump.minHopHeight = v)),
            new Parameter(("Launch", "Hold for the full jump", Seconds), (0.02f, 0.6f), (p => p.jump.fullHoldTime, (p, v) => p.jump.fullHoldTime = v)),
            new Parameter(("Momentum", "Ground speed kept", Share), (0f, 1.5f), (p => p.jump.carry, (p, v) => p.jump.carry = v)),
            new Parameter(("Momentum", "Forward impulse at a full run", UnitsPerSecond), (0f, 4f), (p => p.jump.forwardImpulse, (p, v) => p.jump.forwardImpulse = v)),
            new Parameter(("Air control", "Air speed", UnitsPerSecond), (1f, 12f), (p => p.air.airSpeed, (p, v) => p.air.airSpeed = v)),
            new Parameter(("Air control", "Air acceleration", UnitsPerSecondSquared), (2f, 80f), (p => p.air.airAcceleration, (p, v) => p.air.airAcceleration = v)),
            new Parameter(("Air control", "Drag with no input", UnitsPerSecondSquared), (0f, 30f), (p => p.air.neutralDrag, (p, v) => p.air.neutralDrag = v)),
            new Parameter(("Air control", "Coyote time", Seconds), (0f, 0.4f), (p => p.air.coyoteTime, (p, v) => p.air.coyoteTime = v)),
            new Parameter(("Air control", "Jump buffer time", Seconds), (0f, 0.5f), (p => p.air.jumpBufferTime, (p, v) => p.air.jumpBufferTime = v)),
            new Parameter(("Pose blend", "Rising / falling at", UnitsPerSecond), (1f, 15f), (p => p.airPose.apexBand, (p, v) => p.airPose.apexBand = v)),
            new Parameter(("Pose blend", "Running pose starts at", "share of run speed"), (0f, 1f), (p => p.airPose.runningBand.x, (p, v) => p.airPose.runningBand.x = v)),
            new Parameter(("Pose blend", "Running pose complete at", "share of run speed"), (0f, 1f), (p => p.airPose.runningBand.y, (p, v) => p.airPose.runningBand.y = v)),
            new Parameter(("Pose blend", "Foot ease", "share/frame"), (0.02f, 1f), (p => p.air.legPoseEase, (p, v) => p.air.legPoseEase = v)),
            new Parameter(("Push-off", "Push-off duration", Seconds), (0f, 0.6f), (p => p.airPose.pushOffDuration, (p, v) => p.airPose.pushOffDuration = v)),
            new Parameter(("Push-off", "Trailing foot reaches back", Units), (-1.5f, 1f), (p => p.airPose.pushOffReach.x, (p, v) => p.airPose.pushOffReach.x = v)),
            new Parameter(("Push-off", "Trailing foot reaches down", Units), (-1.5f, 1f), (p => p.airPose.pushOffReach.y, (p, v) => p.airPose.pushOffReach.y = v)),
        }.Concat(PoseParameters("Rising pose (standing)", (p => p.airPose.ascentStanding, (p, pose) => p.airPose.ascentStanding = pose)))
         .Concat(PoseParameters("Rising pose (running)", (p => p.airPose.ascentRunning, (p, pose) => p.airPose.ascentRunning = pose)))
         .Concat(PoseParameters("Apex pose", (p => p.airPose.apex, (p, pose) => p.airPose.apex = pose)))
         .ToList();
    }

    private static List<Parameter> Fall() {
        List<Parameter> parameters = PoseParameters("Falling pose (standing)", (p => p.airPose.descentStanding, (p, pose) => p.airPose.descentStanding = pose))
            .Concat(PoseParameters("Falling pose (running)", (p => p.airPose.descentRunning, (p, pose) => p.airPose.descentRunning = pose)))
            .ToList();
        parameters.AddRange(new[] {
            new Parameter(("Prolonged fall: build-up", "Starts after", Seconds), (0f, 3f), (p => p.fallSecondary.onsetTime, (p, v) => p.fallSecondary.onsetTime = v)),
            new Parameter(("Prolonged fall: build-up", "Full strength after", Seconds), (0.1f, 6f), (p => p.fallSecondary.fullTime, (p, v) => p.fallSecondary.fullTime = v)),
            new Parameter(("Prolonged fall: build-up", "Fall speed adds from", UnitsPerSecond), (0f, 30f), (p => p.fallSecondary.speedBand.x, (p, v) => p.fallSecondary.speedBand.x = v)),
            new Parameter(("Prolonged fall: build-up", "Fall speed adds until", UnitsPerSecond), (0f, 40f), (p => p.fallSecondary.speedBand.y, (p, v) => p.fallSecondary.speedBand.y = v)),
            new Parameter(("Prolonged fall: build-up", "Strength of a slow long fall", Share), (0f, 1f), (p => p.fallSecondary.slowFallStrength, (p, v) => p.fallSecondary.slowFallStrength = v)),
            new Parameter(("Prolonged fall: arms", "Arm swing", Degrees), (0f, 120f), (p => p.fallSecondary.armAmplitude, (p, v) => p.fallSecondary.armAmplitude = v)),
            new Parameter(("Prolonged fall: arms", "Flail speed at the start", "cycles/s"), (0.1f, 6f), (p => p.fallSecondary.flailRate.x, (p, v) => p.fallSecondary.flailRate.x = v)),
            new Parameter(("Prolonged fall: arms", "Flail speed at full strength", "cycles/s"), (0.1f, 6f), (p => p.fallSecondary.flailRate.y, (p, v) => p.fallSecondary.flailRate.y = v)),
            new Parameter(("Prolonged fall: arms", "Flail speed wander", Share), (0f, 0.5f), (p => p.fallSecondary.rateWobble, (p, v) => p.fallSecondary.rateWobble = v)),
            new Parameter(("Prolonged fall: arms", "Wander rate", "cycles/s"), (0.02f, 2f), (p => p.fallSecondary.wobbleRate, (p, v) => p.fallSecondary.wobbleRate = v)),
            new Parameter(("Prolonged fall: arms", "Second wave", Share), (0f, 1f), (p => p.fallSecondary.secondWave, (p, v) => p.fallSecondary.secondWave = v)),
            new Parameter(("Prolonged fall: arms", "Back arm speed", Multiple), (0.5f, 2f), (p => p.fallSecondary.backArmRate, (p, v) => p.fallSecondary.backArmRate = v)),
            new Parameter(("Prolonged fall: arms", "Elbow flail", Share), (0f, 1f), (p => p.fallSecondary.elbowFlail, (p, v) => p.fallSecondary.elbowFlail = v)),
            new Parameter(("Prolonged fall: arms", "Arms lift with fall speed", Degrees), (0f, 80f), (p => p.fallSecondary.armLiftWithSpeed, (p, v) => p.fallSecondary.armLiftWithSpeed = v)),
            new Parameter(("Prolonged fall: arms", "Arms trail acceleration", "°/(u/s²)"), (0f, 12f), (p => p.fallSecondary.armLagPerAcceleration, (p, v) => p.fallSecondary.armLagPerAcceleration = v)),
            new Parameter(("Prolonged fall: arms", "Most the arms trail", Degrees), (0f, 80f), (p => p.fallSecondary.maxArmLag, (p, v) => p.fallSecondary.maxArmLag = v)),
            new Parameter(("Prolonged fall: legs and torso", "Foot drift forward", Units), (0f, 0.6f), (p => p.fallSecondary.legLoosen.x, (p, v) => p.fallSecondary.legLoosen.x = v)),
            new Parameter(("Prolonged fall: legs and torso", "Foot drift up and down", Units), (0f, 0.6f), (p => p.fallSecondary.legLoosen.y, (p, v) => p.fallSecondary.legLoosen.y = v)),
            new Parameter(("Prolonged fall: legs and torso", "Forward pitch", Degrees), (0f, 25f), (p => p.fallSecondary.forwardPitch, (p, v) => p.fallSecondary.forwardPitch = v)),
            new Parameter(("Prolonged fall: landing and aiming", "Damped on approaching the ground", Share), (0f, 1f), (p => p.fallSecondary.landingDamping, (p, v) => p.fallSecondary.landingDamping = v)),
            new Parameter(("Prolonged fall: landing and aiming", "Aim blend time", Seconds), (0.02f, 1f), (p => p.fallSecondary.aimBlendTime, (p, v) => p.fallSecondary.aimBlendTime = v)),
        });
        return parameters;
    }

    // The nine values of one airborne pose: the feet, the arms, the elbows and the torso.
    private static IEnumerable<Parameter> PoseParameters(string group, (Func<MovementPreset, AirPose> read, Action<MovementPreset, AirPose> write) pose) {
        yield return PoseValue((group, "Lead foot forward", Units), (-1f, 2.5f), (pose, (ref AirPose a, float v) => a.leadFoot.x = v, a => a.leadFoot.x));
        yield return PoseValue((group, "Lead foot height", Units), (-3f, 0f), (pose, (ref AirPose a, float v) => a.leadFoot.y = v, a => a.leadFoot.y));
        yield return PoseValue((group, "Trail foot forward", Units), (-2.5f, 1.5f), (pose, (ref AirPose a, float v) => a.trailFoot.x = v, a => a.trailFoot.x));
        yield return PoseValue((group, "Trail foot height", Units), (-3f, 0f), (pose, (ref AirPose a, float v) => a.trailFoot.y = v, a => a.trailFoot.y));
        yield return PoseValue((group, "Front arm raise", Degrees), (-90f, 180f), (pose, (ref AirPose a, float v) => a.armFront = v, a => a.armFront));
        yield return PoseValue((group, "Back arm raise", Degrees), (-90f, 180f), (pose, (ref AirPose a, float v) => a.armBack = v, a => a.armBack));
        yield return PoseValue((group, "Elbow bend", Share), (0f, 1f), (pose, (ref AirPose a, float v) => a.elbowBend = v, a => a.elbowBend));
        yield return PoseValue((group, "Lean", Degrees), (-30f, 40f), (pose, (ref AirPose a, float v) => a.lean = v, a => a.lean));
        yield return PoseValue((group, "Chest pitch", Degrees), (-30f, 40f), (pose, (ref AirPose a, float v) => a.chestPitch = v, a => a.chestPitch));
    }

    private delegate void PoseWriter(ref AirPose pose, float value);

    private static Parameter PoseValue((string group, string label, string unit) display, (float min, float max) range, ((Func<MovementPreset, AirPose> read, Action<MovementPreset, AirPose> write) pose, PoseWriter set, Func<AirPose, float> get) field) {
        return new Parameter(display, range, (
            preset => field.get(field.pose.read(preset)),
            (preset, value) => {
                AirPose pose = field.pose.read(preset);
                field.set(ref pose, value);
                field.pose.write(preset, pose);
            }));
    }

    private static List<Parameter> Gait(GaitSlot slot) {
        return new List<Parameter> {
            GaitParameter(slot, ((("Speed", "Speed", UnitsPerSecond), (0.5f, 15f)), (g => g.speed, (ref GaitProfile g, float v) => g.speed = v))),
            GaitParameter(slot, ((("Speed", "Acceleration", UnitsPerSecondSquared), (1f, 60f)), (g => g.acceleration, (ref GaitProfile g, float v) => g.acceleration = v))),
            GaitParameter(slot, ((("Speed", "Deceleration", UnitsPerSecondSquared), (1f, 80f)), (g => g.deceleration, (ref GaitProfile g, float v) => g.deceleration = v))),
            GaitParameter(slot, ((("Timing", "Duty factor", Share), (0.2f, 0.8f)), (g => g.dutyFactor, (ref GaitProfile g, float v) => g.dutyFactor = v))),
            GaitParameter(slot, ((("Timing", "Contact lead (share of stance ahead)", Share), (0.2f, 0.6f)), (g => g.contactLeadShare, (ref GaitProfile g, float v) => g.contactLeadShare = v))),
            GaitParameter(slot, ((("Timing", "Step length per √speed", Multiple), (0.3f, 2f)), (g => g.stepLengthPerRootSpeed, (ref GaitProfile g, float v) => g.stepLengthPerRootSpeed = v))),
            GaitParameter(slot, ((("Timing", "Min step length", Units), (0.1f, 2f)), (g => g.minStepLength, (ref GaitProfile g, float v) => g.minStepLength = v))),
            GaitParameter(slot, ((("Timing", "Max step length", Units), (0.5f, 5f)), (g => g.maxStepLength, (ref GaitProfile g, float v) => g.maxStepLength = v))),
            GaitParameter(slot, ((("Timing", "Min swing duration", Seconds), (0.1f, 1f)), (g => g.minSwingDuration, (ref GaitProfile g, float v) => g.minSwingDuration = v))),
            GaitParameter(slot, ((("Timing", "Max swing duration", Seconds), (0.2f, 1.5f)), (g => g.maxSwingDuration, (ref GaitProfile g, float v) => g.maxSwingDuration = v))),
            GaitParameter(slot, ((("Timing", "Lift height", Units), (0f, 1.2f)), (g => g.liftHeight, (ref GaitProfile g, float v) => g.liftHeight = v))),
            GaitParameter(slot, ((("Pelvis / body", "Pelvis drop", Units), (0f, 0.8f)), (g => g.pelvisDrop, (ref GaitProfile g, float v) => g.pelvisDrop = v))),
            GaitParameter(slot, ((("Pelvis / body", "Bob amplitude", Units), (0f, 0.4f)), (g => g.bobAmplitude, (ref GaitProfile g, float v) => g.bobAmplitude = v))),
            GaitParameter(slot, ((("Pelvis / body", "Low point after contact", "cycle"), (0f, 0.5f)), (g => g.lowAfterContact, (ref GaitProfile g, float v) => g.lowAfterContact = v))),
            GaitParameter(slot, ((("Pelvis / body", "Surge", Units), (0f, 0.3f)), (g => g.surge, (ref GaitProfile g, float v) => g.surge = v))),
            GaitParameter(slot, ((("Pelvis / body", "Plant impulse", "× impact"), (0f, 2f)), (g => g.plantImpulse, (ref GaitProfile g, float v) => g.plantImpulse = v))),
            GaitParameter(slot, ((("Pelvis / body", "Push-off impulse", UnitsPerSecond), (0f, 5f)), (g => g.pushOffImpulse, (ref GaitProfile g, float v) => g.pushOffImpulse = v))),
            GaitParameter(slot, ((("Upper body", "Base lean", Degrees), (-5f, 30f)), (g => g.lean, (ref GaitProfile g, float v) => g.lean = v))),
            GaitParameter(slot, ((("Upper body", "Secondary pitch oscillation", Degrees), (0f, 10f)), (g => g.pitchOscillation, (ref GaitProfile g, float v) => g.pitchOscillation = v))),
            GaitParameter(slot, ((("Upper body", "Arm swing", Degrees), (0f, 90f)), (g => g.armSwing, (ref GaitProfile g, float v) => g.armSwing = v))),
            GaitParameter(slot, ((("Upper body", "Arm bend", Share), (0f, 1f)), (g => g.armBend, (ref GaitProfile g, float v) => g.armBend = v))),
        };
    }

    private static List<Parameter> Transitions() {
        return new List<Parameter> {
            new Parameter(("Idle thresholds", "Start speed (exit idle)", UnitsPerSecond), (0.05f, 2f), (p => p.transitions.startSpeed, (p, v) => p.transitions.startSpeed = v)),
            new Parameter(("Idle thresholds", "Stop speed (enter idle)", UnitsPerSecond), (0.01f, 2f), (p => p.transitions.stopSpeed, (p, v) => p.transitions.stopSpeed = v)),
            new Parameter(("Walk / run blend", "Run blend start", "share of walk→run speeds"), (0f, 1f), (p => p.transitions.runBlendStart, (p, v) => p.transitions.runBlendStart = v)),
            new Parameter(("Walk / run blend", "Run blend end", "share of walk→run speeds"), (0f, 1f), (p => p.transitions.runBlendEnd, (p, v) => p.transitions.runBlendEnd = v)),
            new Parameter(("Walk / run blend", "Blend rate", "share/s"), (0.2f, 20f), (p => p.transitions.runBlendRate, (p, v) => p.transitions.runBlendRate = v)),
            new Parameter(("Walk / run blend", "Cadence transition (swing share change)", "share/cycle"), (0.02f, 1f), (p => p.transitions.swingShareChangePerCycle, (p, v) => p.transitions.swingShareChangePerCycle = v)),
            new Parameter(("Walk / run blend", "Setting-off planning share", "share of target"), (0f, 1f), (p => p.transitions.settingOffShare, (p, v) => p.transitions.settingOffShare = v)),
            new Parameter(("Overlap", "Walk lift overlap", Share), (0f, 1f), (p => p.transitions.walkLiftOverlap, (p, v) => p.transitions.walkLiftOverlap = v)),
            new Parameter(("Overlap", "Run lift overlap", Share), (0f, 1f), (p => p.transitions.runLiftOverlap, (p, v) => p.transitions.runLiftOverlap = v)),
            new Parameter(("Balance recovery", "Catch-up margin", Units), (0f, 1f), (p => p.balance.catchUpMargin, (p, v) => p.balance.catchUpMargin = v)),
            new Parameter(("Balance recovery", "Catch-up gain", "×/u"), (0f, 10f), (p => p.balance.catchUpGain, (p, v) => p.balance.catchUpGain = v)),
            new Parameter(("Balance recovery", "Max catch-up", Multiple), (1f, 3f), (p => p.balance.maxCatchUp, (p, v) => p.balance.maxCatchUp = v)),
            new Parameter(("Balance recovery", "Emergency step margin", Units), (0.1f, 3f), (p => p.balance.emergencyMargin, (p, v) => p.balance.emergencyMargin = v)),
            new Parameter(("Balance recovery", "Emergency cooldown", Seconds), (0f, 2f), (p => p.balance.emergencyCooldown, (p, v) => p.balance.emergencyCooldown = v)),
            new Parameter(("Posture blending", "Acceleration lean", "°/(u/s²)"), (0f, 3f), (p => p.posture.leanPerAcceleration, (p, v) => p.posture.leanPerAcceleration = v)),
            new Parameter(("Posture blending", "Max acceleration lean", Degrees), (0f, 30f), (p => p.posture.maxAccelerationLean, (p, v) => p.posture.maxAccelerationLean = v)),
            new Parameter(("Posture blending", "Posture blend speed", Hertz), (0.2f, 8f), (p => p.posture.spring.frequency, (p, v) => p.posture.spring.frequency = v)),
        };
    }

    // Deep-debugging values shared by every mode: the body's mass springs and reach, and the upper body's springs.
    private static List<Parameter> Advanced() {
        List<Parameter> parameters = new List<Parameter> {
            new Parameter(("Height and reach", "Body height", Units), (1.5f, 3.5f), (p => p.bodyHeight, (p, v) => p.bodyHeight = v)),
            new Parameter(("Height and reach", "Leg extension", Share), (0.8f, 1f), (p => p.body.legExtension, (p, v) => p.body.legExtension = v)),
            new Parameter(("Height and reach", "Max reach drop", Units), (0f, 1f), (p => p.body.maxReachDrop, (p, v) => p.body.maxReachDrop = v)),
            new Parameter(("Height and reach", "Max plant impulse", UnitsPerSecond), (0f, 5f), (p => p.body.maxPlantImpulse, (p, v) => p.body.maxPlantImpulse = v)),
            new Parameter(("Pelvis over support", "Support coupling", Share), (0f, 1f), (p => p.body.supportCoupling, (p, v) => p.body.supportCoupling = v)),
            new Parameter(("Pelvis over support", "Max support offset", Units), (0f, 1f), (p => p.body.maxSupportOffset, (p, v) => p.body.maxSupportOffset = v)),
        };
        parameters.AddRange(Spring(("Horizontal spring", (p => p.body.horizontalSpring, (p, s) => p.body.horizontalSpring = s))));
        parameters.AddRange(Spring(("Vertical spring", (p => p.body.verticalSpring, (p, s) => p.body.verticalSpring = s))));
        parameters.Add(new Parameter(("Posture", "Max total lean", Degrees), (0f, 45f), (p => p.posture.maxTotalLean, (p, v) => p.posture.maxTotalLean = v)));
        // The posture's frequency is the blend speed under TRANSITIONS.
        parameters.AddRange(Spring(("Posture spring", (p => p.posture.spring, (p, s) => p.posture.spring = s)), false));
        parameters.AddRange(Spring(("Chest spring", (p => p.chestSpring, (p, s) => p.chestSpring = s))));
        parameters.Add(new Parameter(("Head", "Head stabilization", Share), (0f, 1f), (p => p.head.stabilization, (p, v) => p.head.stabilization = v)));
        parameters.Add(new Parameter(("Head", "Stabilization fade", Seconds), (0.01f, 1f), (p => p.head.stabilizationFade, (p, v) => p.head.stabilizationFade = v)));
        parameters.AddRange(Spring(("Head spring", (p => p.head.poseSpring, (p, s) => p.head.poseSpring = s))));
        parameters.Add(new Parameter(("Arms", "Swing lag", "cycle"), (0f, 0.25f), (p => p.arms.swingLag, (p, v) => p.arms.swingLag = v)));
        parameters.Add(new Parameter(("Arms", "Forward elbow flex", Share), (0f, 1f), (p => p.arms.forwardElbowFlex, (p, v) => p.arms.forwardElbowFlex = v)));
        parameters.Add(new Parameter(("Arms", "Bent arm shortening", Share), (0f, 0.8f), (p => p.arms.bentArmShortening, (p, v) => p.arms.bentArmShortening = v)));
        parameters.Add(new Parameter(("Arms", "Bent arm forward angle", Degrees), (0f, 45f), (p => p.arms.bentArmForwardAngle, (p, v) => p.arms.bentArmForwardAngle = v)));
        parameters.AddRange(Spring(("Arm spring", (p => p.arms.spring, (p, s) => p.arms.spring = s))));
        return parameters;
    }

    // A walk or run profile value, read from and written to the profile the section edits.
    private static Parameter GaitParameter(GaitSlot slot, (((string group, string label, string unit) display, (float min, float max) range) shown, (Func<GaitProfile, float> read, GaitWriter write) field) parameter) {
        return new Parameter(parameter.shown.display, parameter.shown.range, (
            preset => parameter.field.read(ProfileOf(preset, slot)),
            (preset, value) => parameter.field.write(ref ProfileOf(preset, slot), value)));
    }

    private static ref GaitProfile ProfileOf(MovementPreset preset, GaitSlot slot) {
        switch (slot) {
            case GaitSlot.Run:
                return ref preset.run;
            case GaitSlot.Crouch:
                return ref preset.crouchGait;
            default:
                return ref preset.walk;
        }
    }

    private static IEnumerable<Parameter> Spring((string group, (Func<MovementPreset, SpringSettings> read, Action<MovementPreset, SpringSettings> write) access) spring, bool includeFrequency = true) {
        return TuningUnits.Spring(spring, includeFrequency);
    }
}
