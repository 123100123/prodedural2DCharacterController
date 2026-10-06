using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Running slides on the real character, with geometry built for each test: crouch pressed at a run drops the body low
// and shoots it forward, slowing to the crouch walk; it passes under a ceiling too low to stand under and stays crouched
// there; a jump out of it keeps its speed; a crouch pressed at a walk only crouches; and a slide off a ledge falls.
// Also writes pictures of the slide pose (Temp/LocomotionScenarios/slide-*.png) to look at.
public class SlideScenarioTests : ScenarioFixture {
    // Clear enough to crouch under, too low to stand under (as in the crouch scenarios).
    private const float CeilingClearance = 4.1f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;
    private bool IsSliding => State is SlideState;
    private SlideSettings Slide => Stage.Character.Parts.Slide;
    private CrouchController Crouch => Stage.Character.Parts.Crouch;
    private LocomotionTelemetry Telemetry => Stage.Inspector.ReadTelemetry();

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // Runs right until the gait is at the run speed.
    private IEnumerator RunFlatOut() {
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(1.5f);
        Assert.That(Stage.Stride.GroundSpeed, Is.GreaterThan(Stage.Stride.Run.speed * 0.9f), "running");
    }

    // Presses crouch (held) and waits for the slide to start.
    private IEnumerator PressCrouchAndSlide() {
        Stage.Press(Key.LeftCtrl);
        yield return WaitUntil(() => IsSliding, 0.2f);
        Assert.That(IsSliding, Is.True, "a crouch pressed at a run slides");
    }

    [UnityTest]
    public IEnumerator RunningThenPressingCrouchSlidesLowAndSlowsDown() {
        float standingHull = Crouch.Hull.height;
        int starts = 0;
        int stops = 0;
        Stage.Character.Events.SlideStarted += _ => starts++;
        Stage.Character.Events.SlideStopped += () => stops++;
        yield return RunFlatOut();
        yield return PressCrouchAndSlide();

        List<float> speeds = new List<float>();
        float lowestHull = float.PositiveInfinity;
        float highestPelvisLate = float.NegativeInfinity;
        float previousX = Body.x;
        bool pictured = false;
        while (IsSliding && speeds.Count < 300) {
            yield return null;
            speeds.Add((Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            lowestHull = Mathf.Min(lowestHull, Crouch.Hull.height);
            if (speeds.Count > 20 && IsSliding) {
                highestPelvisLate = Mathf.Max(highestPelvisLate, Body.y - ScenarioStage.GroundTop);
            }
            if (speeds.Count == 25) {
                WritePicture("slide-pose", Body + new Vector2(0f, -0.5f));
                pictured = true;
            }
        }

        float slideTime = speeds.Count * ScenarioStage.FrameTime;
        float early = (speeds[2] + speeds[3] + speeds[4]) / 3f;
        float late = speeds[speeds.Count - 3];
        Report($"slid {slideTime:F2} s, speed {early:F2} -> {late:F2}, hull {standingHull:F2} -> {lowestHull:F2}, pelvis {highestPelvisLate:F2} above the ground");
        Assert.That(pictured, Is.True, "slid long enough for a picture");
        Assert.That(slideTime, Is.GreaterThan(0.6f), "a slide, not a stumble");
        Assert.That(early, Is.GreaterThan(Stage.Stride.Run.speed), "shoots forward at the boost");
        Assert.That(late, Is.LessThan(early - 4f), "friction slows it");
        Assert.That(lowestHull, Is.LessThan(standingHull - Crouch.Settings.depth * 0.9f), "the crouch hull");
        Assert.That(highestPelvisLate, Is.LessThan(Slide.pelvisHeight + 0.2f), "the body rides low");
        Assert.That(State, Is.InstanceOf<WalkState>(), "ends on its feet");
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "crouched (crouch is held): no pop up");
        Assert.That((starts, stops), Is.EqualTo((1, 1)));

        yield return Hold(1.5f);
        Assert.That(Stage.Stride.IsDrivingGait, Is.True);
        Assert.That(Mathf.Abs(Stage.Stride.GroundSpeed), Is.EqualTo(Stage.Stride.CrouchGait.speed).Within(0.3f), "crouch walks on");
        Assert.That(Stage.Stride.HasLostGround, Is.False);
    }

    [UnityTest]
    public IEnumerator SlidingUnderALowCeilingEndsCrouchedUnderIt() {
        yield return RunFlatOut();
        float ceilingStart = Body.x + 3f;
        const float CeilingWidth = 12f;
        float ceilingBottom = ScenarioStage.GroundTop + CeilingClearance;
        Stage.AddCeiling((ceilingStart, CeilingClearance, CeilingWidth));
        yield return PressCrouchAndSlide();
        Stage.Release(Key.D, Key.LeftShift);

        float highestHullUnder = float.NegativeInfinity;
        bool releasedUnder = false;
        while (IsSliding) {
            yield return null;
            if (Body.x + 0.4f > ceilingStart) {
                highestHullUnder = Mathf.Max(highestHullUnder, Crouch.Hull.yMax);
            }
            if (!releasedUnder && Body.x > ceilingStart + 1.5f) {
                Stage.Release(Key.LeftCtrl);
                releasedUnder = true;
            }
        }
        float endX = Body.x;
        Report($"slid to {endX - ceilingStart:F2} under the ceiling; hull top at most {highestHullUnder - ScenarioStage.GroundTop:F2} (ceiling {CeilingClearance:F2})");
        Assert.That(releasedUnder, Is.True, "crouch was let go under the ceiling");
        Assert.That(endX, Is.GreaterThan(ceilingStart + 2f), "slid on under the ceiling with crouch let go");
        Assert.That(endX, Is.LessThan(ceilingStart + CeilingWidth), "and stopped under it");
        Assert.That(highestHullUnder, Is.LessThan(ceilingBottom), "the hull never met the ceiling");
        Assert.That(State, Is.InstanceOf<WalkState>(), "on its feet");

        yield return Hold(1f);
        Assert.That(Telemetry.IsHeadroomBlocked, Is.True, "no room to stand");
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.9f), "still crouched, not popped up");
        Assert.That(Crouch.Hull.yMax, Is.LessThan(ceilingBottom));
        Assert.That(Body.x, Is.EqualTo(endX).Within(0.6f), "stays where it stopped");
        Assert.That(Stage.Stride.HasLostGround, Is.False);

