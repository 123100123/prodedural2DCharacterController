using UnityEngine;

// Records the gait (and, when asked, the pose) at the very end of each frame, after the leg IK has solved.
[DefaultExecutionOrder(32000)]
public sealed class ScenarioSampler : MonoBehaviour {
    public GaitRecorder Recorder { get; set; }
    public RunPoseRecorder PoseRecorder { get; set; }

    private void LateUpdate() {
        Recorder?.Record();
        PoseRecorder?.Record();
    }
}
