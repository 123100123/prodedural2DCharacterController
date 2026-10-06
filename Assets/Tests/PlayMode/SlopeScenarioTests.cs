using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The character on a 45° hill built for each test (Hill: up-ramp, flat top, down-ramp; one PolygonCollider2D): walking and
// running over it both ways, crouch-walking up, sliding down and up, rolling on it, jumping from it and landing back on
// it, and jumping at it without catching it as a wall. Planted feet stand on the surface with the foot bone turned along
// it. Writes pictures of the character on the ramps (Temp/LocomotionScenarios/slope-*.png) to look at.
public class SlopeScenarioTests : ScenarioFixture {
    private const float HillHeight = 4.5f;
    private const float HillAngle = 45f;
    private const float HillTop = 6f;
    // How far in from a ramp's ends feet and bodies are measured (the corners are neither slope nor flat).
    private const float RampMargin = 0.7f;
    // A foot's turn is measured once it has been planted this long (it turns onto the ground late in its step and settles
    // within SlopeSettings.ankleBlendTime of touching down).
    private const float SettledFootTime = 0.15f;
    private const float FootOnSurfaceTolerance = 0.05f;
    private const float AnkleTolerance = 4f;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;
    private SlopeSettings Slope => Stage.Character.Parts.Slope;

    // Builds the hill a little ahead of the character (on its right) and returns it.
    private Hill HillAhead(float distance) => Stage.AddHill((Body.x + distance, HillHeight, HillAngle, HillTop));

    // Stands the character on the hill at x (on a ramp or the top), settled.
    private IEnumerator StandOnHillAt(Hill hill, float x, int facing = 1) {
        float ground = hill.SurfaceY(x);
        Stage.Character.transform.position += new Vector3(x - Body.x, ground - ScenarioStage.GroundTop + 0.05f, 0f);
        Stage.Stride.TrySettleStance((x, facing, ground));
        Stage.Stride.BeginGroundedMotion(Vector2.zero);
        yield return Hold(0.8f);
    }

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // What a traverse measured: the worst foot and ankle on the ramps, the body's clearance, and the speeds.
    private sealed class Traverse {
        private readonly Hill hill;
        private readonly StrideController stride;
        private (float front, float back) planted;
        private (float sum, int frames) upSpeed;
        private (float sum, int frames) downSpeed;
        private (float sum, int frames) flatSpeed;

        public Traverse(Hill hill, StrideController stride) {
            this.hill = hill;
            this.stride = stride;
        }

        public float WorstFootOffSurface { get; private set; }
        public float WorstFootReach { get; private set; }
        private string worstReachAt = "";
        private string slopeRead = "";
        public float WorstAnkleError { get; private set; }
        public int SettledRampFeet { get; private set; }
        public float LowestBodyClearance { get; private set; } = float.PositiveInfinity;
        public bool WentAirborne { get; private set; }
        public float UphillSpeed => upSpeed.frames > 0 ? upSpeed.sum / upSpeed.frames : 0f;
        public float DownhillSpeed => downSpeed.frames > 0 ? downSpeed.sum / downSpeed.frames : 0f;
        public float FlatSpeed => flatSpeed.frames > 0 ? flatSpeed.sum / flatSpeed.frames : 0f;

        public void Sample(ICharacterState state, float bodySpeedX) {
            Vector2 body = stride.transform.position;
            WentAirborne |= state is AirborneState;
            if (!(state is WalkState)) {
                return;
            }
            LowestBodyClearance = Mathf.Min(LowestBodyClearance, hill.HeightAbove(body));
            planted.front = Foot(stride.FrontLeg, planted.front);
            planted.back = Foot(stride.BackLeg, planted.back);

            float gradient = hill.GradientAt(body.x);
            float speed = Mathf.Abs(bodySpeedX);
            if (hill.IsMidRamp(body.x, RampMargin)) {
                if (slopeRead.Length < 400 && Time.frameCount % 10 == 0) {
                    slopeRead += $" [g {stride.SlopeGradient:F2} share {stride.SlopeShare:F2} v {stride.GroundSpeed:F2} gait {stride.CurrentGait.stepLengthPerRootSpeed:F2}]";
                }
                bool uphill = gradient * bodySpeedX > 0f;
                upSpeed = uphill ? (upSpeed.sum + speed, upSpeed.frames + 1) : upSpeed;
                downSpeed = uphill ? downSpeed : (downSpeed.sum + speed, downSpeed.frames + 1);
            } else if (body.x < hill.FootX - 1f || body.x > hill.EndX + 1f) {
                flatSpeed = (flatSpeed.sum + speed, flatSpeed.frames + 1);
            }
        }

