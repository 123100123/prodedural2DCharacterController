using UnityEngine;

// Seeded, seamless placeholder textures for the parallax background. Every texture tiles horizontally (all patterns are
// periodic across the width), so the layers can be repeated across the level. y = 0 is the bottom row.
public static class PlaceholderArt {
    private const int Seed = 7;

    // Two ridges of hills, the far one pale and hazy, the near one darker; transparent above, so the sky shows.
    public static Texture2D Mountains(int width, int height) {
        Color32[] pixels = new Color32[width * height];
        Color far = new Color(0.36f, 0.42f, 0.58f);
        Color near = new Color(0.22f, 0.27f, 0.41f);
        for (int x = 0; x < width; x++) {
            float farRidge = height * 0.58f + Ridge(x, width, new[] { (1, 46f, 0.3f), (3, 24f, 1.7f), (7, 11f, 4.1f) });
            float nearRidge = height * 0.36f + Ridge(x, width, new[] { (2, 34f, 2.2f), (5, 18f, 0.4f), (11, 9f, 3.3f) });
            for (int y = 0; y < height; y++) {
                Color color;
                float alpha;
                if (y < nearRidge) {
                    color = Color.Lerp(near, near * 0.7f, 1f - y / Mathf.Max(1f, nearRidge));
                    alpha = Mathf.Clamp01(nearRidge - y);
                }
                else if (y < farRidge) {
                    color = Color.Lerp(far, far * 1.15f, Mathf.InverseLerp(0f, farRidge, y));
                    alpha = Mathf.Clamp01(farRidge - y);
                }
                else {
                    color = far;
                    alpha = 0f;
                }
                pixels[y * width + x] = new Color32((byte)(Mathf.Clamp01(color.r) * 255f), (byte)(Mathf.Clamp01(color.g) * 255f), (byte)(Mathf.Clamp01(color.b) * 255f), (byte)(alpha * 255f));
            }
        }
        return ToTexture(pixels, width, height);
    }

    // Broken columns and two arches along a base of rubble; transparent between them.
    public static Texture2D Ruins(int width, int height) {
        Color32[] pixels = new Color32[width * height];
        Color32 stone = new Color32(44, 51, 74, 255);
        Color32 clear = new Color32(0, 0, 0, 0);
        int columnSpacing = width / 4;
        for (int x = 0; x < width; x++) {
            int column = Mathf.RoundToInt((x - columnSpacing / 2f) / columnSpacing);
            float centre = columnSpacing / 2f + column * columnSpacing;
            bool onColumn = Mathf.Abs(x - centre) < 14f;
            int columnTop = Mathf.RoundToInt(height * (0.62f + 0.2f * Hash(Mathf.FloorToInt(Mathf.Repeat(column, 4f)), 0)));
            for (int y = 0; y < height; y++) {
                bool rubble = y < 36 + Mathf.RoundToInt(10f * Hash(x / 6, 1));
                bool pillar = onColumn && y < columnTop - Mathf.RoundToInt(8f * Hash(x / 4, 2));
                bool arch = ArchContains(x, y, columnSpacing, height);
                pixels[y * width + x] = rubble || pillar || arch ? stone : clear;
            }
        }
        return ToTexture(pixels, width, height);
    }

    // Brick courses of 64 x 32 pixels, offset on alternate rows, with mortar lines; the top three units are a crenellated
    // edge with gaps, so the layers behind show above the wall.
    public static Texture2D BrickWall(int width, int height) {
        Color32[] pixels = new Color32[width * height];
        const int brickWidth = 64;
        const int brickHeight = 32;
        const int mortar = 3;
        int crenellationStart = height - 96;
        for (int y = 0; y < height; y++) {
            int row = y / brickHeight;
            int offset = row % 2 == 0 ? 0 : brickWidth / 2;
            for (int x = 0; x < width; x++) {
                bool isGap = y >= crenellationStart && (x / 32) % 2 == 1;
                if (isGap) {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                int brickX = (x + offset) % brickWidth;
                bool isMortar = brickX < mortar || y % brickHeight < mortar;
                int brickId = ((x + offset) / brickWidth) + row * 17;
                float shade = 0.82f + 0.3f * Hash(brickId, 3) - 0.08f * (y % brickHeight) / brickHeight;
                Color brick = isMortar ? new Color(0.14f, 0.15f, 0.2f) : new Color(0.3f, 0.32f, 0.4f) * shade;
                pixels[y * width + x] = new Color32((byte)(Mathf.Clamp01(brick.r) * 255f), (byte)(Mathf.Clamp01(brick.g) * 255f), (byte)(Mathf.Clamp01(brick.b) * 255f), 255);
            }
        }
        return ToTexture(pixels, width, height);
    }

    // A sum of sines whose frequencies are whole numbers of cycles across the width, so the ends meet.
    private static float Ridge(int x, int width, (int cycles, float amplitude, float phase)[] waves) {
        float sum = 0f;
        foreach ((int cycles, float amplitude, float phase) wave in waves) {
            sum += wave.amplitude * Mathf.Sin(2f * Mathf.PI * wave.cycles * x / width + wave.phase);
        }
        return sum;
    }

    // Two arches, each between a pair of neighbouring columns (the first and second, the third and fourth).
    private static bool ArchContains(int x, int y, int spacing, int height) {
        for (int pair = 0; pair < 2; pair++) {
            float centreX = spacing / 2f + (pair * 2 + 0.5f) * spacing;
            float radiusX = spacing / 2f - 10f;
            float baseY = height * 0.5f;
            float dx = (x - centreX) / radiusX;
            float dy = (y - baseY) / 70f;
            float outer = dx * dx + dy * dy;
            float inner = (dx * radiusX / (radiusX - 18f)) * (dx * radiusX / (radiusX - 18f)) + (dy * 70f / 52f) * (dy * 70f / 52f);
            if (y >= baseY && outer <= 1f && inner >= 1f) {
                return true;
            }
        }
        return false;
    }

    private static float Hash(int a, int salt) {
        unchecked {
            uint hash = (uint)(a * 73856093) ^ (uint)((salt + Seed) * 19349663);
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFFu) / (float)0x1000000;
        }
    }

    private static Texture2D ToTexture(Color32[] pixels, int width, int height) {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
