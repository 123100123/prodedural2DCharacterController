using NUnit.Framework;
using UnityEngine;

// The default gaits against the inverseRobo rig's legs on flat ground: at full speed, a foot planted where the gait
// plans it (half a stance ahead of the hip at contact, half behind at toe-off) is within reach with the knee still
// bent, and the standing legs keep a bend too.
public class StrideReachTests {
    // Measured on the rig: thigh plus shin, and the hips' depth below the body pivot.
    private const float LegLength = 2.337f;
    private const float HipBelowPivot = 0.27f;
    private static readonly BodyMassSettings Body = BodyMassSettings.Default;

    // Hip-to-foot distance at contact as a share of the leg, with the pelvis at its gait height less the bob's dip.
    private static float ContactExtension(GaitProfile gait) {
        float lead = GaitTiming.LandingLead(gait.speed, gait);
        float hipHeight = StrideController.DefaultBodyHeight - gait.pelvisDrop - gait.bobAmplitude - HipBelowPivot;
        return Mathf.Sqrt(lead * lead + hipHeight * hipHeight) / LegLength;
    }

    [Test]
    public void StandingLegsKeepTheirKneesBent() {
        float hipHeight = StrideController.DefaultBodyHeight - HipBelowPivot;
        float stanceHalfWidth = IdleStanceSettings.Default.stanceHalfWidth;
        float extension = Mathf.Sqrt(hipHeight * hipHeight + stanceHalfWidth * stanceHalfWidth) / LegLength;
        Assert.That(extension, Is.InRange(0.88f, Body.legExtension), "a little short of straight");
    }

    [Test]
    public void FullSpeedStridesStayWithinReachWithAKneeReserve([Values(false, true)] bool running) {
        GaitProfile gait = running ? GaitProfile.Run : GaitProfile.Walk;
        Assert.That(ContactExtension(gait), Is.LessThanOrEqualTo(Body.legExtension), "the contact leg keeps a bend in reserve");
    }

    // Short, quick running steps: the stride does not split the legs far apart.
    [Test]
    public void TheRunStepIsShorterThanTheLeg() {
        GaitProfile run = GaitProfile.Run;
        Assert.That(GaitTiming.StepLength(run.speed, run), Is.LessThan(LegLength));
    }
}
