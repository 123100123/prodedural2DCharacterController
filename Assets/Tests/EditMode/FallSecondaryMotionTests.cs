using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FallSecondaryMotionTests {
    private const float Tick = 1f / 60f;
    private static readonly FallSecondarySettings Settings = new FallSecondarySettings();

    // A fall simulated frame by frame at a steady downward speed: the outputs of each frame.
    private static List<FallSecondaryMotion.Output> Fall(int seed, float seconds, float verticalSpeed = -20f, float aimSuppression = 0f, float landingPrep = 0f) {
        FallSecondaryMotion motion = new FallSecondaryMotion(Settings);
        motion.Begin(seed);
        List<FallSecondaryMotion.Output> outputs = new List<FallSecondaryMotion.Output>();
        for (float time = 0f; time < seconds; time += Tick) {
            outputs.Add(motion.Tick(new FallSecondaryMotion.Input((time, verticalSpeed, 0f), (landingPrep, aimSuppression), Tick)));
        }
        return outputs;
    }

    [Test]
    public void AShortFallIsLeftAlone() {
        foreach (FallSecondaryMotion.Output output in Fall(1, Settings.onsetTime - 0.01f)) {
            Assert.That(output.Strength, Is.EqualTo(0f));
            Assert.That(output.ArmFront, Is.EqualTo(0f));
            Assert.That(output.ArmBack, Is.EqualTo(0f));
            Assert.That(output.LeadFoot, Is.EqualTo(Vector2.zero));
            Assert.That(output.ForwardPitch, Is.EqualTo(0f));
        }
    }

    [Test]
    public void ALongFallGraduallyBuildsUpToFullStrength() {
        List<FallSecondaryMotion.Output> fall = Fall(1, 4f);
        float previous = 0f;
        foreach (FallSecondaryMotion.Output output in fall) {
            Assert.That(output.Strength, Is.GreaterThanOrEqualTo(previous - 0.0001f), "never weakens as the fall goes on");
            previous = output.Strength;
        }
        Assert.That(fall[fall.Count - 1].Strength, Is.EqualTo(1f).Within(0.001f));
        Assert.That(fall[(int)(1.1f / Tick)].Strength, Is.InRange(0.05f, 0.95f), "partway through the build-up");
    }

    [Test]
    public void TheStrengthRisesContinuously() {
        float previous = 0f;
        for (float time = 0f; time < 3f; time += 0.005f) {
            float strength = FallSecondaryMotion.Strength(time, -20f, Settings);
            Assert.That(strength - previous, Is.LessThan(0.02f), $"no jump at {time}");
            previous = strength;
        }
    }

    [Test]
    public void ASlowLongFallIsWeakerThanAFastOne() {
        float slow = FallSecondaryMotion.Strength(3f, -3f, Settings);
        float fast = FallSecondaryMotion.Strength(3f, -20f, Settings);
        Assert.That(slow, Is.EqualTo(Settings.slowFallStrength).Within(0.001f));
        Assert.That(fast, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ItIsDeterministicForASeedAndDifferentForAnother() {
        List<FallSecondaryMotion.Output> a = Fall(7, 3f);
        List<FallSecondaryMotion.Output> b = Fall(7, 3f);
        List<FallSecondaryMotion.Output> other = Fall(8, 3f);
        float difference = 0f;
        for (int frame = 0; frame < a.Count; frame++) {
            Assert.That(b[frame].ArmFront, Is.EqualTo(a[frame].ArmFront), "the same seed repeats exactly");
            difference += Mathf.Abs(other[frame].ArmFront - a[frame].ArmFront);
        }
        Assert.That(difference, Is.GreaterThan(100f), "another fall flails differently");
    }

    // The flail is built from sine waves, not a random value per frame: its second difference stays inside what the
    // fastest wave allows. Per-frame random jitter of a few degrees would exceed it many times over.
    [Test]
    public void TheMotionIsSmoothFrameToFrameAtFullStrength() {
        List<FallSecondaryMotion.Output> fall = Fall(3, 170f);
        int start = (int)(3f / Tick);
        float worstSecondDifference = 0f;
        float worstStep = 0f;
        for (int frame = start + 2; frame < fall.Count; frame++) {
            float step = fall[frame].ArmFront - fall[frame - 1].ArmFront;
            float previousStep = fall[frame - 1].ArmFront - fall[frame - 2].ArmFront;
            worstStep = Mathf.Max(worstStep, Mathf.Abs(step));
            worstSecondDifference = Mathf.Max(worstSecondDifference, Mathf.Abs(step - previousStep));
        }
        Assert.That(worstStep, Is.LessThan(40f), "largest change in one frame");
        Assert.That(worstSecondDifference, Is.LessThan(10f), "no jitter");
    }

    [Test]
    public void TheLegsAndElbowsAreSmoothToo() {
        List<FallSecondaryMotion.Output> fall = Fall(5, 20f);
        int start = (int)(3f / Tick);
        for (int frame = start + 2; frame < fall.Count; frame++) {
            Vector2 acceleration = (fall[frame].LeadFoot - fall[frame - 1].LeadFoot) - (fall[frame - 1].LeadFoot - fall[frame - 2].LeadFoot);
            Assert.That(acceleration.magnitude, Is.LessThan(0.05f), "feet");
            float elbow = (fall[frame].ElbowFront - fall[frame - 1].ElbowFront) - (fall[frame - 1].ElbowFront - fall[frame - 2].ElbowFront);
            Assert.That(Mathf.Abs(elbow), Is.LessThan(0.05f), "elbows");
        }
    }

    [Test]
    public void TheTwoArmsDoNotMoveInStep() {
        List<FallSecondaryMotion.Output> fall = Fall(11, 60f);
        int start = (int)(3f / Tick);
        double sumFront = 0, sumBack = 0, sumProduct = 0, sumFrontSquared = 0, sumBackSquared = 0;
        int count = 0;
        for (int frame = start; frame < fall.Count; frame++) {
            double front = fall[frame].ArmFront;
            double back = fall[frame].ArmBack;
            sumFront += front;
            sumBack += back;
            sumProduct += front * back;
            sumFrontSquared += front * front;
            sumBackSquared += back * back;
            count++;
        }
        double covariance = sumProduct / count - (sumFront / count) * (sumBack / count);
        double correlation = covariance / System.Math.Sqrt((sumFrontSquared / count - System.Math.Pow(sumFront / count, 2)) * (sumBackSquared / count - System.Math.Pow(sumBack / count, 2)));
        Assert.That(System.Math.Abs(correlation), Is.LessThan(0.6), "the arms swing out of step");
    }

    [Test]
    public void TheArmsSwingWidelyAtFullStrength() {
        List<FallSecondaryMotion.Output> fall = Fall(2, 60f);
        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        for (int frame = (int)(3f / Tick); frame < fall.Count; frame++) {
            lowest = Mathf.Min(lowest, fall[frame].ArmFront);
            highest = Mathf.Max(highest, fall[frame].ArmFront);
        }
        Assert.That(highest - lowest, Is.GreaterThan(Settings.armAmplitude), "a big arc");
    }

    [Test]
    public void AimingGivesTheArmsBackAndTheMotionReturnsWhenAimingStops() {
        List<FallSecondaryMotion.Output> aiming = Fall(1, 4f, -20f, 1f);
        foreach (FallSecondaryMotion.Output output in aiming) {
            Assert.That(output.Strength, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.ArmFront, Is.EqualTo(0f).Within(0.0001f));
        }
        Assert.That(Fall(1, 4f, -20f, 0f)[200].Strength, Is.GreaterThan(0.5f));
    }

    [Test]
    public void ApproachingTheGroundCalmsTheMotion() {
        float open = Fall(1, 4f, -20f, 0f, 0f)[200].Strength;
        float nearGround = Fall(1, 4f, -20f, 0f, 1f)[200].Strength;
        Assert.That(nearGround, Is.EqualTo(open * (1f - Settings.landingDamping)).Within(0.001f));
    }

    [Test]
    public void TheArmsRiseWithTheFallSpeedAndTrailForwardAcceleration() {
        FallSecondaryMotion motion = new FallSecondaryMotion(Settings);
        motion.Begin(1);
        for (float time = 0f; time < 3f; time += Tick) {
            motion.Tick(new FallSecondaryMotion.Input((time, -20f, 0f), (0f, 0f), Tick));
        }
        // Average the flail away by comparing the same moment with and without acceleration: only the lag differs.
        FallSecondaryMotion a = new FallSecondaryMotion(Settings);
        FallSecondaryMotion b = new FallSecondaryMotion(Settings);
        a.Begin(1);
        b.Begin(1);
        FallSecondaryMotion.Output steady = default;
        FallSecondaryMotion.Output accelerating = default;
        for (float time = 0f; time < 3f; time += Tick) {
            steady = a.Tick(new FallSecondaryMotion.Input((time, -20f, 0f), (0f, 0f), Tick));
            accelerating = b.Tick(new FallSecondaryMotion.Input((time, -20f, 5f), (0f, 0f), Tick));
        }
        Assert.That(accelerating.ArmFront, Is.LessThan(steady.ArmFront), "arms trail behind a forward acceleration");
    }

    [Test]
    public void TheHashIsDeterministicAndSpreadsAcrossZeroToOne() {
        Assert.That(FallSecondaryMotion.Hash01(5, 2), Is.EqualTo(FallSecondaryMotion.Hash01(5, 2)));
        Assert.That(FallSecondaryMotion.Hash01(5, 2), Is.Not.EqualTo(FallSecondaryMotion.Hash01(5, 3)));
        float sum = 0f;
        for (int seed = 0; seed < 1000; seed++) {
            float value = FallSecondaryMotion.Hash01(seed, 1);
            Assert.That(value, Is.InRange(0f, 0.99999f));
            sum += value;
        }
        Assert.That(sum / 1000f, Is.InRange(0.4f, 0.6f));
    }

    [Test]
    public void CopyFromCopiesEveryField() {
        FallSecondarySettings target = new FallSecondarySettings();
        FallSecondarySettings source = new FallSecondarySettings();
        foreach (System.Reflection.FieldInfo field in typeof(FallSecondarySettings).GetFields()) {
            if (field.FieldType == typeof(float)) {
                field.SetValue(source, (float)field.GetValue(source) + 0.37f);
            }
            else if (field.FieldType == typeof(Vector2)) {
                field.SetValue(source, (Vector2)field.GetValue(source) + new Vector2(0.3f, 0.2f));
            }
        }
        target.CopyFrom(source);
        foreach (System.Reflection.FieldInfo field in typeof(FallSecondarySettings).GetFields()) {
            Assert.That(field.GetValue(target), Is.EqualTo(field.GetValue(source)), field.Name);
        }
    }
}
