using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FootholdPlannerTests {
    private static readonly FootholdLimits Limits = FootholdLimits.Default;

    private static FootholdRequest WalkingRightFrom(Vector2 plantedFoot, float desiredX) {
        return new FootholdRequest {
            DesiredX = desiredX,
            LandingHipX = desiredX - 0.5f,
            PlantedFoot = plantedFoot,
            HipHeight = 2.1f,
            LegLength = 2.34f,
            ForwardSign = 1f,
        };
    }

    private static bool TryChoose(FootholdRequest request, params Vector2[] candidates) {
        return FootholdPlanner.TryChooseFoothold(request, (new List<Vector2>(candidates), Limits), out _);
    }

    private static Vector2 Choose(FootholdRequest request, params Vector2[] candidates) {
        Assert.IsTrue(FootholdPlanner.TryChooseFoothold(request, (new List<Vector2>(candidates), Limits), out Vector2 foothold));
        return foothold;
    }

    [Test]
    public void AcceptsAStepUpInsideTheWindow() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 1.5f);
        Assert.AreEqual(new Vector2(1.5f, 0.85f), Choose(request, new Vector2(1.5f, 0.85f)));
    }

    [Test]
    public void RejectsLedgesHigherThanAStep() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 1.5f);
        Assert.IsFalse(TryChoose(request, new Vector2(1.5f, Limits.maxStepUp + 0.2f)));
    }

    [Test]
    public void RejectsTheFloorDeepUnderAGap() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 1.5f);
        Assert.IsFalse(TryChoose(request, new Vector2(1.5f, -2.6f)));
    }

    [Test]
    public void PrefersTheNearLedgeOverAFartherHigherOne() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 1.5f);
        Vector2 near = new Vector2(1.4f, 0f);
        Vector2 farHigher = new Vector2(2.2f, 0.9f);
        Assert.AreEqual(near, Choose(request, farHigher, near));
    }

    [Test]
    public void RejectsFootholdsOutOfLegReach() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 6f);
        request.LandingHipX = 1f;
        Assert.IsFalse(TryChoose(request, new Vector2(6f, 0f)), "the hip will be far behind the foothold");
    }

    [Test]
    public void SkipsStepsShorterThanTheMinimum() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 0.05f);
        Assert.IsFalse(TryChoose(request, new Vector2(0.05f, 0f)));
    }

    [Test]
    public void BreaksExactTiesTowardTheWalkingDirection() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 1.5f);
        Vector2 behind = new Vector2(1.3f, 0f);
        Vector2 ahead = new Vector2(1.7f, 0f);
        Assert.AreEqual(ahead, Choose(request, behind, ahead));
    }

    [Test]
    public void FindsNothingWithoutCandidates() {
        Assert.IsFalse(TryChoose(WalkingRightFrom(Vector2.zero, 1.5f)));
    }

    // A foothold that would hold the planted foot beyond its reach until it may lift is passed over for one that keeps it
    // (going uphill, a lower, nearer foothold keeps the hip lower); with none that does, the best foothold is still taken.
    [Test]
    public void PrefersAFootholdThatKeepsThePlantedFootInReach() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 2.4f);
        Vector2 desired = new Vector2(2.4f, 1.0f);
        Vector2 lower = new Vector2(2.2f, -0.6f);
        Assert.AreEqual(desired, Choose(request, desired, lower), "nothing to keep: the nearest spot");

        request.SupportReach = 2.337f;
        request.SupportHipX = 1.6f;
        request.SupportSink = 0.23f;
        Assert.IsFalse(FootholdPlanner.KeepsSupportInReach(desired, request));
        Assert.IsTrue(FootholdPlanner.KeepsSupportInReach(lower, request));
        Assert.AreEqual(lower, Choose(request, desired, lower), "the planted foot stays within reach");
        Assert.AreEqual(desired, Choose(request, desired), "no foothold keeps it: still a step rather than none");
    }

    // Stepping off a crest onto a ramp, the hip at touchdown rides the ground under it, far below the foot still on the
    // top: a ramp foothold the mean height of the feet would put out of reach is within it.
    [Test]
    public void OffACrestTheLandingHipRidesTheGroundUnderIt() {
        FootholdRequest request = WalkingRightFrom(Vector2.zero, 3f);
        request.LandingHipX = 2.5f;
        Vector2 onTheRamp = new Vector2(3f, -1.8f);
        // The step window widened for the slope, as the gait does (StepLimitsOnSlope).
        FootholdLimits slopeLimits = Limits;
        slopeLimits.maxStepDown = 3f;
        List<Vector2> candidates = new List<Vector2> { onTheRamp };
        Assert.IsFalse(FootholdPlanner.TryChooseFoothold(request, (candidates, slopeLimits), out _), "the mean of the feet keeps the hip near the top");

        request.HasLandingGround = true;
        request.LandingGroundY = -1.3f;
        request.HipAboveGround = 2f;
        Assert.IsTrue(FootholdPlanner.TryChooseFoothold(request, (candidates, slopeLimits), out Vector2 foothold));
        Assert.AreEqual(onTheRamp, foothold);
    }
}