        Stage.Press(Key.D);
        yield return Hold(8f);
        Assert.That(Body.x, Is.GreaterThan(ceilingStart + CeilingWidth), "crouch walked out: not stuck");
        yield return Hold(1.5f);
        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "stands up in the open");
    }

    [UnityTest]
    public IEnumerator JumpingOutOfASlideKeepsItsMomentum() {
        List<Vector2> launches = new List<Vector2>();
        Stage.Character.Events.Jumped += launch => launches.Add(launch);
        yield return RunFlatOut();
        yield return PressCrouchAndSlide();
        yield return Hold(0.2f);
        Assert.That(IsSliding, Is.True);

        float takeoffX = Body.x;
        Stage.Press(Key.Space);
        yield return WaitUntil(() => State is AirborneState, 0.2f);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "jumps straight out of the slide");
        yield return Hold(0.3f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => State is WalkState, 3f);

        Assert.That(launches.Count, Is.EqualTo(1));
        Vector2 launch = launches[0];
        Report($"launch {launch}, flew {Body.x - takeoffX:F2}");
        Assert.That(launch.x, Is.GreaterThanOrEqualTo(Stage.Stride.Walk.speed), "keeps the slide's speed");
        Assert.That(launch.y, Is.GreaterThan(0f));
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed");
        Assert.That(Body.x - takeoffX, Is.GreaterThan(4f), "carried forward through the air");
    }

    // Taps Ctrl at a run (released within the tap time), slides, jumps out of it and lands with Ctrl long released.
    private IEnumerator SlideOnATapJumpOutAndLand(bool keepsRunHeld) {
        yield return RunFlatOut();
        Stage.Press(Key.LeftCtrl);
        yield return WaitUntil(() => IsSliding, 0.2f);
        Assert.That(IsSliding, Is.True, "the tap slides");
        yield return Hold(0.05f);
        Stage.Release(Key.LeftCtrl);
        yield return Hold(0.1f);
        Assert.That(IsSliding, Is.True, "the slide goes on after the tap's release");

        Stage.Press(Key.Space);
        yield return WaitUntil(() => State is AirborneState, 0.2f);
        Assert.That(State, Is.InstanceOf<AirborneState>(), "jumped out of the slide");
        yield return Hold(0.15f);
        Stage.Release(Key.Space);
        if (!keepsRunHeld) {
            Stage.Release(Key.LeftShift);
        }
        yield return WaitUntil(() => State is WalkState, 3f);
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed");
        yield return Hold(2f);
    }

    [UnityTest]
    public IEnumerator JumpingOutOfATapSlideLandsStandingAndRunsWhileShiftIsHeld() {
        yield return SlideOnATapJumpOutAndLand(true);

        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "not crouching: the slide's tap latched nothing");
        Assert.That(Stage.Stride.GroundSpeed, Is.EqualTo(Stage.Stride.Run.speed).Within(0.4f), "runs");
    }

    [UnityTest]
    public IEnumerator JumpingOutOfATapSlideWithShiftLetGoLandsStandingAndWalks() {
        yield return SlideOnATapJumpOutAndLand(false);

        Assert.That(Telemetry.CrouchShare, Is.LessThan(0.05f), "not crouching");
        Assert.That(Stage.Stride.GroundSpeed, Is.EqualTo(Stage.Stride.Walk.speed).Within(0.4f), "walks");
    }

    [UnityTest]
    public IEnumerator JumpingOutOfASlideWithCrouchStillHeldLandsCrouched() {
        yield return RunFlatOut();
        yield return PressCrouchAndSlide();
        yield return Hold(0.3f);
        Stage.Press(Key.Space);
        yield return WaitUntil(() => State is AirborneState, 0.2f);
        yield return Hold(0.15f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => State is WalkState, 3f);
        yield return Hold(1.5f);

        Assert.That(State, Is.InstanceOf<WalkState>());
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "Ctrl is still held: crouched, as before");
    }

    [UnityTest]
    public IEnumerator ATapWhileWalkingSlowlyStillLatchesCrouch() {
        Stage.Press(Key.D);
        yield return Hold(2f);
        Stage.Press(Key.LeftCtrl);
        yield return Hold(0.05f);
        Stage.Release(Key.LeftCtrl);
        yield return Hold(1.5f);

        Assert.That(IsSliding, Is.False);
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "the tap latched crouch on");
        Assert.That(Stage.Stride.GroundSpeed, Is.EqualTo(Stage.Stride.CrouchGait.speed).Within(0.25f), "crouch walking");
    }

    [UnityTest]
    public IEnumerator PressingLeftAltWhileWalkingRollsWithTheDefaultSettings() {
        int rolls = 0;
        Stage.Character.Events.Rolled += _ => rolls++;
        Stage.Press(Key.D);
        yield return Hold(1.5f);
        Stage.Press(Key.LeftAlt);
        yield return WaitUntil(() => State is RollState, 0.3f);
        Assert.That(State, Is.InstanceOf<RollState>(), "Left Alt rolls: the roll is on by default");
        Stage.Release(Key.LeftAlt);
        yield return Hold(1.5f);
        Assert.That(rolls, Is.EqualTo(1));
        Assert.That(State, Is.InstanceOf<WalkState>(), "back on its feet");
    }

    [UnityTest]
    public IEnumerator PressingCrouchWhileWalkingSlowlyOnlyCrouches() {
        int starts = 0;
        Stage.Character.Events.SlideStarted += _ => starts++;
        Stage.Press(Key.D);
        yield return Hold(2f);
        Assert.That(Stage.Stride.GroundSpeed, Is.EqualTo(Stage.Stride.Walk.speed).Within(0.3f), "walking");

        Stage.Press(Key.LeftCtrl);
        bool slid = false;
        for (int frame = 0; frame < 120; frame++) {
            yield return null;
            slid |= IsSliding;
        }
        Assert.That(slid, Is.False, "no slide at a walk");
        Assert.That(starts, Is.Zero);
        Assert.That(State, Is.InstanceOf<WalkState>());
        Assert.That(Telemetry.CrouchShare, Is.GreaterThan(0.95f), "crouched as before");
        Assert.That(Stage.Stride.GroundSpeed, Is.EqualTo(Stage.Stride.CrouchGait.speed).Within(0.25f), "at the crouch walk speed");
    }

    [UnityTest]
    public IEnumerator SlidingOffALedgeFallsIntoTheAir() {
        const float PlatformHeight = 3f;
        const float PlatformAhead = 9f;
        float edge = Body.x + PlatformAhead;
        Stage.PlaceOnPlatform((PlatformHeight, PlatformAhead));
        yield return Hold(1f);

        Stage.Press(Key.D, Key.LeftShift);
        yield return WaitUntil(() => Body.x >= edge - 4.5f, 3f);
        yield return PressCrouchAndSlide();
        yield return WaitUntil(() => !IsSliding, 2f);
        float leftAt = Body.x;
        Assert.That(State, Is.InstanceOf<AirborneState>(), "slid off the ledge into a fall");
        Assert.That(leftAt, Is.GreaterThan(edge - 0.5f), "at the edge");

        Stage.Release(Key.LeftCtrl, Key.D, Key.LeftShift);
        yield return WaitUntil(() => State is WalkState, 3f);
        Report($"left the slide at {leftAt - edge:F2} from the edge, landed at {Body.x - edge:F2}");
        Assert.That(State, Is.InstanceOf<WalkState>(), "landed below");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(ScenarioStage.GroundTop).Within(0.1f), "on the track below");
    }

    // Aiming while sliding: the pistol points and fires, the slide carries on low. Pictures of aiming ahead and behind.
    [UnityTest]
    public IEnumerator AimingWhileSlidingPointsThePistolAndKeepsSliding() {
        int shots = 0;
        Stage.Character.Events.Fired += _ => shots++;
        yield return RunFlatOut();
        yield return PressCrouchAndSlide();

        float lowest = float.PositiveInfinity;
        for (int frame = 0; frame < 40 && IsSliding; frame++) {
            Stage.AimAndFire(Body + new Vector2(6f, 2f), frame % 8 < 4);
            yield return null;
            if (frame > 15) {
                lowest = Mathf.Min(lowest, Body.y - ScenarioStage.GroundTop);
            }
            if (frame == 22) {
                WritePicture("slide-aim-ahead", Body + new Vector2(0f, -0.5f));
            }
        }
        Assert.That(IsSliding, Is.True, "still sliding while aiming");
        Assert.That(shots, Is.GreaterThan(0), "fires while sliding");
        Assert.That(lowest, Is.LessThan(Slide.pelvisHeight + 0.2f), "the low slide body holds");

        bool facedAlongTheSlide = true;
        for (int frame = 0; frame < 12 && IsSliding; frame++) {
            Stage.AimAt(Body + new Vector2(-6f, 1.5f));
            yield return null;
            facedAlongTheSlide &= Stage.Character.transform.localScale.x > 0f;
        }
        WritePicture("slide-aim-behind", Body + new Vector2(0f, -0.5f));
        Stage.StopAiming();
        Assert.That(facedAlongTheSlide, Is.True, "a pointer behind does not turn the sliding character round");
    }

    private static void WritePicture(string name, Vector2 center) {
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), Capture(center, 3.2f).EncodeToPNG());
    }

    private static Texture2D Capture(Vector2 center, float orthographicSize) {
        GameObject host = new GameObject("pictureCamera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = new Vector3(center.x, center.y, -10f);
        camera.orthographicSize = orthographicSize;
        camera.aspect = 16f / 9f;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(host);
        return picture;
    }
}
