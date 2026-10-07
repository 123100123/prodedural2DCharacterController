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
    // A leg that stepped ahead of its cue (released at full extension): its coming cue was answered by that step.
    private (bool front, bool back) steppedAhead;
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
            steppedAhead = (false, false);
            return;
        }

        StridesThisTick = Mathf.Abs(tick.speed) * tick.deltaTime / Mathf.Max(0.01f, tick.strideLength);
        SwingShare = Mathf.MoveTowards(SwingShare, tick.targetSwingShare, transitions.swingShareChangePerCycle * StridesThisTick);
        (bool frontLeg, bool backLeg) crossed = cycle.Advance(StridesThisTick);
        // A cue a leg already answered by stepping ahead of it is used up as it comes.
        (bool front, bool back) answered = (crossed.frontLeg && steppedAhead.front, crossed.backLeg && steppedAhead.back);
        steppedAhead = (steppedAhead.front && !crossed.frontLeg, steppedAhead.back && !crossed.backLeg);
        cues = (cues.front || (crossed.frontLeg && !answered.front), cues.back || (crossed.backLeg && !answered.back));
    }

    // The feet were just placed in a stance by something else (a landing, a roll): the gait starts afresh from it, with
    // the trailing foot stepping first, instead of carrying on a rhythm the feet no longer match.
    public void RestartFromStance() {
        motion.Reset();
        cues = (false, false);
        outOfTurn = (false, false);
        steppedAhead = (false, false);
        StridesThisTick = 0f;
    }

    // The feet were just placed where a moving gait has them (a running landing): the rhythm carries on, with the given
    // leg stepping first, instead of starting afresh from a stance.
    public void ContinueRhythm(bool frontLegStepsFirst) {
        lastLift = (!frontLegStepsFirst, 0f);
    }

    public bool IsCued(bool frontLeg) => frontLeg ? cues.front : cues.back;

    // Share of a stride left before the leg is cued; none while its cue is pending.
    public float StridesUntilCue(bool frontLeg) => IsCued(frontLeg) ? 0f : cycle.StridesUntilLift(frontLeg);

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
            steppedAhead = (false, false);
        }
        return wasOutOfTurn;
    }

    // The leg is next to step but its cue has not come yet: no cue pending and its lift phase is less than half a cycle away
    // (the other leg's comes first otherwise).
    public bool IsNextToStep(bool frontLeg) => !IsCued(frontLeg) && !(frontLeg ? steppedAhead.front : steppedAhead.back) && cycle.StridesUntilLift(frontLeg) <= 0.5f;

    // The leg stepped ahead of its cue as its normal next step (a trailing foot released at full extension): the cue, when
    // it comes, is used up by that step, so the clock keeps its phase and the legs stay half a cycle apart.
    public void StepAheadOfCue(bool frontLeg) {
        steppedAhead = frontLeg ? (true, steppedAhead.back) : (steppedAhead.front, true);
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