        private float Foot(LegStepper leg, float plantedTime) {
            if (!leg.IsGrounded) {
                return 0f;
            }
            plantedTime += ScenarioStage.FrameTime;
            Vector2 target = leg.FootTarget;
            if (!hill.IsMidRamp(target.x, RampMargin)) {
                return plantedTime;
            }
            WorstFootOffSurface = Mathf.Max(WorstFootOffSurface, Mathf.Abs(hill.NormalDistance(target)));
            float reach = plantedTime < 2.5f * ScenarioStage.FrameTime ? 0f : Vector2.Distance(leg.FootBone.position, target);
            if (reach > WorstFootReach) {
                WorstFootReach = reach;
                Vector2 hip = leg.HipPosition;
                worstReachAt = $"foot {target - hip} from the hip (leg {leg.LegLength:F2}), gradient {hill.GradientAt(target.x):F2}, planted {plantedTime:F2} s, body {hill.HeightAbove(stride.transform.position):F2} above";
            }
            if (plantedTime < SettledFootTime) {
                return plantedTime;
            }
            SettledRampFeet++;
            float slopeAngle = Mathf.Atan(hill.GradientAt(target.x)) * Mathf.Rad2Deg;
            WorstAnkleError = Mathf.Max(WorstAnkleError, Mathf.Abs(Mathf.DeltaAngle(FootBoneTilt(leg), slopeAngle)));
            return plantedTime;
        }

        public string Describe() {
            return $"feet on ramps {SettledRampFeet}: worst off the surface {WorstFootOffSurface:F3}, worst reach {WorstFootReach:F3}, " +
                   $"worst ankle error {WorstAnkleError:F1}°; lowest body {LowestBodyClearance:F2} above the ground; " +
                   $"slope read mid-ramp {slopeRead}; speed flat {FlatSpeed:F2}, uphill {UphillSpeed:F2}, downhill {DownhillSpeed:F2}; airborne {WentAirborne}; worst reach at {worstReachAt}";
        }
    }

    // The tilt of the foot bone as shown in the world, folded into -90..90 (a mirrored root shows its x axis pointing left).
    private static float FootBoneTilt(LegStepper leg) {
        Vector3 axis = leg.FootBone.localToWorldMatrix.MultiplyVector(Vector3.right);
        float tilt = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        return tilt > 90f ? tilt - 180f : tilt < -90f ? tilt + 180f : tilt;
    }

