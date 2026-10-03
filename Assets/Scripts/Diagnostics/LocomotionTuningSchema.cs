using System;
using System.Collections.Generic;

// Every tunable locomotion value the runtime panel offers, by section (one per tab): what it is called, its unit,
// a range that makes sense for it, and the preset field it reads and writes. Declarative, so the panel needs no
// knowledge of the settings and the sections can be tested.
public static class LocomotionTuningSchema {
    public sealed class Section {
        public Section(string title, IReadOnlyList<TuningParameter> parameters) {
            Title = title;
            Parameters = parameters;
        }

        public string Title { get; }
        public IReadOnlyList<TuningParameter> Parameters { get; }
    }

    private delegate void GaitWriter(ref GaitProfile gait, float value);

    private const string Units = "u";
    private const string UnitsPerSecond = "u/s";
    private const string UnitsPerSecondSquared = "u/s²";
    private const string Seconds = "s";
    private const string Hertz = "Hz";
    private const string Degrees = "°";
    private const string Share = "share";
    private const string Multiple = "×";

    public static IReadOnlyList<Section> Build() {
        return new[] {
            new Section("IDLE", Idle()),
            new Section("WALK", Gait(false)),
            new Section("RUN", Gait(true)),
            new Section("TRANSITIONS", Transitions()),
            new Section("BODY", Body()),
            new Section("UPPER BODY", UpperBody()),
        };
    }

    private static List<TuningParameter> Idle() {
        List<TuningParameter> parameters = new List<TuningParameter> {
            new TuningParameter(("Stance", "Stance half width", Units), (0f, 0.6f), (p => p.idle.stanceHalfWidth, (p, v) => p.idle.stanceHalfWidth = v)),
            new TuningParameter(("Stance", "Stance lead", Units), (-0.3f, 0.5f), (p => p.idle.stanceLead, (p, v) => p.idle.stanceLead = v)),
            new TuningParameter(("Stance", "Foot tolerance", Units), (0.02f, 0.6f), (p => p.idle.footTolerance, (p, v) => p.idle.footTolerance = v)),
            new TuningParameter(("Stance", "Settle step duration", Seconds), (0.05f, 1f), (p => p.idle.settleStepDuration, (p, v) => p.idle.settleStepDuration = v)),
            new TuningParameter(("Stance", "Settle lift height", Units), (0f, 0.8f), (p => p.idle.settleLiftHeight, (p, v) => p.idle.settleLiftHeight = v)),
            new TuningParameter(("Posture", "Idle lean", Degrees), (-10f, 20f), (p => p.posture.idleLean, (p, v) => p.posture.idleLean = v)),
            new TuningParameter(("Breathing", "Breath period", Seconds), (1f, 10f), (p => p.breathing.period, (p, v) => p.breathing.period = v)),
            new TuningParameter(("Breathing", "Breath pitch", Degrees), (0f, 10f), (p => p.breathing.pitch, (p, v) => p.breathing.pitch = v)),
            new TuningParameter(("Breathing", "Breath rise", Units), (0f, 0.3f), (p => p.breathing.rise, (p, v) => p.breathing.rise = v)),
        };
        parameters.AddRange(IdleThresholds("Thresholds"));
        return parameters;
    }

    private static IEnumerable<TuningParameter> IdleThresholds(string group) {
        yield return new TuningParameter((group, "Start speed (exit idle)", UnitsPerSecond), (0.05f, 2f), (p => p.transitions.startSpeed, (p, v) => p.transitions.startSpeed = v));
        yield return new TuningParameter((group, "Stop speed (enter idle)", UnitsPerSecond), (0.01f, 2f), (p => p.transitions.stopSpeed, (p, v) => p.transitions.stopSpeed = v));
    }

