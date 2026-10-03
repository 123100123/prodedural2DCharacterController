using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The full-speed run against the reference sprinter: one forward body line without a bend at the waist, the push-off
// leg straight and well behind the hip, the swinging knee driven up in front, and the arms pumping front and back.
public class RunPoseTests : ScenarioFixture {
    [UnityTest]
    public IEnumerator TheFullRunMatchesTheReferenceSprinter() {
        RunPoseRecorder recorder = new RunPoseRecorder((Stage.Character, Stage.Stride));
        Stage.Sampler.PoseRecorder = recorder;
        yield return Hold(1.5f);
        float standingWaistBend = recorder.Poses.Average(pose => pose.WaistBend);
        float standingTorsoLean = recorder.Poses.Average(pose => pose.TorsoLean);

        Stage.Character.Inspector.Preview = new GaitPreview((GaitPreview.Mode.Run, null));
        yield return Hold(2.5f);
        recorder.Clear();
        yield return Hold(2.5f);
        List<RunPoseRecorder.Pose> run = recorder.Poses.ToList();

        float torsoLean = run.Average(pose => pose.TorsoLean);
        float waistBend = run.Average(pose => pose.WaistBend) - standingWaistBend;
        List<RunPoseRecorder.Leg> pushOffs = PushOffs(run);
        float kneeDrive = run.Max(pose => Mathf.Max(SwingThigh(pose.Front), SwingThigh(pose.Back)));
        (float ahead, float behind) hands = (run.Max(pose => Mathf.Max(pose.HandsAhead.front, pose.HandsAhead.back)),
                                             run.Min(pose => Mathf.Min(pose.HandsAhead.front, pose.HandsAhead.back)));
        Report($"standingTorsoLean={standingTorsoLean:F1} torsoLean={torsoLean:F1} waistBendOverStanding={waistBend:F1} kneeDriveThigh={kneeDrive:F1} handsAhead={hands.ahead:F2} handsBehind={hands.behind:F2}");
        foreach (RunPoseRecorder.Leg pushOff in pushOffs) {
            Report($"push-off: leg={LegForward(pushOff):F1} extension={pushOff.Extension:F2} footAheadOfHip={pushOff.FootAheadOfHip:F2}L");
        }

        Assert.That(torsoLean, Is.InRange(15f, 25f), "one forward body line, like the reference");
        Assert.That(Mathf.Abs(waistBend), Is.LessThan(5f), "the torso leans from the hips, not the waist");
        Assert.That(pushOffs.Count, Is.GreaterThan(3));
        Assert.That(pushOffs.Average(LegForward), Is.LessThan(-25f), "the push-off leg slants well behind the hip");
        Assert.That(pushOffs.Average(leg => leg.Extension), Is.GreaterThan(0.93f), "the push-off leg is straight");
        Assert.That(pushOffs.Average(leg => leg.FootAheadOfHip), Is.LessThan(-0.42f), "the push-off foot is about half a leg behind");
        Assert.That(kneeDrive, Is.GreaterThan(45f), "the swinging knee drives up in front");
        Assert.That(hands.ahead, Is.GreaterThan(0.5f), "a hand pumps forward of the body");
        Assert.That(hands.behind, Is.LessThan(-0.3f), "a hand pumps back behind the body");
    }

    // A leg's pose on the frame its foot leaves the ground.
    private static List<RunPoseRecorder.Leg> PushOffs(List<RunPoseRecorder.Pose> run) {
        List<RunPoseRecorder.Leg> pushOffs = new List<RunPoseRecorder.Leg>();
        for (int index = 1; index < run.Count; index++) {
            AddIfLifted((run[index - 1].Front, run[index].Front), pushOffs);
            AddIfLifted((run[index - 1].Back, run[index].Back), pushOffs);
        }
        return pushOffs;
    }

    // The planted frame just before the lift is the push-off pose.
    private static void AddIfLifted((RunPoseRecorder.Leg before, RunPoseRecorder.Leg after) leg, List<RunPoseRecorder.Leg> pushOffs) {
        if (leg.before.IsGrounded && !leg.after.IsGrounded) {
            pushOffs.Add(leg.before);
        }
    }

    private static float SwingThigh(RunPoseRecorder.Leg leg) => leg.IsGrounded ? float.NegativeInfinity : leg.ThighForward;

    // Degrees the hip-to-foot line points forward of straight down (negative: behind).
    private static float LegForward(RunPoseRecorder.Leg leg) {
        return Mathf.Asin(Mathf.Clamp(leg.FootAheadOfHip / Mathf.Max(0.01f, leg.Extension), -1f, 1f)) * Mathf.Rad2Deg;
    }
}