    private IEnumerator CrossTheHill((Hill hill, Traverse traverse) course, (int direction, float maxSeconds, string picture) walk, params Key[] keys) {
        Stage.Press(keys);
        Stage.Press(walk.direction > 0 ? Key.D : Key.A);
        float goalX = walk.direction > 0 ? course.hill.EndX + 1.5f : course.hill.FootX - 1.5f;
        float previousX = Body.x;
        bool pictured = walk.picture == null;
        float climbMidX = walk.direction > 0 ? (course.hill.FootX + course.hill.TopStartX) / 2f : (course.hill.TopEndX + course.hill.EndX) / 2f;
        for (float elapsed = 0f; elapsed < walk.maxSeconds && (Body.x - goalX) * walk.direction < 0f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            // The feet are measured after the IK has solved this frame.
            yield return new WaitForEndOfFrame();
            course.traverse.Sample(State, (Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            if (!pictured && (Body.x - climbMidX) * walk.direction >= 0f) {
                WritePicture(walk.picture, Body + new Vector2(0f, -0.8f));
                pictured = true;
            }
        }
        Stage.ReleaseAll();
        Report(course.traverse.Describe());
        Assert.That((Body.x - goalX) * walk.direction, Is.GreaterThanOrEqualTo(0f), "crossed the hill without stalling");
    }

    // fewestFeet: how many settled planted-foot frames on the ramps the traverse must have measured (running feet are
    // down briefly).
    private void AssertFeetAndAnkles(Traverse traverse, int fewestFeet = 20) {
        Assert.That(traverse.SettledRampFeet, Is.GreaterThan(fewestFeet), "feet stood on the ramps");
        Assert.That(traverse.WorstFootOffSurface, Is.LessThanOrEqualTo(FootOnSurfaceTolerance), "planted feet stand on the slope");
        Assert.That(traverse.WorstFootReach, Is.LessThanOrEqualTo(PlantedFootTolerance), "the legs reach their planted feet");
        Assert.That(traverse.WorstAnkleError, Is.LessThanOrEqualTo(AnkleTolerance), "planted foot bones lie along the slope");
        Assert.That(traverse.LowestBodyClearance, Is.GreaterThan(1.2f), "the body stays well above the ground");
    }

    [UnityTest]
    public IEnumerator WalkingOverTheHillClimbsSlowerDescendsFasterAndKeepsTheFeetOnTheSlope() {
        Hill hill = HillAhead(4f);
        Traverse traverse = new Traverse(hill, Stage.Stride);
        yield return CrossTheHill((hill, traverse), (1, 16f, "slope-walk-up"));
        Assert.That(traverse.WentAirborne, Is.False, "walks the whole hill on its feet");
        AssertFeetAndAnkles(traverse);
        Assert.That(traverse.UphillSpeed, Is.LessThan(0.75f * Stage.Stride.Walk.speed), "slower climbing");
        Assert.That(traverse.DownhillSpeed, Is.GreaterThan(traverse.UphillSpeed + 0.4f), "faster descending than climbing");
    }

    // Facing left the root is mirrored: the feet and the foot bones are measured the same.
    [UnityTest]
    public IEnumerator WalkingLeftOverTheHillTurnsTheMirroredFeetAlongTheSlope() {
        Hill hill = HillAhead(4f);
        yield return StandOnHillAt(hill, hill.EndX + 3f, -1);
        Traverse traverse = new Traverse(hill, Stage.Stride);
        yield return CrossTheHill((hill, traverse), (-1, 16f, "slope-walk-left"));
        Assert.That(Stage.Character.transform.localScale.x, Is.LessThan(0f), "faced left");
        Assert.That(traverse.WentAirborne, Is.False, "walks the whole hill on its feet");
        AssertFeetAndAnkles(traverse);
    }

    [UnityTest]
    public IEnumerator RunningOverTheHillKeepsTheFeetOnTheSlopeAndNeverFalls() {
        Hill hill = HillAhead(8f);
        Traverse traverse = new Traverse(hill, Stage.Stride);
        yield return CrossTheHill((hill, traverse), (1, 10f, "slope-run-up"), Key.LeftShift);
        Assert.That(traverse.WentAirborne, Is.False, "runs the whole hill on its feet");
        AssertFeetAndAnkles(traverse, 3);
        Assert.That(traverse.UphillSpeed, Is.LessThan(0.75f * Stage.Stride.Run.speed), "slower climbing");
        Assert.That(traverse.UphillSpeed, Is.GreaterThan(Stage.Stride.Walk.speed), "still a run up the hill");
        Assert.That(traverse.DownhillSpeed, Is.GreaterThan(traverse.UphillSpeed + 1f), "faster descending than climbing");
    }

    [UnityTest]
    public IEnumerator CrouchWalkingUpTheHillReachesTheTop() {
        Hill hill = HillAhead(3f);
        Traverse traverse = new Traverse(hill, Stage.Stride);
        Stage.Press(Key.LeftCtrl);
        yield return Hold(0.5f);
        Assert.That(Stage.Character.Parts.Crouch.Share, Is.GreaterThan(0.9f), "crouched");
        Stage.Press(Key.D);
        float topX = hill.TopStartX + 1f;
        float previousX = Body.x;
        bool blocked = false;
        for (float elapsed = 0f; elapsed < 14f && Body.x < topX; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            yield return new WaitForEndOfFrame();
            traverse.Sample(State, (Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            blocked |= Stage.Character.Parts.Crouch.IsHeadroomBlocked && hill.IsMidRamp(Body.x, RampMargin);
            if (Mathf.Abs(Body.x - (hill.FootX + hill.TopStartX) / 2f) < 0.05f) {
                WritePicture("slope-crouch-up", Body + new Vector2(0f, -0.8f));
            }
        }
        Stage.ReleaseAll();
        Report(traverse.Describe());
        Assert.That(Body.x, Is.GreaterThanOrEqualTo(topX), "reached the top crouched");
        Assert.That(traverse.WentAirborne, Is.False);
        Assert.That(blocked, Is.False, "the ramp under and ahead is not a ceiling");
        Assert.That(traverse.WorstFootOffSurface, Is.LessThanOrEqualTo(FootOnSurfaceTolerance), "planted feet stand on the slope");
    }

    [UnityTest]
    public IEnumerator ASlideDownTheRampSpeedsUp() {
        Hill hill = HillAhead(4f);
        // From a run off the top onto the down-ramp.
        yield return StandOnHillAt(hill, hill.TopStartX + 0.5f);
        Stage.Press(Key.D, Key.LeftShift);
        yield return WaitUntil(() => Body.x > hill.TopEndX + 0.6f, 4f);
        Stage.Press(Key.LeftCtrl);
        yield return WaitUntil(() => State is SlideState, 0.3f);
        Assert.That(State is SlideState, Is.True, "slides down the ramp");
        float startSpeed = Stage.Character.Parts.SlideState.Speed;
        float fastest = startSpeed;
        float lowestClearance = float.PositiveInfinity;
        for (int frame = 0; frame < 30 && State is SlideState && Body.x < hill.EndX - 0.3f; frame++) {
            yield return null;
            fastest = Mathf.Max(fastest, Stage.Character.Parts.SlideState.Speed);
            lowestClearance = Mathf.Min(lowestClearance, hill.HeightAbove(Body));
            if (frame == 12) {
                WritePicture("slope-slide-down", Body + new Vector2(0f, -0.4f));
            }
        }
        Report($"slide down: {startSpeed:F2} -> fastest {fastest:F2}, lowest pelvis {lowestClearance:F2} above the ground");
        Assert.That(fastest, Is.GreaterThan(startSpeed + 0.3f), "gains speed down the slope");
        Assert.That(lowestClearance, Is.GreaterThan(0.3f), "the sliding body stays above the ramp");
        Stage.ReleaseAll();
        yield return WaitUntil(() => State is WalkState, 3f);
        Assert.That(State is WalkState, Is.True, "gets up at the bottom");
    }

    [UnityTest]
    public IEnumerator ASlideUpTheRampSlowsAndEndsOnTheRamp() {
        Hill hill = HillAhead(12f);
        Stage.Press(Key.D, Key.LeftShift);
        string approach = "";
        for (float elapsed = 0f; elapsed < 5f && Body.x <= hill.FootX - 0.4f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            if (Body.x > hill.FootX - 3f) {
                approach += $" {State.GetType().Name[0]}{Body.x - hill.FootX:F2}/{hill.HeightAbove(Body):F2} st{Stage.Stride.StanceHeight - hill.BaseY:F2} sh{Stage.Stride.SlopeShare:F2} g{Stage.Stride.SlopeGradient:F2}";
            }
        }
        Report("approach:" + approach);
        Stage.Press(Key.LeftCtrl);
        yield return WaitUntil(() => State is SlideState, 0.3f);
        Assert.That(State is SlideState, Is.True, "slides at the foot of the ramp");
        Stage.Release(Key.D, Key.LeftShift);
        float startSpeed = Stage.Character.Parts.SlideState.Speed;
        float slideTime = 0f;
        bool fell = false;
        string trace = $"{Body.x - hill.FootX:F2}/{hill.HeightAbove(Body):F2}";
        while (State is SlideState && slideTime < 3f) {
            yield return null;
            slideTime += ScenarioStage.FrameTime;
            fell |= State is AirborneState;
            trace += $" {State.GetType().Name[0]}{Body.x - hill.FootX:F2}/{hill.HeightAbove(Body):F2}";
        }
        for (int frame = 0; frame < 18; frame++) {
            yield return null;
            fell |= State is AirborneState;
            trace += $" {State.GetType().Name[0]}";
        }
        Report(trace);
        Report($"slide up: from {startSpeed:F2}, slid {slideTime:F2} s up to x {Body.x - hill.FootX:F2} past the foot, ended in {State.GetType().Name}");
        Assert.That(slideTime, Is.LessThan(1.2f), "the climb stops the slide soon");
        Assert.That(Body.x, Is.LessThan(hill.TopStartX), "ends on the ramp, short of the top");
        Assert.That(Body.x, Is.GreaterThan(hill.FootX + 0.8f), "slid some way up");
        Assert.That(fell, Is.False, "ends on its feet");
        Stage.ReleaseAll();
    }

    [UnityTest]
    public IEnumerator RollingUpAndDownTheRampsStaysOnTheSurface() {
        Hill hill = HillAhead(4f);
        foreach ((float startX, string picture) roll in new[] { (hill.FootX + 0.8f, "slope-roll-up"), (hill.TopEndX + 0.6f, "slope-roll-down") }) {
            yield return StandOnHillAt(hill, roll.startX);
            Stage.Press(Key.D);
            yield return Hold(0.2f);
            Stage.Press(Key.LeftAlt);
            yield return WaitUntil(() => State is RollState, 0.3f);
            Stage.Release(Key.LeftAlt);
            Assert.That(State is RollState, Is.True, "rolls on the ramp");
            float startX = Body.x;
            float lowest = float.PositiveInfinity;
            int frames = 0;
            bool fell = false;
            while (State is RollState && frames < 180) {
                yield return null;
                frames++;
                fell |= State is AirborneState;
                lowest = Mathf.Min(lowest, hill.HeightAbove(Body));
                if (frames == 25) {
                    WritePicture(roll.picture, Body + new Vector2(0f, -0.3f));
                }
            }
            yield return Hold(0.4f);
            fell |= State is AirborneState;
            Stage.ReleaseAll();
            Report($"{roll.picture}: rolled {Body.x - startX:F2} in {frames} frames, pivot at least {lowest:F2} above the ground, now {State.GetType().Name}");
            Assert.That(fell, Is.False, "the roll ends on its feet");
            Assert.That(State is WalkState, Is.True, "walks on after the roll");
            Assert.That(Body.x - startX, Is.GreaterThan(1.5f), "rolled forward along the ramp");
            Assert.That(lowest, Is.GreaterThan(0.25f), "the rolling body stays above the ramp");
            yield return Hold(0.5f);
        }
    }

    [UnityTest]
    public IEnumerator AJumpFromTheRampLandsBackOnItAtTheStandingHeight() {
        Hill hill = HillAhead(4f);
        float x = (hill.FootX + hill.TopStartX) / 2f;
        yield return StandOnHillAt(hill, x);
        float standing = hill.HeightAbove(Body);
        int landings = 0;
        Stage.Character.Events.Landed += _ => landings++;
        Stage.Press(Key.Space);
        yield return WaitUntil(() => State is AirborneState, 0.6f);
        Assert.That(State is AirborneState, Is.True, "jumped");
        Stage.Release(Key.Space);
        yield return WaitUntil(() => State is WalkState, 3f);
        yield return Hold(1.2f);
        float landed = hill.HeightAbove(Body);
        Report($"standing {standing:F3} above the ramp before, {landed:F3} after; at x {Body.x - x:F2} from the start; landings {landings}");
        Assert.That(landings, Is.EqualTo(1));
        Assert.That(hill.IsMidRamp(Body.x, 0.3f), Is.True, "landed back on the ramp");
        Assert.That(landed, Is.EqualTo(standing).Within(0.1f), "settles at the standing height over the slope");
        Assert.That(Mathf.Abs(hill.NormalDistance(Stage.Stride.FrontLeg.FootTarget)), Is.LessThanOrEqualTo(FootOnSurfaceTolerance), "front foot on the ramp");
        Assert.That(Mathf.Abs(hill.NormalDistance(Stage.Stride.BackLeg.FootTarget)), Is.LessThanOrEqualTo(FootOnSurfaceTolerance), "back foot on the ramp");
        Assert.That(standing, Is.GreaterThan(1.6f).And.LessThan(2.5f), "stands on the slope at about its standing height");
    }

    [UnityTest]
    public IEnumerator JumpingAtTheRampIsNotAWallSlide() {
        Hill hill = HillAhead(2.5f);
        bool slid = false;
        bool climbed = false;
        Stage.Press(Key.D);
        yield return WaitUntil(() => Body.x > hill.FootX - 1.6f, 2f);
        Stage.Press(Key.Space);
        for (float elapsed = 0f; elapsed < 2.5f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            slid |= State is WallSlideState;
            climbed |= State is ClimbState;
        }
        Stage.ReleaseAll();
        yield return Hold(0.5f);
        Report($"after the jump at the ramp: {State.GetType().Name} at {Body.x - hill.FootX:F2} past the foot");
        Assert.That(slid, Is.False, "a walkable slope is no wall to slide down");
        Assert.That(climbed, Is.False, "nor a ledge");
        Assert.That(State is WalkState, Is.True, "lands on the ramp and walks on");
    }

    // The character, and a close-up of its feet (name-feet.png).
    private void WritePicture(string name, Vector2 center) {
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), Capture(center, 3.2f).EncodeToPNG());
        Vector2 feet = (Stage.Stride.FrontLeg.FootTarget + Stage.Stride.BackLeg.FootTarget) / 2f;
        File.WriteAllBytes(Path.Combine(folder, name + "-feet.png"), Capture(feet + new Vector2(0f, 0.5f), 1.1f).EncodeToPNG());
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
