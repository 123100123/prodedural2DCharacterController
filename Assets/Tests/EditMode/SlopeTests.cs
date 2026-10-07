using NUnit.Framework;
using UnityEngine;

// Slope rules: which surfaces are ground to walk on, how a slope scales the gait's speed and steps, how a foot bone turns to
// lie along the ground (mirrored with the root), and how a slope pulls on a running slide.
public class SlopeTests {
    private static Vector2 NormalOfSlope(float degreesRisingRight) {
        float radians = degreesRisingRight * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static GaitSet Gaits() => new GaitSet((GaitProfile.Walk, GaitProfile.Run, GaitProfile.Crouch), GaitTransitionSettings.Default, 0f);

    [Test]
    public void ASlopeUpToTheLimitIsGroundAndSteeperIsAWall() {
        SlopeSettings settings = new SlopeSettings();
        Assert.That(settings.maxSlopeAngle, Is.EqualTo(50f), "the default limit");
        Assert.That(SlopeMath.IsWalkable(Vector2.up, settings.maxSlopeAngle), Is.True, "flat ground");
        Assert.That(SlopeMath.IsSloped(Vector2.up, settings.maxSlopeAngle), Is.False, "flat ground is no slope");
        Assert.That(SlopeMath.IsWalkable(NormalOfSlope(45f), settings.maxSlopeAngle), Is.True, "45° rising right");
        Assert.That(SlopeMath.IsWalkable(NormalOfSlope(-45f), settings.maxSlopeAngle), Is.True, "45° rising left");
        Assert.That(SlopeMath.IsSloped(NormalOfSlope(45f), settings.maxSlopeAngle), Is.True);
        Assert.That(SlopeMath.IsWalkable(NormalOfSlope(55f), settings.maxSlopeAngle), Is.False, "steeper than the limit");
        Assert.That(SlopeMath.IsWalkable(Vector2.left, settings.maxSlopeAngle), Is.False, "a wall");
        Assert.That(SlopeMath.IsWalkable(Vector2.down, settings.maxSlopeAngle), Is.False, "a ceiling");
        Assert.That(SlopeMath.IsSloped(NormalOfSlope(1f), settings.maxSlopeAngle), Is.False, "a slight tilt counts as flat");
    }

    [Test]
    public void TheGradientAndTiltComeFromTheNormal() {
        Assert.That(SlopeMath.Gradient(NormalOfSlope(45f)), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SlopeMath.Gradient(NormalOfSlope(-45f)), Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(SlopeMath.Gradient(Vector2.up), Is.EqualTo(0f));
        Assert.That(SlopeMath.SurfaceTilt(NormalOfSlope(30f)), Is.EqualTo(30f).Within(0.001f));
        Assert.That(SlopeMath.AngleOfGradient(-1f), Is.EqualTo(45f).Within(0.001f));
        Assert.That(SlopeMath.Steepness(1f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SlopeMath.Steepness(3f), Is.EqualTo(1f), "at most the 45° value");
        Assert.That(SlopeMath.Steepness(0f), Is.EqualTo(0f));
    }

    [Test]
    public void FortyFiveDegreesUphillIsNoticeablySlowerAndDownhillALittleFasterAlongTheSurface() {
        SlopeSettings settings = new SlopeSettings();
        SlopeGait up = new SlopeGait(1f, settings);
        float cosine = Mathf.Cos(45f * Mathf.Deg2Rad);
        Assert.That(up.SpeedScale(1f), Is.EqualTo(cosine * settings.uphillSpeedFactor).Within(0.0001f), "moving right up a slope rising right");
        Assert.That(up.SpeedScale(-1f), Is.EqualTo(cosine * settings.downhillSpeedFactor).Within(0.0001f), "moving left down it");
        Assert.That(new SlopeGait(-1f, settings).SpeedScale(-1f), Is.EqualTo(cosine * settings.uphillSpeedFactor).Within(0.0001f), "moving left up a slope rising left");

        // Along the surface: the horizontal speed over the cosine.
        Assert.That(up.SpeedScale(1f) / cosine, Is.LessThan(0.8f), "climbing is noticeably slower along the surface");
        Assert.That(up.SpeedScale(-1f) / cosine, Is.InRange(1f, 1.25f), "descending is a little faster, under control");
        Assert.That(SlopeGait.Flat.SpeedScale(1f), Is.EqualTo(1f), "flat ground changes nothing");

        float gentle = new SlopeGait(Mathf.Tan(20f * Mathf.Deg2Rad), settings).SpeedScale(1f);
        Assert.That(gentle, Is.InRange(up.SpeedScale(1f), 1f), "a gentler slope slows less");
    }

    [Test]
    public void TheGaitOnASlopeHeadsForTheScaledSpeedAndAClimbingRunIsStillARun() {
        SlopeSettings settings = new SlopeSettings();
        GaitSet flat = Gaits();
        GaitSet uphill = flat.OnSlope(new SlopeGait(1f, settings));
        float scale = uphill.Slope.SpeedScale(1f);

        Assert.That(uphill.TargetSpeed((1f, true)), Is.EqualTo(GaitProfile.Run.speed * scale).Within(0.0001f));
        Assert.That(uphill.TargetSpeed((-1f, false)), Is.EqualTo(-GaitProfile.Walk.speed * uphill.Slope.SpeedScale(-1f)).Within(0.0001f));
        Assert.That(uphill.RunningAt(GaitProfile.Run.speed * scale), Is.EqualTo(1f).Within(0.0001f), "the slow climb at full effort is a run");
        Assert.That(uphill.MovingAt(GaitProfile.Walk.speed * scale), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(flat.RunningAt(GaitProfile.Run.speed * scale), Is.LessThan(1f), "on flat ground that speed is no full run");
        Assert.That(flat.TargetSpeed((1f, true)), Is.EqualTo(GaitProfile.Run.speed), "flat ground keeps the speeds");
    }

    // Steps on a slope are bounded by the legs' reach (SlopeReach), not by a share of the flat step: steeper ground
    // shortens them more, flat ground and unknown legs keep them, and only the steps change.
    [Test]
    public void StepsShortenWithSteepnessAndFlatGroundKeepsThem() {
        SlopeSettings settings = new SlopeSettings();
        GaitProfile walk = GaitProfile.Walk;
        LegReach legs = SlopeReachTests.RigLegs;
        float speed = 2f;
        GaitProfile steep = new SlopeGait(1f, settings).Reaching(legs).Bound(walk, speed);
        GaitProfile gentle = new SlopeGait(Mathf.Tan(15f * Mathf.Deg2Rad), settings).Reaching(legs).Bound(walk, speed);
        GaitProfile flat = new SlopeGait(0f, settings).Reaching(legs).Bound(walk, speed);
        float flatStep = GaitTiming.StepLength(speed, walk);

        Assert.That(GaitTiming.StepLength(speed, steep), Is.LessThan(flatStep));
        Assert.That(GaitTiming.StepLength(speed, gentle), Is.InRange(GaitTiming.StepLength(speed, steep), flatStep), "a gentler slope shortens less");
        Assert.That(GaitTiming.StepLength(speed, flat), Is.EqualTo(flatStep), "flat ground keeps the steps");
        Assert.That(new SlopeGait(1f, settings).Bound(walk, speed).maxStepLength, Is.EqualTo(walk.maxStepLength), "unknown legs leave the gait as it is");
        Assert.That(steep.speed, Is.EqualTo(walk.speed), "only the steps change");
        Assert.That(steep.stepLengthPerRootSpeed, Is.EqualTo(walk.stepLengthPerRootSpeed), "the steps are capped, not rescaled");

        float slopeStep = GaitTiming.StepLength(speed, Gaits().OnSlope(new SlopeGait(1f, settings).Reaching(legs)).At(speed, 0f));
        Assert.That(slopeStep, Is.LessThan(flatStep));
    }

    [Test]
    public void TheFootBoneTurnsWithTheSlopeAndMirrorsUnderAMirroredRoot() {
        Vector2 risingRight = NormalOfSlope(45f);
        Assert.That(SlopeMath.AnkleAngle(risingRight, 1), Is.EqualTo(45f).Within(0.001f));
        Assert.That(SlopeMath.AnkleAngle(risingRight, -1), Is.EqualTo(-45f).Within(0.001f), "the mirrored root shows the local angle mirrored");
        Assert.That(SlopeMath.AnkleAngle(NormalOfSlope(-30f), 1), Is.EqualTo(-30f).Within(0.001f));
        Assert.That(SlopeMath.AnkleAngle(Vector2.up, -1), Is.EqualTo(0f).Within(0.001f), "level on flat ground");

        // Under a mirrored root, the local angle shows the foot along the same world slope.
        Matrix4x4 mirroredRoot = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        Vector3 bone = mirroredRoot.MultiplyVector(Quaternion.Euler(0f, 0f, SlopeMath.FootBoneAngle(45f, -1)) * Vector3.right);
        float shownTilt = Mathf.Atan2(bone.y, bone.x) * Mathf.Rad2Deg;
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(shownTilt, 45f)) < 0.01f || Mathf.Abs(Mathf.DeltaAngle(shownTilt, 225f)) < 0.01f, Is.True, $"shown at {shownTilt}");
    }

    [Test]
    public void ASlopePullsASlideDownhillUpToItsTopSpeedAndBrakesItUphill() {
        SlopeSettings settings = new SlopeSettings();
        const float Step = 1f / 60f;
        float downhill = SlopeMath.SlideSpeed(10f, (-1f, 1, Step), settings);
        float uphill = SlopeMath.SlideSpeed(10f, (1f, 1, Step), settings);
        float expected = settings.slideSlopeAcceleration * Mathf.Sin(45f * Mathf.Deg2Rad) * Step;
        Assert.That(downhill, Is.EqualTo(10f + expected).Within(0.0001f), "moving right down a slope rising left");
        Assert.That(uphill, Is.EqualTo(10f - expected).Within(0.0001f));
        Assert.That(SlopeMath.SlideSpeed(10f, (1f, -1, Step), settings), Is.EqualTo(downhill).Within(0.0001f), "moving left down a slope rising right");
        Assert.That(SlopeMath.SlideSpeed(settings.slideMaxSpeed, (-1f, 1, Step), settings), Is.EqualTo(settings.slideMaxSpeed), "no faster than the top speed");
        Assert.That(SlopeMath.SlideSpeed(settings.slideMaxSpeed + 3f, (-1f, 1, Step), settings), Is.EqualTo(settings.slideMaxSpeed + 3f), "a faster slide keeps its speed");
        Assert.That(SlopeMath.SlideSpeed(0.05f, (1f, 1, Step), settings), Is.EqualTo(0f), "brakes to a stop, never backwards");
        Assert.That(SlopeMath.SlideSpeed(10f, (0f, 1, Step), settings), Is.EqualTo(10f), "flat ground leaves it to friction");
    }

    [Test]
    public void TravelAlongTheSurfaceFollowsTheSlope() {
        Vector2 up = SlopeMath.AlongSurface(1f, 1);
        Assert.That(up.x, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.0001f));
        Assert.That(up.y, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.0001f));
        Vector2 leftDown = SlopeMath.AlongSurface(1f, -1);
        Assert.That(leftDown.y, Is.LessThan(0f), "moving left on a slope rising right goes down");
        Assert.That(SlopeMath.AlongSurface(0f, -1), Is.EqualTo(Vector2.left));
    }

    [Test]
    public void TheHillFaceFillsItsOutline() {
        Vector2[] hill = { new Vector2(0f, -1f), new Vector2(0f, 0f), new Vector2(4.5f, 4.5f), new Vector2(10.5f, 4.5f), new Vector2(15f, 0f), new Vector2(15f, -1f) };
        int[] triangles = GroundPolygonMesh.Triangulate(hill);
        Assert.That(triangles.Length, Is.EqualTo(3 * (hill.Length - 2)));
        float area = 0f;
        for (int index = 0; index < triangles.Length; index += 3) {
            Vector2 a = hill[triangles[index]];
            Vector2 b = hill[triangles[index + 1]];
            Vector2 c = hill[triangles[index + 2]];
            area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) / 2f;
        }
        float expected = 15f * 1f + (15f + 6f) / 2f * 4.5f;
        Assert.That(area, Is.EqualTo(expected).Within(0.001f), "the triangles cover the outline exactly once");
        System.Array.Reverse(hill);
        Assert.That(GroundPolygonMesh.Triangulate(hill).Length, Is.EqualTo(3 * (hill.Length - 2)), "either winding");
    }
}
