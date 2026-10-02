using System.Collections.Generic;
using UnityEngine;

// The grounded gait's timing without its geometry: the same speed ramp, pace, step scheduler and clock-driven swings
// StrideController uses, ticked at a fixed rate, recording when each foot lifts and lands.
public sealed class GaitTimingSimulation {
    public const float Tick = 1f / 120f;

    public readonly struct FootEvent {
        public readonly float Time;
        public readonly bool Front;
        public readonly float Phase;

        public FootEvent((float time, bool front, float phase) footEvent) {
            Time = footEvent.time;
            Front = footEvent.front;
            Phase = footEvent.phase;
        }
    }

    private readonly (GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) gaits =
        (GaitProfile.Walk, GaitProfile.Run, GaitTransitionSettings.Default);
    private readonly StepScheduler scheduler = new StepScheduler();
    private readonly SwingProgress front = new SwingProgress();
    private readonly SwingProgress back = new SwingProgress();
    private float speed;
    private float running;

    public float Time { get; private set; }


    public List<FootEvent> Lifts { get; } = new List<FootEvent>();
    public List<FootEvent> Plants { get; } = new List<FootEvent>();

    public GaitProfile Gait => GaitProfile.Blend((gaits.walk, gaits.run), running);
    public bool IsFrontSwinging => !front.IsGrounded;

    public void Hold((float direction, bool running) input, float seconds) {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Tick) {
            Step(input);
        }
    }

    // Holds the input until the given leg is swinging, then for the given share of its swing.
    public void HoldUntilSwinging((float direction, bool running) input, (bool frontLeg, float progress) swing) {
        SwingProgress leg = swing.frontLeg ? front : back;
        while (leg.IsGrounded || leg.Progress < swing.progress) {
            Step(input);
        }
    }

    private void Step((float direction, bool running) input) {
        Time += Tick;
        float target = input.direction * (input.running ? gaits.run.speed : gaits.walk.speed);
        speed = GroundSpeedRamp.Step((speed, target, Gait), Tick);
        running = Mathf.MoveTowards(running, LocomotionPace.RunningAt(speed, gaits), gaits.transitions.runBlendRate * Tick);

        float swingShare = SwingTiming.CycleShareAt(speed, Gait);
        scheduler.Advance((speed, GaitTiming.StrideLength(speed, Gait), swingShare, Tick, true), gaits.transitions);
        if (scheduler.IsMoving) {
            LiftOnCue((true, front, back));
            LiftOnCue((false, back, front));
        }
        AdvanceSwing((true, front));
        AdvanceSwing((false, back));
    }

    private void LiftOnCue((bool isFront, SwingProgress stepping, SwingProgress other) legs) {
        if (!scheduler.MayLiftNow(legs.isFront, (legs.stepping.IsGrounded, legs.other.LetsOtherLegLift))) {
            return;
        }
        scheduler.ConsumeCue(legs.isFront);
        float release = Mathf.Lerp(gaits.transitions.walkLiftOverlap, gaits.transitions.runLiftOverlap, running);
        legs.stepping.Begin(SwingTiming.OnClock((Gait.maxSwingDuration, release)));
        scheduler.RecordLift(legs.isFront);
        Lifts.Add(new FootEvent((Time, legs.isFront, scheduler.Phase)));
    }

    private void AdvanceSwing((bool isFront, SwingProgress leg) foot) {
        if (foot.leg.Advance((scheduler.SwingProgressThisTick, Tick))) {
            Plants.Add(new FootEvent((Time, foot.isFront, scheduler.Phase)));
        }
    }
}
