using UnityEngine;

// Placeholder textures for the environment. Replace the PNGs with real art whenever you like; the setup only references
// the sprites.
public static class PlaceholderArt {
    // The dot grid's colours: a calm mid-dark blue that the light grey character stands out from and the near-black
    // ground stands out against, with dots a step lighter. Shadows then darken it further without it going black.
    public static readonly Color GridBase = new Color(0.255f, 0.30f, 0.41f);
    public static readonly Color GridDot = new Color(0.46f, 0.52f, 0.66f);
    public static readonly Color GridMajorDot = new Color(0.62f, 0.69f, 0.84f);

    // A plain field with a dot at every crossing of an invisible grid (a "dot grid", as in dotted paper), the dot at the
    // corner of each tile a little bigger. Each cell is cellPixels wide, and the tile holds cellsPerTile cells each way,
    // so the pattern repeats seamlessly: a dot sits on the tile's corner and is drawn across its edges.
    public static Texture2D DotGrid(int cellPixels, int cellsPerTile) {
        int size = cellPixels * cellsPerTile;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = WrappedDistance(x, cellPixels);
                float dy = WrappedDistance(y, cellPixels);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool isMajor = IsOnTileCorner(x, y, size, cellPixels);
                float radius = isMajor ? cellPixels * 0.085f : cellPixels * 0.05f;
                float coverage = Mathf.Clamp01(radius + 0.5f - distance);
                Color dot = isMajor ? GridMajorDot : GridDot;
                pixels[y * size + x] = ToColor32(Color.Lerp(GridBase, dot, coverage));
            }
        }
        return ToTexture(pixels, size, size);
    }

    // The distance of a pixel to the nearest grid line along one axis, with lines every `period` pixels (including 0).
    private static float WrappedDistance(int pixel, int period) {
        int offset = pixel % period;
        return Mathf.Min(offset, period - offset);
    }

    // Whether the nearest crossing is the tile's corner (where the wrap-around joins), for the bigger dot.
    private static bool IsOnTileCorner(int x, int y, int size, int cellPixels) {
        int half = cellPixels / 2;
        bool nearX = x < half || x >= size - half;
        bool nearY = y < half || y >= size - half;
        return nearX && nearY;
    }

    // A wall lantern on a bracket: a dark iron frame around a warm glass, its centre at the middle of the texture so the
    // light sits in the lantern. Transparent elsewhere.
    public static Texture2D Lantern(int width, int height) {
        Color32[] pixels = new Color32[width * height];
        Color32 iron = new Color32(30, 32, 42, 255);
        Color32 clear = new Color32(0, 0, 0, 0);
        int centreX = width / 2;
        int centreY = height / 2;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                bool bracket = Mathf.Abs(x - centreX) <= 2 && y > centreY + 20;
                bool arm = y >= height - 6 && Mathf.Abs(x - centreX) <= 14;
                bool frame = Mathf.Abs(x - centreX) <= 12 && y >= centreY - 20 && y <= centreY + 20;
                bool glass = Mathf.Abs(x - centreX) <= 8 && y >= centreY - 16 && y <= centreY + 16;
                bool cap = Mathf.Abs(x - centreX) <= 15 && (y == centreY + 21 || y == centreY - 21 || y == centreY + 22);
                if (glass) {
                    float glow = 1f - Mathf.Clamp01(Mathf.Sqrt((x - centreX) * (x - centreX) + (y - centreY) * (y - centreY)) / 18f);
                    pixels[y * width + x] = new Color32((byte)(255f * Mathf.Lerp(0.85f, 1f, glow)), (byte)(255f * Mathf.Lerp(0.6f, 0.95f, glow)), (byte)(255f * Mathf.Lerp(0.25f, 0.65f, glow)), 255);
                }
                else {
                    pixels[y * width + x] = bracket || arm || frame || cap ? iron : clear;
                }
            }
        }
        return ToTexture(pixels, width, height);
    }

    private static Color32 ToColor32(Color color) {
        return new Color32((byte)(Mathf.Clamp01(color.r) * 255f), (byte)(Mathf.Clamp01(color.g) * 255f), (byte)(Mathf.Clamp01(color.b) * 255f), 255);
    }

    private static Texture2D ToTexture(Color32[] pixels, int width, int height) {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
