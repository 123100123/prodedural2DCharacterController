using NUnit.Framework;
using UnityEngine;

public class GaitWeightTests {
    private static readonly (Vector2 hipOffset, float reach) Leg = (new Vector2(0f, -0.26f), 2.25f);

    [Test]
    public void PelvisIsHighestOverItsSupportFoot() {
        float overFoot = GaitPose.HighestPelvisY((Vector2.zero, 0f), Leg);
        float besideFoot = GaitPose.HighestPelvisY((Vector2.zero, 1.2f), Leg);

        Assert.That(overFoot, Is.EqualTo(2.25f + 0.26f).Within(0.001f));
        Assert.That(besideFoot, Is.LessThan(overFoot - 0.2f), "spread feet lower the pelvis");
    }

    [Test]
    public void FootOutOfReachKeepsThePelvisAtFootLevel() {
        float y = GaitPose.HighestPelvisY((Vector2.zero, 10f), Leg);
        Assert.That(y, Is.EqualTo(0.26f).Within(0.001f));
    }

    [Test]
    public void StepTravelsForwardAndEndsOnTheFoothold([Values(0f, 0.5f, 1f)] float runShare) {
        StepArcShape shape = new StepArcShape();
        (Vector2 start, Vector2 end) step = (Vector2.zero, new Vector2(2f, 0f));
        float previousX = -1f;
        for (float t = 0f; t <= 1.0001f; t += 0.05f) {
            float x = StepArc.Evaluate((step.start, step.end, t), (shape, 0.5f, runShare)).x;
            Assert.That(x, Is.GreaterThanOrEqualTo(previousX - 0.0001f), "never slides backward");
            previousX = x;
        }
        Assert.That(Vector2.Distance(StepArc.Evaluate((step.start, step.end, 1f), (shape, 0.5f, runShare)), step.end), Is.LessThan(0.001f));
    }

    [Test]
    public void WalkingStepClearsTheHigherEndAndLandsGently() {
        StepArcShape shape = new StepArcShape();
        (Vector2 start, Vector2 end) stepUp = (Vector2.zero, new Vector2(1.5f, 0.8f));

        Vector2 apex = StepArc.Evaluate((stepUp.start, stepUp.end, 0.4f), (shape, 0.5f, 0f));
        Assert.That(apex.y, Is.GreaterThanOrEqualTo(0.8f + 0.5f - 0.05f));

        float nearEnd = StepArc.Evaluate((stepUp.start, stepUp.end, 0.95f), (shape, 0.5f, 0f)).y;
        float atEnd = StepArc.Evaluate((stepUp.start, stepUp.end, 1f), (shape, 0.5f, 0f)).y;
        Assert.That((nearEnd - atEnd) / 0.05f, Is.InRange(0.1f, 2f), "the foot is still lowering as it touches down, but gently");
    }

    [Test]
    public void RunningFootComesDownHarderThanWalkingFoot() {
        StepArcShape shape = new StepArcShape();
        (Vector2 start, Vector2 end) step = (Vector2.zero, new Vector2(2f, 0f));
        float walkingDescent = StepArc.Evaluate((step.start, step.end, 0.95f), (shape, 0.5f, 0f)).y;
        float runningDescent = StepArc.Evaluate((step.start, step.end, 0.95f), (shape, 0.5f, 1f)).y;
        Assert.That(runningDescent, Is.GreaterThan(2f * walkingDescent), "a running foot is still high just before it plants");
    }

    [Test]
    public void WalkingFootPassesUnderTheHipNearTheTopOfItsArc() {
        SwingAtFullSpeed swing = new SwingAtFullSpeed(GaitProfile.Walk, 0f);
        float topProgress = 0f;
        for (float progress = 0f; progress <= 1f; progress += 0.01f) {
            if (swing.Foot(progress).y > swing.Foot(topProgress).y) {
                topProgress = progress;
            }
        }
        Assert.That(Mathf.Abs(swing.FootBehindHip(topProgress)), Is.LessThan(0.35f * swing.Lead));
    }

    // A foot raised high while still far behind the hip folds the shin back flat behind the body.
    [Test]
    public void RunningFootStaysLowWhileItTrailsTheBody() {
        SwingAtFullSpeed swing = new SwingAtFullSpeed(GaitProfile.Run, 1f);
        for (float progress = 0f; progress <= 1f; progress += 0.01f) {
            if (swing.FootBehindHip(progress) > swing.Lag / 2f) {
                Assert.That(swing.Foot(progress).y, Is.LessThan(0.6f * GaitProfile.Run.liftHeight), $"at progress {progress:F2}");
            }
        }
    }

    // The knee drive: the running foot is highest as it passes under the hip, about a quarter of a leg up, which folds
    // the knee up in front of the body.
    [Test]
    public void RunningFootPeaksUnderTheHipForTheKneeDrive() {
        SwingAtFullSpeed swing = new SwingAtFullSpeed(GaitProfile.Run, 1f);
        float peakProgress = 0f;
        for (float progress = 0f; progress <= 1f; progress += 0.01f) {
            if (swing.Foot(progress).y > swing.Foot(peakProgress).y) {
                peakProgress = progress;
            }
        }
        Assert.That(Mathf.Abs(swing.FootBehindHip(peakProgress)), Is.LessThan(0.3f), "under the hip at the top of the arc");
        Assert.That(swing.Foot(peakProgress).y, Is.GreaterThan(0.25f * 2.337f), "a quarter of the leg high");
    }

    // A full-speed swing on flat ground, measured from where the hip was at lift-off: the foot lifts its toe-off lag
    // behind the hip and lands its contact lead ahead of where the hip is at touchdown.
    private readonly struct SwingAtFullSpeed {
        private readonly (Vector2 start, Vector2 end) step;
        private readonly (StepArcShape shape, float liftHeight, float runShare) arc;
        private readonly float hipTravel;

        public SwingAtFullSpeed(GaitProfile gait, float runShare) {
            Lead = GaitTiming.ContactLead(gait.speed, gait);
            Lag = GaitTiming.ToeOffLag(gait.speed, gait);
            hipTravel = gait.speed * GaitTiming.SwingDuration(gait.speed, gait);
            step = (new Vector2(-Lag, 0f), new Vector2(hipTravel + Lead, 0f));
            arc = (new StepArcShape(), gait.liftHeight, runShare);
        }

        public float Lead { get; }
        public float Lag { get; }

        public Vector2 Foot(float progress) => StepArc.Evaluate((step.start, step.end, progress), arc);

        public float FootBehindHip(float progress) => hipTravel * progress - Foot(progress).x;
    }

    [Test]
    public void WalkKeepsAFootDownWhileRunHasFlight() {
        Assert.That(GaitProfile.Walk.dutyFactor, Is.GreaterThan(0.5f), "stance longer than swing: a foot is always down");
        Assert.That(GaitProfile.Run.dutyFactor, Is.LessThan(0.5f), "stance shorter than swing: both feet leave the ground");
    }

    [Test]
    public void BlendMovesSmoothlyFromWalkToRun() {
        (GaitProfile walk, GaitProfile run) profiles = (GaitProfile.Walk, GaitProfile.Run);
        Assert.That(GaitProfile.Blend(profiles, 0f).speed, Is.EqualTo(profiles.walk.speed));
        Assert.That(GaitProfile.Blend(profiles, 1f).speed, Is.EqualTo(profiles.run.speed));
        Assert.That(GaitProfile.Blend(profiles, 0.5f).speed, Is.EqualTo((profiles.walk.speed + profiles.run.speed) / 2f).Within(0.0001f));
    }
}
