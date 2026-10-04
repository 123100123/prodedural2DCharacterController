using UnityEngine;

// Works out the pose of this frame of a flight from its motion: the rising pose with its push-off, the hang and the
// descent, blended by the vertical speed (AirPhase), each moving from its standing to its running version as the
// character moves forward faster. Pure: the same motion always gives the same pose.
public static class AirbornePoseSolver {
    public readonly struct Motion {
        public Motion((float vertical, float forward) speed, float runSpeed, float timeSinceLaunch) {
            VerticalSpeed = speed.vertical;
            ForwardSpeed = speed.forward;
            RunSpeed = runSpeed;
            TimeSinceLaunch = timeSinceLaunch;
        }

        public float VerticalSpeed { get; }
        // Horizontal speed (either direction).
        public float ForwardSpeed { get; }
        public float RunSpeed { get; }
        public float TimeSinceLaunch { get; }
    }

    // 0 standing pose, 1 running pose.
    public static float RunningShare(Motion motion, AirPoseSettings settings) {
        float share = Mathf.Clamp01(Mathf.Abs(motion.ForwardSpeed) / Mathf.Max(0.01f, motion.RunSpeed));
        float band = Mathf.InverseLerp(settings.runningBand.x, Mathf.Max(settings.runningBand.x + 0.01f, settings.runningBand.y), share);
        return Mathf.SmoothStep(0f, 1f, band);
    }

    // 1 at take-off, falling to 0 over the push-off.
    public static float PushOff(float timeSinceLaunch, AirPoseSettings settings) {
        return 1f - Mathf.SmoothStep(0f, 1f, timeSinceLaunch / Mathf.Max(0.001f, settings.pushOffDuration));
    }

    public static AirPose Solve(Motion motion, AirPoseSettings settings) {
        AirPhase phase = AirPhase.From(motion.VerticalSpeed, settings.apexBand);
        float running = RunningShare(motion, settings);
        AirPose rising = AirPose.Lerp(settings.ascentStanding, settings.ascentRunning, running);
        AirPose falling = AirPose.Lerp(settings.descentStanding, settings.descentRunning, running);

        // The back leg stays reaching back and down behind the rising body while the legs push off.
        rising.trailFoot += settings.pushOffReach * PushOff(motion.TimeSinceLaunch, settings);
        return AirPose.Blend3((rising, phase.Ascent), (settings.apex, phase.Apex), (falling, phase.Descent));
    }
}
