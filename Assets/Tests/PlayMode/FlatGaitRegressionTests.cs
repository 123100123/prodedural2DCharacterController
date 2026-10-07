using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Pins the steady flat-ground walk and run as they were measured before the gait was bounded by leg reach on slopes
// (Phase 18): step and stride length, cycle period, the support leg's peak extension, the catch-up and the recovery
// counts. Flat ground sits at the reach limit by design, so any slope geometry leaking onto it shows up here.
public class FlatGaitRegressionTests : ScenarioFixture {
    private const float Tolerance = 0.03f;

    private readonly struct FlatGait {
        public FlatGait((float step, float stride, float cyclePeriod) timing, (float peakSupportExtension, float meanCatchUp) reach) {
            Step = timing.step;
            Stride = timing.stride;
            CyclePeriod = timing.cyclePeriod;
            PeakSupportExtension = reach.peakSupportExtension;
            MeanCatchUp = reach.meanCatchUp;
        }

        public float Step { get; }
        public float Stride { get; }
        public float CyclePeriod { get; }
        public float PeakSupportExtension { get; }
        public float MeanCatchUp { get; }
    }

    // Measured at 4abbd2c (before Phase 18) over the last three seconds of five seconds of steady input.
    private static readonly FlatGait Walk = new FlatGait((1.5750f, 3.1500f, 0.9000f), (0.9385f, 1f));
    private static readonly FlatGait Run = new FlatGait((2.7558f, 5.5125f, 0.6125f), (1.0017f, 1f));

    [UnityTest]
    public IEnumerator SteadyFlatGaitMatchesThePinnedValues([Values(false, true)] bool running) {
        yield return Hold(1f);
        Stage.Press(running ? new[] { Key.D, Key.LeftShift } : new[] { Key.D });
        yield return Hold(2f);
        int releasesBefore = Stage.Stride.Diagnostics.ExtensionReleases;
        yield return Hold(3f);
        int extensionReleases = Stage.Stride.Diagnostics.ExtensionReleases - releasesBefore;
        GaitMetrics steady = Measure(running ? "steady run" : "steady walk", Recorder.Now - 3f, Recorder.Now);
        Report($"step={steady.MeanStepLength:F4} stride={steady.MeanStrideLength:F4} cycle={steady.MeanCyclePeriod:F4} " +
               $"peakSupportExt={steady.WorstSupportExtension:F4} catchUp={steady.MeanCatchUp:F4}/{steady.MaxCatchUp:F4} " +
               $"trailingExt={steady.WorstTrailingExtension:F4} trailingLag={steady.WorstTrailingLag:F4} " +
               $"balanceSteps={steady.BalanceSteps} resyncs={steady.PhaseResyncs} extensionReleases={extensionReleases} footJump={steady.WorstPlantedFootJump:F4}");

        FlatGait pinned = running ? Run : Walk;
        AssertNear(steady.MeanStepLength, pinned.Step, "step length");
        AssertNear(steady.MeanStrideLength, pinned.Stride, "stride length");
        AssertNear(steady.MeanCyclePeriod, pinned.CyclePeriod, "cycle period (cadence)");
        AssertNear(steady.WorstSupportExtension, pinned.PeakSupportExtension, "peak support-leg extension");
        AssertNear(steady.MeanCatchUp, pinned.MeanCatchUp, "catch-up");
        Assert.That(steady.BalanceSteps, Is.Zero, "no balance steps");
        Assert.That(steady.PhaseResyncs, Is.Zero, "no phase resyncs");
        Assert.That(extensionReleases, Is.Zero, "no foot released at full extension");
        Assert.That(steady.WorstPlantedFootJump, Is.LessThan(1e-4f), "planted feet stay put");
    }

    private static void AssertNear(float measured, float pinned, string what) {
        Assert.That(measured, Is.EqualTo(pinned).Within(Tolerance * pinned), $"{what}: {measured:F4} against {pinned:F4} pinned");
    }
}
