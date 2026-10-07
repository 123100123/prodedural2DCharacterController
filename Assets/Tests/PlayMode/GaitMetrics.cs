using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Gait measurements over one time window of a recording.
public sealed class GaitMetrics {
    // A frame-to-frame phase change this far from what the distance travelled explains is a jump (a resync or prime).
    private const float PhaseJumpTolerance = 0.06f;
    // Below this speed the gait may be starting: priming the phase for the first step is not a jump.
    private const float MovingSpeed = 0.6f;
    // Lifts further apart than this are separated by a standing pause; either foot may start again after one.
    private const float RhythmGap = 0.8f;

    private readonly List<GaitRecorder.Sample> samples;
    private readonly List<GaitRecorder.FootEvent> events;

    public GaitMetrics(GaitRecorder recorder, (float start, float end) window) {
        Window = window;
        samples = recorder.Samples.Where(sample => sample.Time >= window.start && sample.Time <= window.end).ToList();
        events = recorder.FootEvents.Where(footEvent => footEvent.Time >= window.start && footEvent.Time <= window.end).ToList();
    }

    public (float start, float end) Window { get; }

    public int LiftCount => Lifts.Count;

    // Consecutive lifts by the same leg within one rhythm: a limp or a repeated step.
    public int RepeatedLegLifts {
        get {
            List<GaitRecorder.FootEvent> lifts = Lifts;
            int repeats = 0;
            for (int index = 1; index < lifts.Count; index++) {
                bool sameLeg = lifts[index].Front == lifts[index - 1].Front;
                bool sameRhythm = lifts[index].Time - lifts[index - 1].Time < RhythmGap;
                repeats += sameLeg && sameRhythm ? 1 : 0;
            }
            return repeats;
        }
    }

    // A limp makes opposite contact intervals alternate short and long, while a cadence change only drifts them: the
    // largest gap between an interval and the mean of its neighbours, as a share of that mean. 0 is perfectly even.
    public float WorstContactAsymmetry {
        get {
            List<float> intervals = AlternatingContactIntervals();
            float worst = 0f;
            for (int index = 1; index < intervals.Count - 1; index++) {
                float neighbours = (intervals[index - 1] + intervals[index + 1]) / 2f;
                worst = Mathf.Max(worst, Mathf.Abs(intervals[index] - neighbours) / neighbours);
            }
            return worst;
        }
    }

    public float MeanContactInterval {
        get {
            List<float> intervals = AlternatingContactIntervals();
            return intervals.Count == 0 ? 0f : intervals.Average();
        }
    }

    public int PhaseJumps {
        get {
            int jumps = 0;
            for (int index = 1; index < samples.Count; index++) {
                GaitRecorder.Sample previous = samples[index - 1];
                GaitRecorder.Sample current = samples[index];
                if (!previous.IsDrivingGait || !current.IsDrivingGait || Mathf.Abs(previous.GroundSpeed) < MovingSpeed) {
                    continue;
                }
                float advance = Mathf.Repeat(current.Phase - previous.Phase + 0.5f, 1f) - 0.5f;
                jumps += Mathf.Abs(advance - current.ExpectedPhaseAdvance) > PhaseJumpTolerance ? 1 : 0;
            }
            return jumps;
        }
    }

    public float MeanLean => samples.Count == 0 ? 0f : samples.Average(sample => sample.BodyLineLean);
    public float MeanHeadAngle => samples.Count == 0 ? 0f : samples.Average(sample => sample.HeadAngle);
    // Frames the grounded gait was not driving (in the air, landing, climbing).
    public int UngroundedFrames => samples.Count(sample => !sample.IsDrivingGait);
    public bool EndsGrounded => samples.Count > 0 && samples[samples.Count - 1].IsDrivingGait;
    public float MeanBodyAboveStance => samples.Count == 0 ? 0f : samples.Average(sample => sample.BodyAboveStance);
    public float HeadAngleDeviation => StandardDeviation(samples.Select(sample => sample.HeadAngle));
    public float ChestAngleDeviation => StandardDeviation(samples.Select(sample => sample.ChestAngle));
    public float FlightShare => samples.Count == 0 ? 0f : samples.Count(sample => !sample.FrontGrounded && !sample.BackGrounded) / (float)samples.Count;
    public float WorstPlantedFootGap => samples.Count == 0 ? 0f : samples.Max(sample => sample.PlantedFootGap);
    public float MeanSpeed => samples.Count == 0 ? 0f : samples.Average(sample => Mathf.Abs(sample.GroundSpeed));
    // Mean distance of the pelvis ahead of its support foot around mid-stance, in units: 0 is right
    // over the foot, as a body travelling through its legs should be.
    public float MidStancePelvisOffset {
        get {
            List<GaitRecorder.Sample> midStance = samples.Where(sample => sample.SupportProgress >= 0.4f && sample.SupportProgress <= 0.6f).ToList();
            return midStance.Count == 0 ? 0f : midStance.Average(sample => sample.PelvisOverSupport);
        }
    }
    public float WorstSupportExtension => SupportSamples.Count == 0 ? 0f : SupportSamples.Max(sample => sample.SupportExtension);
    public float MeanSupportExtension => SupportSamples.Count == 0 ? 0f : SupportSamples.Average(sample => sample.SupportExtension);
    public float MeanCatchUp => samples.Count == 0 ? 1f : samples.Average(sample => sample.CatchUp);
    public float MaxCatchUp => samples.Count == 0 ? 1f : samples.Max(sample => sample.CatchUp);
    // Largest hip-to-foot distance (share of the leg) and horizontal lag of a planted foot behind its hip.
    public float WorstTrailingExtension => Worst(sample => sample.TrailingExtension);
    public float WorstTrailingLag => Worst(sample => sample.TrailingLag);
    // Mean distance between consecutive plants of opposite feet (a step) and of the same foot (a stride), and the mean time
    // between plants of the same foot (the cycle period).
    public float MeanStepLength => MeanOf(PlantSpacings(false).Select(spacing => spacing.distance));
    public float MeanStrideLength => MeanOf(PlantSpacings(true).Select(spacing => spacing.distance));
    public float MeanCyclePeriod => MeanOf(PlantSpacings(true).Select(spacing => spacing.time));
    // Largest frame-to-frame movement of a foot planted in both frames: a planted foot never slides or snaps.
    public float WorstPlantedFootJump {
        get {
            float worst = 0f;
            for (int index = 1; index < samples.Count; index++) {
                GaitRecorder.Sample previous = samples[index - 1];
                GaitRecorder.Sample current = samples[index];
                if (!previous.IsDrivingGait || !current.IsDrivingGait) {
                    continue;
                }
                worst = previous.FrontGrounded && current.FrontGrounded ? Mathf.Max(worst, Vector2.Distance(previous.FrontFoot, current.FrontFoot)) : worst;
                worst = previous.BackGrounded && current.BackGrounded ? Mathf.Max(worst, Vector2.Distance(previous.BackFoot, current.BackFoot)) : worst;
            }
            return worst;
        }
    }
    public int BalanceSteps => CountOver(sample => sample.BalanceSteps);
    public int PhaseResyncs => CountOver(sample => sample.PhaseResyncs);
    public int FootholdFailures => CountOver(sample => sample.FootholdFailures);

