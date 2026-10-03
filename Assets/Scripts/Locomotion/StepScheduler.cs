using UnityEngine;

// Decides when each leg lifts and how fast swings progress. Both legs share one gait clock, advanced by the distance
// travelled, and are cued half a cycle apart; a cued leg lifts as soon as it is planted and the other foot lets it (see
// SwingTiming.ReleaseProgress). Swings advance on the same clock through one shared swing share of the cycle, ramped
// steadily between walking and running, so a cued leg is planted by its cue at any cadence and opposite contacts stay
// evenly spaced through speed and gait changes. Motion starts and stops with hysteresis, and a restart continues the rhythm.
public sealed class StepScheduler {
    // A step this recent belongs to the rhythm a restart continues (a brief stop, a turn); an older one was a stance.
    private const float RhythmMemory = 0.8f;

    private readonly GaitCycle cycle = new GaitCycle();
    private readonly MotionLatch motion = new MotionLatch();
    private (bool front, bool back) cues;
    private (bool front, bool back) outOfTurn;
    private (bool front, float secondsAgo) lastLift = (false, float.PositiveInfinity);

    public float Phase => cycle.Phase;
    public bool IsMoving => motion.IsMoving;
    // Share of a stride the clock advanced this tick.
    public float StridesThisTick { get; private set; }
    // Share of the cycle a swing on the clock lasts at the moment: ramped toward the gait's share at the current speed.
    public float SwingShare { get; private set; } = 0.4f;
    // How far a swing on the clock progresses this tick.
    public float SwingProgressThisTick => StridesThisTick / Mathf.Max(0.05f, SwingShare);
    public bool LastLiftWasFront => lastLift.front;
    public float SecondsSinceLastLift => lastLift.secondsAgo;

    // targetSwingShare: the gait's swing share at this speed. frontLegTrails: the front leg is behind in the direction
    // of motion, so it steps first from a stance.
    public void Advance((float speed, float strideLength, float targetSwingShare, float deltaTime, bool frontLegTrails) tick, GaitTransitionSettings transitions) {
        lastLift.secondsAgo += tick.deltaTime;
        if (motion.Track(tick.speed, transitions) == MotionLatch.Change.Started) {
            cycle.PrimeToLift(FirstLegToStep(tick.frontLegTrails));
            SwingShare = tick.targetSwingShare;
        }
        if (!IsMoving) {
            StridesThisTick = 0f;
            cues = (false, false);
            outOfTurn = (false, false);
            return;
        }

        StridesThisTick = Mathf.Abs(tick.speed) * tick.deltaTime / Mathf.Max(0.01f, tick.strideLength);
        SwingShare = Mathf.MoveTowards(SwingShare, tick.targetSwingShare, transitions.swingShareChangePerCycle * StridesThisTick);
        (bool frontLeg, bool backLeg) crossed = cycle.Advance(StridesThisTick);
        cues = (cues.front || crossed.frontLeg, cues.back || crossed.backLeg);
    }

    // The feet were just placed in a stance by something else (a landing, a roll): the gait starts afresh from it, with
    // the trailing foot stepping first, instead of carrying on a rhythm the feet no longer match.
    public void RestartFromStance() {
        motion.Reset();
        cues = (false, false);
        outOfTurn = (false, false);
        StridesThisTick = 0f;
    }

    public bool IsCued(bool frontLeg) => frontLeg ? cues.front : cues.back;

    // The cue has come, the leg is planted, and the other foot is down or far enough through its swing.
    public bool MayLiftNow(bool frontLeg, (bool isPlanted, bool otherLetsLift) legs) {
        return IsCued(frontLeg) && legs.isPlanted && legs.otherLetsLift;
    }

    public void ConsumeCue(bool frontLeg) {
        if (frontLeg) {
            cues.front = false;
        }
        else {
            cues.back = false;
        }
    }

    // Returns true when the lift answered an out-of-turn cue and re-phased the clock to it.
    public bool RecordLift(bool frontLeg) {
        lastLift = (frontLeg, 0f);
        bool wasOutOfTurn = frontLeg ? outOfTurn.front : outOfTurn.back;
        if (wasOutOfTurn) {
            cycle.SyncToLift(frontLeg);
            outOfTurn = frontLeg ? (false, outOfTurn.back) : (outOfTurn.front, false);
        }
        return wasOutOfTurn;
    }

    // Cues a leg out of turn (an emergency balance step). When it lifts, the clock is put at its lift, so the other leg
    // follows half a cycle later.
    public void CueOutOfTurn(bool frontLeg) {
        if (frontLeg) {
            cues.front = true;
            outOfTurn.front = true;
        }
        else {
            cues.back = true;
            outOfTurn.back = true;
        }
    }

    private bool FirstLegToStep(bool frontLegTrails) => lastLift.secondsAgo < RhythmMemory ? !lastLift.front : frontLegTrails;
}
