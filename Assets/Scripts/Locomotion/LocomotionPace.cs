using UnityEngine;

// How far the gait has come from standing toward walking (Moving) and from walking toward running (Running). Both are
// measured from the ground speed on fixed scales, so they rise and fall with speed alone: the run input only raises the
// target speed, and the gait turns into a run as the body actually speeds up. Effects that grow with motion (posture,
// bob, arm swing) scale with Moving; the walk and run profiles blend by Running.
public readonly struct LocomotionPace {
    public static readonly LocomotionPace Standing = new LocomotionPace((0f, 0f));

    public LocomotionPace((float moving, float running) shares) {
        Moving = Mathf.Clamp01(shares.moving);
        Running = Mathf.Clamp01(shares.running);
    }

    public float Moving { get; }
    public float Running { get; }

    // 0 standing, 1 at full walking speed and above.
    public static float MovingAt(float speed, GaitProfile walk) => Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, walk.speed));

    // 0 up to the start of the run band, 1 from its end; the band is a share of the speed range from full walk to full run.
    public static float RunningAt(float speed, (GaitProfile walk, GaitProfile run, GaitTransitionSettings transitions) gait) {
        float speedRange = Mathf.Max(0.01f, gait.run.speed - gait.walk.speed);
        float shareOfRange = (Mathf.Abs(speed) - gait.walk.speed) / speedRange;
        float bandWidth = Mathf.Max(0.001f, gait.transitions.runBlendEnd - gait.transitions.runBlendStart);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((shareOfRange - gait.transitions.runBlendStart) / bandWidth));
    }
}
