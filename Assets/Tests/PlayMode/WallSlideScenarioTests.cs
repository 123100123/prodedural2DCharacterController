using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Wall slides and wall jumps on the real character, against walls built for each test: catching a wall slides down it
// at the capped speed with the back to it, letting go or landing ends the slide, a jump kicks off away and up, wall
// jumps climb a shaft, and a ledge in reach is still climbed rather than slid past.
public class WallSlideScenarioTests : ScenarioFixture {
    private const float TallWall = 30f;
    // The hull's half width: a body held against a wall stands this far from its face (plus the motor's skin).
    private const float HullHalfWidth = 0.4f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;
    private bool IsSliding => State is WallSlideState;
    private WallSettings Wall => Stage.Character.Parts.Wall;
    // +1 facing right, -1 facing left (the rig is authored facing right).
    private int Facing => Stage.Character.transform.localScale.x < 0f ? -1 : 1;

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // A tall wall to the right, the character lifted beside it and pressing into it (after falling freely for a while):
    // it falls, catches the wall and slides.
    private IEnumerator SlideDownATallWallOnTheRight(float lift, float fallFirst = 0f) {
        Stage.AddWall((Body.x + 1f, 1, TallWall));
        yield return Hold(0.5f);
        Stage.Lift(lift);
        yield return Hold(fallFirst);
        Stage.Press(Key.D);
        yield return WaitUntil(() => IsSliding, 2f);
        Assert.That(IsSliding, Is.True, "caught the wall");
    }

    [UnityTest]
    public IEnumerator JumpingIntoAWallWhileHoldingTowardItSlidesDownAtTheCappedSpeed() {
        float faceX = Body.x + 1.6f;
        Stage.AddWall((faceX, 1, TallWall));
        yield return Hold(0.5f);

        Stage.Press(Key.D, Key.Space);
        yield return Hold(0.3f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => IsSliding, 2f);
        Assert.That(IsSliding, Is.True, "caught the wall at the top of the jump");

        float fastest = 0f;
        float slideTime = 0f;
        float previousY = Body.y;
        bool facedAway = true;
        bool heldAgainstWall = true;
        while (IsSliding && slideTime < 3f) {
            yield return null;
            if (!IsSliding) {
                break;
            }
            slideTime += ScenarioStage.FrameTime;
            float speed = (previousY - Body.y) / ScenarioStage.FrameTime;
            previousY = Body.y;
            if (slideTime > 0.25f) {
                fastest = Mathf.Max(fastest, speed);
            }
            facedAway &= Facing == -1;
            heldAgainstWall &= Mathf.Abs(faceX - (Body.x + HullHalfWidth)) < 0.1f;
        }

        Report($"slid for {slideTime:F2} s, fastest {fastest:F2} u/s (cap {Wall.maxSlideSpeed:F2})");
        Assert.That(slideTime, Is.GreaterThan(0.35f), "a slide, not a bump");
        Assert.That(fastest, Is.GreaterThan(Wall.maxSlideSpeed * 0.8f), "slides down");
        Assert.That(fastest, Is.LessThanOrEqualTo(Wall.maxSlideSpeed + 0.05f), "never faster than the slide speed");
        Assert.That(facedAway, Is.True, "the back to the wall");
        Assert.That(heldAgainstWall, Is.True, "held against the wall");
    }

    [UnityTest]
    public IEnumerator ACatchFromAFastFallBrakesToTheSlideSpeed() {
        yield return SlideDownATallWallOnTheRight(16f, 0.4f);
        float previousY = Body.y;
        yield return null;
        float catchSpeed = (previousY - Body.y) / ScenarioStage.FrameTime;
        yield return Hold((catchSpeed - Wall.maxSlideSpeed) / Wall.slideAcceleration + 0.1f);
        previousY = Body.y;
        yield return null;
        float speed = (previousY - Body.y) / ScenarioStage.FrameTime;
        Report($"caught at {catchSpeed:F2} u/s, after braking {speed:F2} u/s");
        Assert.That(catchSpeed, Is.GreaterThan(Wall.maxSlideSpeed + 3f), "caught in a fast fall");
        Assert.That(IsSliding, Is.True, "still sliding");
        Assert.That(speed, Is.EqualTo(Wall.maxSlideSpeed).Within(0.1f));
    }

