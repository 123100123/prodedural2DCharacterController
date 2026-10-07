using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Shift as a run intent: the pure rules (the intent itself, the walk's readiness, the crouch latch it cancels, the roll's
// run exit), each state's own readiness answer, and the state machine's record of its last switch.
public class RunIntentTests {
    [Test]
    public void TheRunInputWithNoDirectionIsNoIntent() {
        Assert.That(RunIntent.Resolve((true, 0f)), Is.False, "Shift alone does nothing");
        Assert.That(RunIntent.Resolve((true, 0.1f)), Is.False, "inside the stick dead zone");
        Assert.That(RunIntent.Resolve((false, 1f)), Is.False, "a direction alone is a walk");
        Assert.That(RunIntent.Resolve((true, 1f)), Is.True);
        Assert.That(RunIntent.Resolve((true, -1f)), Is.True);
    }

    [Test]
    public void TheSlidesRunIntentIsTheSameRuleTowardTheAimSide() {
        Assert.That(SlidePolicy.InputDeadZone, Is.EqualTo(RunIntent.InputDeadZone));
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, false, 1)), Is.True);
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, true, 1)), Is.True, "aiming toward the side it moves runs");
        Assert.That(SlidePolicy.IsRunIntent((true, -1f, true, -1)), Is.True);
        Assert.That(SlidePolicy.IsRunIntent((true, 1f, true, -1)), Is.False, "backpedalling while aiming is a walk");
        Assert.That(SlidePolicy.IsRunIntent((true, 0f, false, 1)), Is.False);
    }

    [Test]
    public void TheIntentPointsAlongADirectionOnlyWhenItIsThere() {
        Assert.That(RunIntent.Along((true, 1f), 1), Is.True);
        Assert.That(RunIntent.Along((true, -1f), 1), Is.False, "pointing back");
        Assert.That(RunIntent.Along((false, 1f), 1), Is.False, "no intent");
        Assert.That(RunIntent.Along((true, -1f), -1), Is.True);
    }

    [Test]
    public void TheWalkNamesWhatHoldsItBackInOrder() {
        Assert.That(OnFeet(aimingBackward: true, held: true).Block, Is.EqualTo(RunBlock.AimingBackward), "backpedalling while aiming first");
        Assert.That(OnFeet(held: true).Block, Is.EqualTo(RunBlock.CrouchHeld), "aiming forward does not block the run");
        Assert.That(OnFeet(held: true, requested: true, headroom: true).Block, Is.EqualTo(RunBlock.CrouchHeld), "a held crouch wins");
        Assert.That(OnFeet(requested: true, headroom: true).Block, Is.EqualTo(RunBlock.NoHeadroom), "a low ceiling keeps the crouch");
        Assert.That(OnFeet(tap: true).Block, Is.EqualTo(RunBlock.CrouchTap), "a press that may still be a tap");
        Assert.That(OnFeet(tap: true, headroom: true).Block, Is.EqualTo(RunBlock.NoHeadroom));
        Assert.That(OnFeet(requested: true).Block, Is.EqualTo(RunBlock.CrouchLatched));
        Assert.That(OnFeet(pushing: true).Block, Is.EqualTo(RunBlock.Pushing));
        Assert.That(OnFeet().CanRun, Is.True, "nothing in the way");
        Assert.That(OnFeet().Block, Is.EqualTo(RunBlock.None));
    }

    [Test]
    public void TheRunIntentCancelsALatchedCrouchButNotAHeldOne() {
        Assert.That(RunIntent.CancelsCrouchLatch(true, OnFeet(requested: true)), Is.True, "latched + intent: cancel");
        Assert.That(RunIntent.CancelsCrouchLatch(true, OnFeet(requested: true, held: true)), Is.False, "held + intent: keep (crouch walk)");
        Assert.That(RunIntent.CancelsCrouchLatch(false, OnFeet(requested: true)), Is.False, "no intent (Shift with no direction): keep");
        Assert.That(RunIntent.CancelsCrouchLatch(true, OnFeet(requested: true, headroom: true)), Is.False, "under a low ceiling: keep");
        Assert.That(RunIntent.CancelsCrouchLatch(true, RunReadiness.BlockedBy(RunBlock.Sliding)), Is.False, "the slide's own latch stays");
    }

    [Test]
    public void UnderTheIntentACrouchPressThatMayStillBeATapDoesNotCrouch() {
        Assert.That(RunIntent.IgnoresCrouchTap(true, OnFeet(tap: true)), Is.True, "a young press is ignored");
        Assert.That(RunIntent.IgnoresCrouchTap(false, OnFeet(tap: true)), Is.False, "no intent: it crouches as before");
        Assert.That(RunIntent.IgnoresCrouchTap(true, OnFeet(held: true)), Is.False, "held past the tap time: crouch walk");
        Assert.That(RunIntent.IgnoresCrouchTap(true, RunReadiness.BlockedBy(RunBlock.Sliding)), Is.False, "the slide's crouch is its own");
        Assert.That(RunIntent.CancelsCrouchLatch(true, OnFeet(tap: true)), Is.False, "nothing to cancel yet");
    }

    [Test]
    public void TheToggleTellsAPressThatMayStillBeATapFromAHold() {
        HoldOrTapToggle toggle = new HoldOrTapToggle(0.25f);
        toggle.Update((true, false, true), 0f);
        Assert.That(toggle.IsPossibleTap(0.2f), Is.True);
        Assert.That(toggle.IsPossibleTap(0.3f), Is.False, "held past the tap time");
        toggle.CancelLatch();
        Assert.That(toggle.IsPossibleTap(0.1f), Is.False, "a press whose start was forgotten is a hold");
    }

    // The gait still runs at the stick's share of the run speed below the intent's dead zone, as before the intent.
    [Test]
    public void ASmallStickTiltWithRunHeldHeadsForItsShareOfTheRunSpeed() {
        const float smallTilt = 0.156f;
        Assert.That(smallTilt, Is.LessThan(RunIntent.InputDeadZone));
        Assert.That(RunIntent.Resolve((true, smallTilt)), Is.False, "no intent inside the dead zone");
        Assert.That(RunIntent.GaitRuns((true, smallTilt, false, 1)), Is.True, "but the gait runs");
        Assert.That(RunIntent.GaitRuns((true, 0f, false, 1)), Is.False);
        Assert.That(RunIntent.GaitRuns((true, 1f, true, 1)), Is.True, "aiming right while moving right runs");
        Assert.That(RunIntent.GaitRuns((true, -1f, true, -1)), Is.True, "aiming left while moving left runs");
        Assert.That(RunIntent.GaitRuns((true, 1f, true, -1)), Is.False, "backpedalling right while aiming left walks");
        Assert.That(RunIntent.GaitRuns((true, -smallTilt, true, 1)), Is.False, "a small backward tilt walks too");
        Assert.That(RunIntent.GaitRuns((true, -1f, false, 1)), Is.True, "without aiming the facing does not matter");
        Assert.That(RunIntent.GaitRuns((false, 1f, false, 1)), Is.False);
        Assert.That(RunIntent.AimingBackward(true, 0f, 1), Is.False, "standing still is not backpedalling");
        GaitSet gaits = new GaitSet((GaitProfile.Walk, GaitProfile.Run, GaitProfile.Crouch), GaitTransitionSettings.Default, 0f);
        float target = gaits.TargetSpeed((smallTilt, RunIntent.GaitRuns((true, smallTilt, false, 1))));
        Assert.That(target, Is.EqualTo(smallTilt * GaitProfile.Run.speed).Within(1e-4f));
    }

    [Test]
    public void TheRollHandsOverToTheRunPartWayThroughTheRecoveryOnlyWithTheIntentAlongIt() {
        RollRunExitSettings settings = new RollRunExitSettings();
        Assert.That(settings.runExitShare, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(settings.HandsOver((true, settings.runExitShare - 0.01f, 0f)), Is.False, "too early");
        Assert.That(settings.HandsOver((true, settings.runExitShare, settings.maxStandGap)), Is.True);
        Assert.That(settings.HandsOver((true, 1f, settings.maxStandGap + 0.01f)), Is.False, "the pelvis is still too low: the walk would jerk it up");
        Assert.That(settings.HandsOver((false, 1f, 0f)), Is.False, "no intent along the roll: the full recovery");
        Assert.That(settings.KeepsTravelSpeed(true), Is.True);
        Assert.That(settings.KeepsTravelSpeed(false), Is.False);

        settings.enabled = false;
        Assert.That(settings.HandsOver((true, 1f, 0f)), Is.False, "switched off");
        Assert.That(settings.KeepsTravelSpeed(true), Is.False);
    }

    [Test]
    public void EveryStateAnswersForItselfWithItsOwnReason() {
        CharacterStateMachine machine = new CharacterStateMachine();
        CharacterStates states = new CharacterStates();
        StateContext context = new StateContext((machine, null, states));
        PlayerCommands running = new PlayerCommands { MoveAxis = 1f, RunHeld = true, RunIntent = true };
        Dictionary<ICharacterState, RunBlock> expected = new Dictionary<ICharacterState, RunBlock> {
            { new JumpWindupState(context, (new JumpSettings(), new AirborneSettings())), RunBlock.JumpWindup },
            { new AirborneState(context, (new AirborneSettings(), new JumpSettings(), new AirPoseSettings(), new FallSecondarySettings(), new LandingSettings())), RunBlock.Airborne },
            { new RollState(context, (new RollSettings(), new RollMomentumSettings(), new RollRunExitSettings())), RunBlock.RollCommitted },
            { new ClimbState(context, (new ClimbSettings(), new LedgeHangSettings())), RunBlock.Climbing },
            { new WallSlideState(context, new WallSettings()), RunBlock.WallSliding },
            { new SlideState(context, (new SlideSettings(), new JumpSettings())), RunBlock.Sliding },
        };
        HashSet<string> names = new HashSet<string> { "Walk" };
        foreach (KeyValuePair<ICharacterState, RunBlock> state in expected) {
            RunReadiness readiness = state.Key.RunReadinessFor(running);
            Assert.That(readiness.Block, Is.EqualTo(state.Value), state.Key.Name);
            Assert.That(readiness.CanRun, Is.False, state.Key.Name);
            Assert.That(readiness.Reason, Is.Not.Empty);
            Assert.That(names.Add(state.Key.Name), Is.True, $"{state.Key.Name} is a name of its own");
        }
    }

    [Test]
    public void EveryReasonHasAName() {
        foreach (RunBlock block in System.Enum.GetValues(typeof(RunBlock))) {
            Assert.That(RunReadiness.BlockedBy(block).Reason, Is.Not.Empty, block.ToString());
        }
    }

    [Test]
    public void TheMachineRecordsItsLastSwitch() {
        CharacterStateMachine machine = new CharacterStateMachine();
        List<string> calls = new List<string>();
        FakeState first = new FakeState("First", calls);
        FakeState second = new FakeState("Second", calls);
        Assert.That(machine.ChangeCount, Is.Zero);
        Assert.That(machine.Previous, Is.Null);

        machine.ChangeTo(first);
        Assert.That(machine.Current, Is.SameAs(first));
        Assert.That(machine.Previous, Is.Null, "nothing before the first state");
        Assert.That(machine.ChangeCount, Is.EqualTo(1));

        machine.ChangeTo(second);
        Assert.That(machine.Current, Is.SameAs(second));
        Assert.That(machine.Previous, Is.SameAs(first));
        Assert.That(machine.ChangeCount, Is.EqualTo(2));
        Assert.That(machine.LastChangeTime, Is.EqualTo(Time.time));
        Assert.That(calls, Is.EqualTo(new[] { "First.Enter", "First.Exit", "Second.Enter" }), "exit before enter, as before");
    }

    private static RunReadiness OnFeet(bool aimingBackward = false, bool requested = false, bool held = false, bool headroom = false, bool pushing = false, bool tap = false) {
        // A held crouch is also requested (the reader's crouch request is held or latched); a pending tap is held.
        return RunReadiness.OnFeet((aimingBackward, (requested || held || tap, held || tap, tap, headroom), pushing));
    }

    private sealed class FakeState : ICharacterState {
        private readonly List<string> calls;

        public FakeState(string name, List<string> calls) {
            Name = name;
            this.calls = calls;
        }

        public string Name { get; }
        public bool AllowsUpperBodyActions => true;
        public bool AllowsCrouch => true;
        public RunReadiness RunReadinessFor(PlayerCommands commands) => RunReadiness.Ready;
        public void Enter() => calls.Add(Name + ".Enter");
        public void Tick(float deltaTime, PlayerCommands commands) { }
        public void Exit() => calls.Add(Name + ".Exit");
    }
}
