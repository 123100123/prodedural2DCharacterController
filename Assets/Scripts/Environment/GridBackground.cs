using UnityEngine;

// A plain tiled background that stays under the camera: it follows it in whole periods of its pattern (GridMath.Snap),
// so the dots look fixed in the world, and being larger than the view it always covers it. It is one lit sprite, so it
// takes the lights and the shadows of the level like everything else.
[DefaultExecutionOrder(50)]
public sealed class GridBackground : MonoBehaviour {
    [Tooltip("World size of one repeat of the pattern; the background moves in steps of this.")]
    [SerializeField] private float period = 5f;
    [Tooltip("Camera to follow; falls back to the main camera.")]
    [SerializeField] private Camera targetCamera;

    private void LateUpdate() {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null) {
            return;
        }
        Vector2 snapped = GridMath.Snap(camera.transform.position, period);
        transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);
    }
}
