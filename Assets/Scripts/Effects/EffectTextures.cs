using UnityEngine;

// The few textures the effects use, drawn in code: nothing is imported. The particle textures are white with the shape
// in their alpha, so the particle colour tints them.
public static class EffectTextures {
    private const int PuffSize = 64;
    private const int SparkSize = 32;

    // A soft round blob, opaque in the middle and clear at the rim.
    public static Texture2D SoftPuff() => Radial("SoftPuff", PuffSize, 1.6f);

    // A tight bright dot that stretches into a streak along its velocity.
    public static Texture2D Spark() => Radial("Spark", SparkSize, 0.7f);

    private static Texture2D Radial(string name, int size, float falloff) {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) / 2f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), falloff);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    // A tiny brass-coloured rectangle for a shell casing, with a lighter top edge.
    public static Sprite CasingSprite() {
        const int Width = 6;
        const int Height = 3;
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = "Casing", filterMode = FilterMode.Point };
        Color body = new Color(0.78f, 0.58f, 0.2f, 1f);
        Color highlight = new Color(1f, 0.85f, 0.45f, 1f);
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
                texture.SetPixel(x, y, y == Height - 1 ? highlight : body);
            }
        }
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 40f);
    }
}