    private static List<TuningParameter> Gait(bool isRun) {
        return new List<TuningParameter> {
            GaitParameter(isRun, ((("Speed", "Speed", UnitsPerSecond), (0.5f, 15f)), (g => g.speed, (ref GaitProfile g, float v) => g.speed = v))),
            GaitParameter(isRun, ((("Speed", "Acceleration", UnitsPerSecondSquared), (1f, 60f)), (g => g.acceleration, (ref GaitProfile g, float v) => g.acceleration = v))),
            GaitParameter(isRun, ((("Speed", "Deceleration", UnitsPerSecondSquared), (1f, 80f)), (g => g.deceleration, (ref GaitProfile g, float v) => g.deceleration = v))),
            GaitParameter(isRun, ((("Timing", "Duty factor", Share), (0.2f, 0.8f)), (g => g.dutyFactor, (ref GaitProfile g, float v) => g.dutyFactor = v))),
            GaitParameter(isRun, ((("Timing", "Contact lead (share of stance ahead)", Share), (0.2f, 0.6f)), (g => g.contactLeadShare, (ref GaitProfile g, float v) => g.contactLeadShare = v))),
            GaitParameter(isRun, ((("Timing", "Step length per √speed", Multiple), (0.3f, 2f)), (g => g.stepLengthPerRootSpeed, (ref GaitProfile g, float v) => g.stepLengthPerRootSpeed = v))),
            GaitParameter(isRun, ((("Timing", "Min step length", Units), (0.1f, 2f)), (g => g.minStepLength, (ref GaitProfile g, float v) => g.minStepLength = v))),
            GaitParameter(isRun, ((("Timing", "Max step length", Units), (0.5f, 5f)), (g => g.maxStepLength, (ref GaitProfile g, float v) => g.maxStepLength = v))),
            GaitParameter(isRun, ((("Timing", "Min swing duration", Seconds), (0.1f, 1f)), (g => g.minSwingDuration, (ref GaitProfile g, float v) => g.minSwingDuration = v))),
            GaitParameter(isRun, ((("Timing", "Max swing duration", Seconds), (0.2f, 1.5f)), (g => g.maxSwingDuration, (ref GaitProfile g, float v) => g.maxSwingDuration = v))),
            GaitParameter(isRun, ((("Timing", "Lift height", Units), (0f, 1.2f)), (g => g.liftHeight, (ref GaitProfile g, float v) => g.liftHeight = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Pelvis drop", Units), (0f, 0.8f)), (g => g.pelvisDrop, (ref GaitProfile g, float v) => g.pelvisDrop = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Bob amplitude", Units), (0f, 0.4f)), (g => g.bobAmplitude, (ref GaitProfile g, float v) => g.bobAmplitude = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Low point after contact", "cycle"), (0f, 0.5f)), (g => g.lowAfterContact, (ref GaitProfile g, float v) => g.lowAfterContact = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Surge", Units), (0f, 0.3f)), (g => g.surge, (ref GaitProfile g, float v) => g.surge = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Plant impulse", "× impact"), (0f, 2f)), (g => g.plantImpulse, (ref GaitProfile g, float v) => g.plantImpulse = v))),
            GaitParameter(isRun, ((("Pelvis / body", "Push-off impulse", UnitsPerSecond), (0f, 5f)), (g => g.pushOffImpulse, (ref GaitProfile g, float v) => g.pushOffImpulse = v))),
            GaitParameter(isRun, ((("Upper body", "Base lean", Degrees), (-5f, 30f)), (g => g.lean, (ref GaitProfile g, float v) => g.lean = v))),
            GaitParameter(isRun, ((("Upper body", "Secondary pitch oscillation", Degrees), (0f, 10f)), (g => g.pitchOscillation, (ref GaitProfile g, float v) => g.pitchOscillation = v))),
            GaitParameter(isRun, ((("Upper body", "Arm swing", Degrees), (0f, 90f)), (g => g.armSwing, (ref GaitProfile g, float v) => g.armSwing = v))),
            GaitParameter(isRun, ((("Upper body", "Arm bend", Share), (0f, 1f)), (g => g.armBend, (ref GaitProfile g, float v) => g.armBend = v))),
        };
    }

    private static List<TuningParameter> Transitions() {
        List<TuningParameter> parameters = new List<TuningParameter> {
            new TuningParameter(("Walk / run blend", "Run blend start", "share of walk→run speeds"), (0f, 1f), (p => p.transitions.runBlendStart, (p, v) => p.transitions.runBlendStart = v)),
            new TuningParameter(("Walk / run blend", "Run blend end", "share of walk→run speeds"), (0f, 1f), (p => p.transitions.runBlendEnd, (p, v) => p.transitions.runBlendEnd = v)),
            new TuningParameter(("Walk / run blend", "Blend rate", "share/s"), (0.2f, 20f), (p => p.transitions.runBlendRate, (p, v) => p.transitions.runBlendRate = v)),
            new TuningParameter(("Walk / run blend", "Cadence transition (swing share change)", "share/cycle"), (0.02f, 1f), (p => p.transitions.swingShareChangePerCycle, (p, v) => p.transitions.swingShareChangePerCycle = v)),
            new TuningParameter(("Walk / run blend", "Setting-off planning share", "share of target"), (0f, 1f), (p => p.transitions.settingOffShare, (p, v) => p.transitions.settingOffShare = v)),
            new TuningParameter(("Overlap", "Walk lift overlap", Share), (0f, 1f), (p => p.transitions.walkLiftOverlap, (p, v) => p.transitions.walkLiftOverlap = v)),
            new TuningParameter(("Overlap", "Run lift overlap", Share), (0f, 1f), (p => p.transitions.runLiftOverlap, (p, v) => p.transitions.runLiftOverlap = v)),
            new TuningParameter(("Balance recovery", "Catch-up margin", Units), (0f, 1f), (p => p.balance.catchUpMargin, (p, v) => p.balance.catchUpMargin = v)),
            new TuningParameter(("Balance recovery", "Catch-up gain", "×/u"), (0f, 10f), (p => p.balance.catchUpGain, (p, v) => p.balance.catchUpGain = v)),
            new TuningParameter(("Balance recovery", "Max catch-up", Multiple), (1f, 3f), (p => p.balance.maxCatchUp, (p, v) => p.balance.maxCatchUp = v)),
            new TuningParameter(("Balance recovery", "Emergency step margin", Units), (0.1f, 3f), (p => p.balance.emergencyMargin, (p, v) => p.balance.emergencyMargin = v)),
            new TuningParameter(("Balance recovery", "Emergency cooldown", Seconds), (0f, 2f), (p => p.balance.emergencyCooldown, (p, v) => p.balance.emergencyCooldown = v)),
            new TuningParameter(("Posture response", "Acceleration lean", "°/(u/s²)"), (0f, 3f), (p => p.posture.leanPerAcceleration, (p, v) => p.posture.leanPerAcceleration = v)),
            new TuningParameter(("Posture response", "Max acceleration lean", Degrees), (0f, 30f), (p => p.posture.maxAccelerationLean, (p, v) => p.posture.maxAccelerationLean = v)),
            new TuningParameter(("Posture response", "Posture blend speed", Hertz), (0.2f, 8f), (p => p.posture.spring.frequency, (p, v) => p.posture.spring.frequency = v)),
        };
        parameters.AddRange(IdleThresholds("Idle hysteresis"));
        return parameters;
    }

    private static List<TuningParameter> Body() {
        List<TuningParameter> parameters = new List<TuningParameter> {
            new TuningParameter(("Height and reach", "Body height", Units), (1.5f, 3.5f), (p => p.bodyHeight, (p, v) => p.bodyHeight = v)),
            new TuningParameter(("Height and reach", "Leg extension", Share), (0.8f, 1f), (p => p.body.legExtension, (p, v) => p.body.legExtension = v)),
            new TuningParameter(("Height and reach", "Max reach drop", Units), (0f, 1f), (p => p.body.maxReachDrop, (p, v) => p.body.maxReachDrop = v)),
            new TuningParameter(("Height and reach", "Max plant impulse", UnitsPerSecond), (0f, 5f), (p => p.body.maxPlantImpulse, (p, v) => p.body.maxPlantImpulse = v)),
            new TuningParameter(("Pelvis over support", "Support coupling", Share), (0f, 1f), (p => p.body.supportCoupling, (p, v) => p.body.supportCoupling = v)),
            new TuningParameter(("Pelvis over support", "Max support offset", Units), (0f, 1f), (p => p.body.maxSupportOffset, (p, v) => p.body.maxSupportOffset = v)),
        };
        parameters.AddRange(Spring(("Horizontal spring", (p => p.body.horizontalSpring, (p, s) => p.body.horizontalSpring = s))));
        parameters.AddRange(Spring(("Vertical spring", (p => p.body.verticalSpring, (p, s) => p.body.verticalSpring = s))));
        return parameters;
    }

    private static List<TuningParameter> UpperBody() {
        List<TuningParameter> parameters = new List<TuningParameter> {
            GaitParameter(false, ((("Lean", "Walk base lean", Degrees), (-5f, 30f)), (g => g.lean, (ref GaitProfile g, float v) => g.lean = v))),
            GaitParameter(true, ((("Lean", "Run base lean", Degrees), (-5f, 30f)), (g => g.lean, (ref GaitProfile g, float v) => g.lean = v))),
            GaitParameter(false, ((("Lean", "Walk secondary pitch", Degrees), (0f, 10f)), (g => g.pitchOscillation, (ref GaitProfile g, float v) => g.pitchOscillation = v))),
            GaitParameter(true, ((("Lean", "Run secondary pitch", Degrees), (0f, 10f)), (g => g.pitchOscillation, (ref GaitProfile g, float v) => g.pitchOscillation = v))),
            new TuningParameter(("Lean", "Acceleration lean", "°/(u/s²)"), (0f, 3f), (p => p.posture.leanPerAcceleration, (p, v) => p.posture.leanPerAcceleration = v)),
            new TuningParameter(("Lean", "Max acceleration lean", Degrees), (0f, 30f), (p => p.posture.maxAccelerationLean, (p, v) => p.posture.maxAccelerationLean = v)),
            new TuningParameter(("Lean", "Max total lean", Degrees), (0f, 45f), (p => p.posture.maxTotalLean, (p, v) => p.posture.maxTotalLean = v)),
            new TuningParameter(("Head", "Head stabilization", Share), (0f, 1f), (p => p.head.stabilization, (p, v) => p.head.stabilization = v)),
            new TuningParameter(("Head", "Stabilization fade", Seconds), (0.01f, 1f), (p => p.head.stabilizationFade, (p, v) => p.head.stabilizationFade = v)),
            GaitParameter(false, ((("Arms", "Walk arm swing", Degrees), (0f, 90f)), (g => g.armSwing, (ref GaitProfile g, float v) => g.armSwing = v))),
            GaitParameter(true, ((("Arms", "Run arm swing", Degrees), (0f, 90f)), (g => g.armSwing, (ref GaitProfile g, float v) => g.armSwing = v))),
            new TuningParameter(("Arms", "Swing lag", "cycle"), (0f, 0.25f), (p => p.arms.swingLag, (p, v) => p.arms.swingLag = v)),
            new TuningParameter(("Arms", "Forward elbow flex", Share), (0f, 1f), (p => p.arms.forwardElbowFlex, (p, v) => p.arms.forwardElbowFlex = v)),
        };
        parameters.AddRange(Spring(("Posture spring", (p => p.posture.spring, (p, s) => p.posture.spring = s))));
        parameters.AddRange(Spring(("Chest spring", (p => p.chestSpring, (p, s) => p.chestSpring = s))));
        parameters.AddRange(Spring(("Head spring", (p => p.head.poseSpring, (p, s) => p.head.poseSpring = s))));
        parameters.AddRange(Spring(("Arm spring", (p => p.arms.spring, (p, s) => p.arms.spring = s))));
        return parameters;
    }

    // A walk or run profile value, read from and written to the profile the section edits.
    private static TuningParameter GaitParameter(bool isRun, (((string group, string label, string unit) display, (float min, float max) range) shown, (Func<GaitProfile, float> read, GaitWriter write) field) parameter) {
        return new TuningParameter(parameter.shown.display, parameter.shown.range, (
            preset => parameter.field.read(isRun ? preset.run : preset.walk),
            (preset, value) => WriteGait((preset, isRun), (parameter.field.write, value))));
    }

    private static void WriteGait((LocomotionPreset preset, bool isRun) target, (GaitWriter write, float value) change) {
        if (target.isRun) {
            change.write(ref target.preset.run, change.value);
        }
        else {
            change.write(ref target.preset.walk, change.value);
        }
    }

    // A spring's frequency, damping and response.
    private static IEnumerable<TuningParameter> Spring((string group, (Func<LocomotionPreset, SpringSettings> read, Action<LocomotionPreset, SpringSettings> write) access) spring) {
        yield return new TuningParameter((spring.group, "Frequency", Hertz), (0.2f, 12f), (
            p => spring.access.read(p).frequency,
            (p, v) => spring.access.write(p, new SpringSettings(v, spring.access.read(p).damping, spring.access.read(p).response))));
        yield return new TuningParameter((spring.group, "Damping", "ratio"), (0.05f, 2f), (
            p => spring.access.read(p).damping,
            (p, v) => spring.access.write(p, new SpringSettings(spring.access.read(p).frequency, v, spring.access.read(p).response))));
        yield return new TuningParameter((spring.group, "Response", Multiple), (-2f, 3f), (
            p => spring.access.read(p).response,
            (p, v) => spring.access.write(p, new SpringSettings(spring.access.read(p).frequency, spring.access.read(p).damping, v))));
    }
}