    [UnityTest]
    public IEnumerator LettingGoOfTheWallEndsTheSlideAfterTheGrace() {
        yield return SlideDownATallWallOnTheRight(10f);
        yield return Hold(0.3f);
        Assert.That(IsSliding, Is.True);

        Stage.Release(Key.D);
        yield return null;
        yield return null;
        Assert.That(IsSliding, Is.True, "a moment's let-go keeps the slide");
        yield return Hold(Wall.releaseGrace + 0.1f);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "drops off into a fall");

        yield return Hold(0.3f);
        float previousY = Body.y;
        yield return null;
        float fallSpeed = (previousY - Body.y) / ScenarioStage.FrameTime;
        Report($"fall speed after letting go {fallSpeed:F2}");
        Assert.That(IsSliding, Is.False, "does not catch the wall again without pressing into it");
        Assert.That(fallSpeed, Is.GreaterThan(Wall.maxSlideSpeed + 2f), "falls freely again");
    }

    [UnityTest]
    public IEnumerator AWallJumpKicksAwayFromTheWallAndUp() {
        List<(Vector2 position, int direction)> kicks = new List<(Vector2, int)>();
        yield return SlideDownATallWallOnTheRight(8f);
        Stage.Character.Events.WallJumped += kick => kicks.Add(kick);
        yield return Hold(0.2f);
        Vector2 start = Body;

        Stage.Press(Key.Space);
        yield return Hold(0.05f);
        int facingAfterKick = Facing;
        yield return Hold(0.2f);
        Vector2 after = Body;
        AirSnapshot air = Stage.Inspector.ReadTelemetry().Air;
        Stage.Release(Key.Space);

        Report($"moved {after - start} (launch {air.LaunchVelocity})");
        Assert.That(kicks.Count, Is.EqualTo(1), "one wall jump");
        Assert.That(kicks[0].direction, Is.EqualTo(-1), "away from the wall on the right");
        Assert.That(State, Is.InstanceOf<AirborneState>());
        Assert.That(air.LaunchedByJump, Is.True);
        Assert.That(air.LaunchVelocity.x, Is.EqualTo(-Wall.jumpVelocity.x).Within(0.01f));
        Assert.That(after.x, Is.LessThan(start.x - 1f), "away from the wall, even holding toward it");
        Assert.That(after.y, Is.GreaterThan(start.y + 1f), "and up");
        Assert.That(facingAfterKick, Is.EqualTo(-1), "turned the way it flies");
    }

    [UnityTest]
    public IEnumerator AJumpJustAfterDroppingOffTheWallStillKicksOffIt() {
        int kicks = 0;
        yield return SlideDownATallWallOnTheRight(10f);
        Stage.Character.Events.WallJumped += _ => kicks++;
        yield return Hold(0.3f);

        Stage.Release(Key.D);
        yield return WaitUntil(() => !IsSliding, 1f);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "dropped off the wall");
        yield return null;
        yield return null;
        Stage.Press(Key.Space);
        yield return Hold(0.1f);
        AirSnapshot air = Stage.Inspector.ReadTelemetry().Air;
        Stage.Release(Key.Space);

        Assert.That(kicks, Is.EqualTo(1), "the coyote window still kicks off the wall");
        Assert.That(air.LaunchVelocity.x, Is.LessThan(0f), "away from it");
        Assert.That(air.LaunchVelocity.y, Is.GreaterThan(0f), "and up");
    }

    [UnityTest]
    public IEnumerator AJumpLongAfterDroppingOffTheWallDoesNotKickOffIt() {
        int kicks = 0;
        yield return SlideDownATallWallOnTheRight(14f);
        Stage.Character.Events.WallJumped += _ => kicks++;
        yield return Hold(0.3f);

        Stage.Release(Key.D);
        yield return WaitUntil(() => !IsSliding, 1f);
        yield return Hold(Wall.coyoteTime + 0.15f);
        Stage.Press(Key.Space);
        yield return Hold(0.1f);
        Stage.Release(Key.Space);

        Assert.That(kicks, Is.Zero, "too late for a wall jump");
        Assert.That(State, Is.InstanceOf<AirborneState>(), "still falling");
    }

    [UnityTest]
    public IEnumerator AlternatingWallJumpsClimbAShaftHigherThanAJump() {
        float groundY = Body.y;
        Stage.Press(Key.Space);
        float singleJumpApex = groundY;
        for (float elapsed = 0f; elapsed < 1.5f; elapsed += ScenarioStage.FrameTime) {
            if (elapsed > 0.3f) {
                Stage.Release(Key.Space);
            }
            yield return null;
            singleJumpApex = Mathf.Max(singleJumpApex, Body.y);
        }
        yield return Hold(0.5f);

        float x = Body.x;
        Stage.AddWall((x - 1.4f, -1, TallWall));
        Stage.AddWall((x + 1.4f, 1, TallWall));
        int kicks = 0;
        Stage.Character.Events.WallJumped += _ => kicks++;
        yield return Hold(0.3f);

        Stage.Press(Key.D, Key.Space);
        yield return Hold(0.3f);
        Stage.Release(Key.Space);
        Key toward = Key.D;
        float highest = Body.y;
        for (int jump = 0; jump < 6; jump++) {
            yield return WaitUntil(() => IsSliding, 1.5f);
            Assert.That(IsSliding, Is.True, $"caught the wall before wall jump {jump + 1}");
            Key away = toward == Key.D ? Key.A : Key.D;
            Stage.Release(toward);
            Stage.Press(away, Key.Space);
            toward = away;
            for (int frame = 0; frame < 18; frame++) {
                yield return null;
                highest = Mathf.Max(highest, Body.y);
            }
            Stage.Release(Key.Space);
        }
        yield return WaitUntil(() => IsSliding, 1.5f);
        highest = Mathf.Max(highest, Body.y);

        Report($"single jump apex {singleJumpApex - groundY:F2} above the ground, shaft climb {highest - groundY:F2} after {kicks} wall jumps");
        Assert.That(kicks, Is.EqualTo(6));
        Assert.That(highest, Is.GreaterThan(singleJumpApex + 4f), "the wall jumps climb far past a single jump");
    }

    [UnityTest]
    public IEnumerator ALedgeInReachIsClimbedRatherThanSlidPast() {
        float faceX = Body.x + 0.5f;
        float top = Stage.AddLedge((faceX, 9f, 4f));
        yield return Hold(0.5f);
        bool everSlid = false;

        Stage.Press(Key.D);
        Stage.Lift(top - 1.5f - Body.y);
        for (float elapsed = 0f; elapsed < 1.5f && !(State is ClimbState); elapsed += ScenarioStage.FrameTime) {
            yield return null;
            everSlid |= IsSliding;
        }
        Assert.That(State, Is.InstanceOf<ClimbState>(), "grabs the ledge");
        Assert.That(everSlid, Is.False, "never slid instead");

        Stage.Release(Key.D);
        yield return WaitUntil(() => State is WalkState, 4f);
        yield return Hold(0.3f);
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(0.05f), "stands on top");
    }

    [UnityTest]
    public IEnumerator SlidingDownToTheGroundLandsBackIntoTheWalk() {
        List<float> landings = new List<float>();
        Stage.Character.Events.Landed += landing => landings.Add(landing.impactSpeed);
        yield return SlideDownATallWallOnTheRight(4f);
        yield return Hold(0.2f);
        landings.Clear();

        int airborneFrames = 0;
        for (float elapsed = 0f; elapsed < 4f && !(State is WalkState); elapsed += ScenarioStage.FrameTime) {
            yield return null;
            airborneFrames += State is AirborneState ? 1 : 0;
        }
        yield return Hold(0.5f);

        Report($"landed at {(landings.Count > 0 ? landings[0] : -1f):F2} u/s after {airborneFrames} frames in the air");
        Assert.That(State, Is.InstanceOf<WalkState>(), "walks again");
        Assert.That(Stage.Stride.IsDrivingGait, Is.True);
        Assert.That(landings.Count, Is.EqualTo(1), "one landing");
        Assert.That(landings[0], Is.LessThan(Wall.maxSlideSpeed + 1.5f), "lands at the slide speed, not a fall's");
        Assert.That(airborneFrames, Is.LessThanOrEqualTo(3), "straight from the slide to the ground");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(0.05f), "on the ground");
    }
}
