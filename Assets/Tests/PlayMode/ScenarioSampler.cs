using UnityEngine;

// Records the gait (and, when asked, the pose) at the very end of each frame, after the leg IK has solved, and lets the
// stage re-send the input it is holding.
[DefaultExecutionOrder(32000)]
public sealed class ScenarioSampler : MonoBehaviour {
    public GaitRecorder Recorder { get; set; }
    public RunPoseRecorder PoseRecorder { get; set; }

    public System.Action ResendHeldInput { get; set; }

    private void LateUpdate() {
        ResendHeldInput?.Invoke();
        Recorder?.Record();
        PoseRecorder?.Record();
    }
}
