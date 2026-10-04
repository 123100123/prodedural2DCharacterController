using UnityEngine;

// The parallax of a background layer: a factor of 1 keeps the layer fixed in the world (it passes by as fast as the
// level), 0 keeps it fixed on the screen (it follows the camera), and in between the layer lags behind the camera.
public static class ParallaxMath {
    // How far the layer moves for a camera movement, per axis.
    public static Vector2 Offset(Vector2 cameraDelta, Vector2 factor) {
        return new Vector2(cameraDelta.x * (1f - factor.x), cameraDelta.y * (1f - factor.y));
    }
}
