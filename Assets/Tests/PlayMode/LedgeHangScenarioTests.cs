using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The holdable ledge hang on the real character, against a tall ledge built for each test: a ledge grabbed without
// pressing toward it is held until up climbs it, down lets go without grabbing it straight back, and holding toward the
// ledge as it is grabbed still climbs straight on.
public class LedgeHangScenarioTests : ScenarioFixture {
    private const float LedgeHeight = 9f;
    // The idle sway and breathing move the hanging body at most about this far.
    private const float HangDrift = 0.15f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;
    private ClimbState Climb => Stage.Character.Parts.Climb;
    // The top of the ledge the last LiftBesideATallLedge built.
    private float ledgeTop;

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // Turns the character to face right (toward the ledges these tests build) and lets it settle.
    private IEnumerator FaceRight() {
        Stage.Press(Key.D);
        yield return Hold(0.1f);
        Stage.Release(Key.D);
        yield return Hold(0.6f);
    }

    // A tall, deep ledge just right of the body; the character is lifted beside its top and falls to its hands.
    // Returns the ledge top.
    private float LiftBesideATallLedge() {
        ledgeTop = Stage.AddLedge((Body.x + 0.5f, LedgeHeight, 30f));
        Stage.Lift(ledgeTop - 1.5f - Body.y);
        return ledgeTop;
    }

    private IEnumerator HangWithoutPressingToward() {
        yield return FaceRight();
        LiftBesideATallLedge();
        yield return WaitUntil(() => Climb.IsHanging, 2f);
        Assert.That(Climb.IsHanging, Is.True, "grabbed the ledge and hangs");
    }

    [UnityTest]
    public IEnumerator ALedgeGrabbedWithoutPressingTowardItIsHeldUntilUpClimbsIt() {
        yield return FaceRight();
        float top = LiftBesideATallLedge();
        yield return WaitUntil(() => Climb.IsHanging, 2f);
        Assert.That(Climb.IsHanging, Is.True, "grabbed the ledge and hangs");

        Vector2 hangStart = Body;
        float drift = 0f;
        bool heldOn = true;
        for (float elapsed = 0f; elapsed < 1f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            heldOn &= Climb.IsHanging;
            drift = Mathf.Max(drift, Vector2.Distance(Body, hangStart));
        }
        Report($"hung for 1 s, drifting at most {drift:F3} from the hang position");
        Assert.That(heldOn, Is.True, "still hanging after a second");
        Assert.That(drift, Is.LessThan(HangDrift), "the hang only sways");
        Assert.That(drift, Is.GreaterThan(0.002f), "the hang is alive: it sways and breathes");
        Assert.That(Body.y, Is.LessThan(top - 1f), "below the top, at arm's length");

        Stage.Press(Key.W);
        yield return WaitUntil(() => State is WalkState, 4f);
        Stage.Release(Key.W);
        yield return Hold(0.3f);
        Assert.That(State, Is.InstanceOf<WalkState>(), "climbed up and walks");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(0.05f), "stands on top");
    }

    [UnityTest]
    public IEnumerator DownLetsGoOfTheHangWithoutGrabbingItStraightBack() {
        yield return HangWithoutPressingToward();
        int drops = 0;
        Stage.Character.Events.LedgeDropped += _ => drops++;

        Stage.Press(Key.S);
        bool leftTheLedge = false;
        bool grabbedAgain = false;
        for (float elapsed = 0f; elapsed < 3f && !(leftTheLedge && State is WalkState); elapsed += ScenarioStage.FrameTime) {
            yield return null;
            if (!(State is ClimbState)) {
                leftTheLedge = true;
            }
            else if (leftTheLedge) {
                grabbedAgain = true;
            }
        }
        Stage.Release(Key.S);
        yield return Hold(0.3f);

        Assert.That(leftTheLedge, Is.True, "let go");
        Assert.That(drops, Is.EqualTo(1), "one drop event");
        Assert.That(grabbedAgain, Is.False, "did not grab the ledge straight back");
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(0.05f), "on the ground below");
    }

    [UnityTest]
    public IEnumerator AwayFromTheLedgeAlsoLetsGo() {
        yield return HangWithoutPressingToward();
        Stage.Press(Key.A);
        yield return WaitUntil(() => !(State is ClimbState), 0.5f);
        Stage.Release(Key.A);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "dropped into a fall");
    }

    [UnityTest]
    public IEnumerator JumpWhileHangingPullsUp() {
        yield return HangWithoutPressingToward();
        Stage.Press(Key.Space);
        yield return null;
        Stage.Release(Key.Space);
        yield return WaitUntil(() => State is WalkState, 4f);
        yield return Hold(0.2f);
        Assert.That(State, Is.InstanceOf<WalkState>(), "climbed up");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ledgeTop).Within(0.05f), "stands on top");
    }

    [UnityTest]
    public IEnumerator HoldingTowardTheLedgeAsItIsGrabbedClimbsStraightOn() {
        yield return FaceRight();
        Stage.Press(Key.D);
        float top = LiftBesideATallLedge();
        yield return WaitUntil(() => State is ClimbState, 2f);
        Assert.That(State, Is.InstanceOf<ClimbState>(), "grabbed the ledge");

        bool everHeld = false;
        for (float elapsed = 0f; elapsed < 3f && !(State is WalkState); elapsed += ScenarioStage.FrameTime) {
            yield return null;
            everHeld |= Climb.IsHanging;
        }
        Stage.Release(Key.D);
        yield return Hold(0.2f);

        Assert.That(everHeld, Is.False, "never waited in the hang");
        Assert.That(State, Is.InstanceOf<WalkState>(), "climbed up");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(0.05f), "stands on top");
    }
}
