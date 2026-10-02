using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Gait measurements over one time window of a recording.
public sealed class GaitMetrics {
    // A frame-to-frame phase change this far from what the distance travelled explains is a jump (a resync or prime).
    private const float PhaseJumpTolerance = 0.06f;
    // Below this speed the gait may be starting: priming the phase for the first step is not a jump.
    private const float MovingSpeed = 0.6f;

    private readonly List<GaitRecorder.Sample> samples;
    private readonly List<GaitRecorder.FootEvent> events;

    public GaitMetrics(GaitRecorder recorder, (float start, float end) window) {
        Window = window;
        samples = recorder.Samples.Where(sample => sample.Time >= window.start && sample.Time <= window.end).ToList();
        events = recorder.FootEvents.Where(footEvent => footEvent.Time >= window.start && footEvent.Time <= window.end).ToList();
    }

    public (float start, float end) Window { get; }

    public int LiftCount => Lifts.Count;

    // Consecutive lifts by the same leg: a limp or a repeated step.
    public int RepeatedLegLifts {
        get {
            List<GaitRecorder.FootEvent> lifts = Lifts;
            int repeats = 0;
            for (int index = 1; index < lifts.Count; index++) {
                repeats += lifts[index].Front == lifts[index - 1].Front ? 1 : 0;
            }
            return repeats;
        }
    }

    // Largest difference between two consecutive contact intervals, as a share of their mean: 0 is perfectly even.
    public float WorstContactAsymmetry {
        get {
            List<float> intervals = AlternatingContactIntervals();
            float worst = 0f;
            for (int index = 1; index < intervals.Count; index++) {
                float mean = (intervals[index] + intervals[index - 1]) / 2f;
                worst = Mathf.Max(worst, Mathf.Abs(intervals[index] - intervals[index - 1]) / mean);
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
    public float MeanBodyAboveStance => samples.Count == 0 ? 0f : samples.Average(sample => sample.BodyAboveStance);
    public float HeadAngleDeviation => StandardDeviation(samples.Select(sample => sample.HeadAngle));
    public float ChestAngleDeviation => StandardDeviation(samples.Select(sample => sample.ChestAngle));
    public float FlightShare => samples.Count == 0 ? 0f : samples.Count(sample => !sample.FrontGrounded && !sample.BackGrounded) / (float)samples.Count;
    public float WorstPlantedFootGap => samples.Count == 0 ? 0f : samples.Max(sample => sample.PlantedFootGap);
    public float MeanSpeed => samples.Count == 0 ? 0f : samples.Average(sample => Mathf.Abs(sample.GroundSpeed));

    public string Describe(string label) {
        return $"{label,-28} t=[{Window.start:F2},{Window.end:F2}] speed={MeanSpeed:F2} lifts={LiftCount} repeats={RepeatedLegLifts} " +
               $"contactAsym={WorstContactAsymmetry:F2} contactInterval={MeanContactInterval:F3} phaseJumps={PhaseJumps} " +
               $"lean={MeanLean:F1} bodyAboveStance={MeanBodyAboveStance:F2} flight={FlightShare:F2} " +
               $"headDev={HeadAngleDeviation:F2} chestDev={ChestAngleDeviation:F2} footGap={WorstPlantedFootGap:F3}";
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