    public string Describe(string label) {
        return $"{label,-28} t=[{Window.start:F2},{Window.end:F2}] speed={MeanSpeed:F2} lifts={LiftCount} repeats={RepeatedLegLifts} " +
               $"contactAsym={WorstContactAsymmetry:F2} contactInterval={MeanContactInterval:F3} phaseJumps={PhaseJumps} " +
               $"lean={MeanLean:F1} bodyAboveStance={MeanBodyAboveStance:F2} flight={FlightShare:F2} " +
               $"headDev={HeadAngleDeviation:F2} chestDev={ChestAngleDeviation:F2} footGap={WorstPlantedFootGap:F3} " +
               $"balanceSteps={BalanceSteps} resyncs={PhaseResyncs} footholdFailures={FootholdFailures} " +
               $"midStancePelvis={MidStancePelvisOffset:F2} supportExt={MeanSupportExtension:F2}/{WorstSupportExtension:F2} catchUp={MeanCatchUp:F2}" +
               (RepeatedLegLifts > 0 ? $" lifts: {LiftSequence}" : "");
    }

    private float Worst(System.Func<GaitRecorder.Sample, float> value) {
        List<float> values = samples.Select(value).Where(found => !float.IsNaN(found)).ToList();
        return values.Count == 0 ? 0f : values.Max();
    }

    private static float MeanOf(IEnumerable<float> values) {
        List<float> list = values.ToList();
        return list.Count == 0 ? 0f : list.Average();
    }

    // Horizontal distance and time between each plant and the next plant of the other foot (sameFoot false) or of the
    // same foot (true).
    private List<(float distance, float time)> PlantSpacings(bool sameFoot) {
        List<GaitRecorder.FootEvent> plants = events.Where(footEvent => !footEvent.IsLift).ToList();
        List<(float distance, float time)> spacings = new List<(float distance, float time)>();
        for (int index = 1; index < plants.Count; index++) {
            for (int earlier = index - 1; earlier >= 0; earlier--) {
                if ((plants[earlier].Front == plants[index].Front) == sameFoot) {
                    spacings.Add((Mathf.Abs(plants[index].Foot.x - plants[earlier].Foot.x), plants[index].Time - plants[earlier].Time));
                    break;
                }
            }
        }
        return spacings;
    }

    private List<GaitRecorder.Sample> SupportSamples => samples.Where(sample => !float.IsNaN(sample.SupportExtension)).ToList();

    private string LiftSequence => string.Join(" ", Lifts.Select(lift => $"{(lift.Front ? "F" : "B")}{lift.Time:F2}"));

    // How much a running counter grew over the window.
    private int CountOver(System.Func<GaitRecorder.Sample, int> counter) {
        return samples.Count == 0 ? 0 : counter(samples[samples.Count - 1]) - counter(samples[0]);
    }

    private List<GaitRecorder.FootEvent> Lifts => events.Where(footEvent => footEvent.IsLift).ToList();

    // Time between each plant and the next plant of the other foot.
    private List<float> AlternatingContactIntervals() {
        List<GaitRecorder.FootEvent> plants = events.Where(footEvent => !footEvent.IsLift).ToList();
        List<float> intervals = new List<float>();
        for (int index = 1; index < plants.Count; index++) {
            if (plants[index].Front != plants[index - 1].Front) {
                intervals.Add(plants[index].Time - plants[index - 1].Time);
            }
        }
        return intervals;
    }

    private static float StandardDeviation(IEnumerable<float> values) {
        List<float> list = values.ToList();
        if (list.Count < 2) {
            return 0f;
        }
        float mean = list.Average();
        return Mathf.Sqrt(list.Sum(value => (value - mean) * (value - mean)) / list.Count);
    }
}
