using NUnit.Framework;
using UnityEngine;

// The default gaits against the inverseRobo rig's legs on flat ground: at full speed, a foot planted where the gait
// plans it is within reach with the knee still bent at contact, a running push-off leg straightens out behind, and the
// standing legs keep a bend.
public class StrideReachTests {
    // Measured on the rig: thigh plus shin, and the hips' depth below the body pivot.
    private const float LegLength = 2.337f;
    private const float HipBelowPivot = 0.27f;
    private static readonly BodyMassSettings Body = BodyMassSettings.Default;

    // Hip-to-foot distance as a share of the leg, with the foot the given distance ahead of or behind the hip and the
    // pelvis at its gait height shifted by the bob.
    private static float Extension((float horizontal, float bob) at, GaitProfile gait) {
        float hipHeight = StrideController.DefaultBodyHeight - gait.pelvisDrop + at.bob * gait.bobAmplitude - HipBelowPivot;
        return Mathf.Sqrt(at.horizontal * at.horizontal + hipHeight * hipHeight) / LegLength;
    }

    private static float ContactExtension(GaitProfile gait) => Extension((GaitTiming.ContactLead(gait.speed, gait), -1f), gait);

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

    // The reference runner's push-off leg ends straight, about half a leg length behind the hip: the gait asks for at
    // least the reach ceiling there, so the ceiling straightens the leg rather than the knee staying bent.
    [Test]
    public void TheRunningPushOffLegStraightensBehindTheHip() {
        GaitProfile run = GaitProfile.Run;
        float toeOffLag = GaitTiming.ToeOffLag(run.speed, run);
        Assert.That(toeOffLag, Is.InRange(0.45f * LegLength, 0.65f * LegLength), "about half a leg length behind");
        Assert.That(Extension((toeOffLag, 1f), run), Is.GreaterThanOrEqualTo(Body.legExtension));
    }

    // The foot lands close under the body: much less ahead of the hip than it pushes off behind.
    [Test]
    public void TheRunningFootLandsCloseUnderTheBody() {
        GaitProfile run = GaitProfile.Run;
        Assert.That(GaitTiming.ContactLead(run.speed, run), Is.LessThan(0.35f * LegLength));
        Assert.That(GaitTiming.StepLength(run.speed, run), Is.LessThan(1.3f * LegLength), "a long stride, not a split");
    }
}
