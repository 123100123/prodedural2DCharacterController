using UnityEngine;

// Moves a background layer with the camera by its parallax factor (ParallaxMath). The layer stands at its authored
// position when the camera is where it is after the first frame (the camera follows the character from the start),
// so the authored scene looks as placed; it then lags behind the camera by its factor. Runs after the camera follow.
[DefaultExecutionOrder(50)]
public sealed class ParallaxLayer : MonoBehaviour {
    [Tooltip("Per axis: 1 stays fixed in the world, 0 follows the camera on the screen.")]
    [SerializeField] private Vector2 factor = new Vector2(0.5f, 0.5f);
    [Tooltip("Camera to follow; falls back to the main camera.")]
    [SerializeField] private Camera targetCamera;

    private Vector3 origin;
    private Vector2 originCamera;
    private bool hasOrigin;

    public Vector2 Factor { get => factor; set => factor = value; }

    private void OnEnable() => hasOrigin = false;

    private void LateUpdate() {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null) {
            return;
        }

        Vector2 cameraPosition = camera.transform.position;
        if (!hasOrigin) {
            origin = transform.position;
            originCamera = cameraPosition;
            hasOrigin = true;
        }
        Vector2 offset = ParallaxMath.Offset(cameraPosition - originCamera, factor);
        transform.position = new Vector3(origin.x + offset.x, origin.y + offset.y, origin.z);
    }
}
