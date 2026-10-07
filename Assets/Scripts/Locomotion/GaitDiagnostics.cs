// What the grounded gait did recently, kept for inspection (the debug panel): when each foot last planted and the
// intervals between opposite contacts, and when it last needed a balance step, a phase resync or found no foothold.
// Times are game time in seconds. Nothing in the gait reads these back.
public sealed class GaitDiagnostics {
    public float LastFrontPlant { get; private set; } = float.NegativeInfinity;
    public float LastBackPlant { get; private set; } = float.NegativeInfinity;
    public bool LastPlantWasFront { get; private set; }
    // Time from a front-foot contact to the next back-foot contact, and the other way round: equal in an even gait.
    public float FrontToBackContact { get; private set; }
    public float BackToFrontContact { get; private set; }

    public int BalanceSteps { get; private set; }
    public float LastBalanceStep { get; private set; } = float.NegativeInfinity;
    public int PhaseResyncs { get; private set; }
    public float LastPhaseResync { get; private set; } = float.NegativeInfinity;
    // Trailing feet released at full extension ahead of their cue (normal steps: neither balance steps nor resyncs).
    public int ExtensionReleases { get; private set; }
    public float LastExtensionRelease { get; private set; } = float.NegativeInfinity;
    public int FootholdFailures { get; private set; }
    public float LastFootholdFailure { get; private set; } = float.NegativeInfinity;
    // Cadence multiplier the support recovery applies at the moment (1 when the feet keep up).
    public float CatchUp { get; private set; } = 1f;

    public void RecordPlant(bool frontLeg, float time) {
        if (frontLeg) {
            BackToFrontContact = time - LastBackPlant;
            LastFrontPlant = time;
        }
        else {
            FrontToBackContact = time - LastFrontPlant;
            LastBackPlant = time;
        }
        LastPlantWasFront = frontLeg;
    }

    public void RecordBalanceStep(float time) {
        BalanceSteps++;
        LastBalanceStep = time;
    }

    public void RecordPhaseResync(float time) {
        PhaseResyncs++;
        LastPhaseResync = time;
    }

    public void RecordExtensionRelease(float time) {
        ExtensionReleases++;
        LastExtensionRelease = time;
    }

    public void RecordFootholdFailure(float time) {
        FootholdFailures++;
        LastFootholdFailure = time;
    }

    public void RecordCatchUp(float catchUp) => CatchUp = catchUp;
}
