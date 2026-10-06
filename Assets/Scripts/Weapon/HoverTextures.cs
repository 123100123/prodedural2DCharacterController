using UnityEngine;

// The pictures of a hover target, drawn in code like the effects' textures: a square wooden board (warm grain, a darker
// frame, a bullseye of dark dots) and the parts of its rotor. Each sprite is made once and shared by every target.
public static class HoverTextures {
    public const int BoardPixels = 64;
    // Pixels per unit of every part, so the board is 1 unit wide and its transform scale sets the size it is shown at.
    public const float PixelsPerUnit = 64f;
    public const int MastWidth = 8;
    public const int MastHeight = 24;
    public const int HubPixels = 16;
    public const int BladeLength = 64;
    public const int BladeThickness = 10;

    private static readonly Color WoodLight = new Color(0.84f, 0.62f, 0.37f, 1f);
    private static readonly Color WoodDark = new Color(0.66f, 0.45f, 0.24f, 1f);
    private static readonly Color Frame = new Color(0.36f, 0.21f, 0.1f, 1f);
    private static readonly Color FrameEdge = new Color(0.5f, 0.31f, 0.16f, 1f);
    public static readonly Color DotColor = new Color(0.14f, 0.08f, 0.04f, 1f);
    private static readonly Color Metal = new Color(0.22f, 0.23f, 0.26f, 1f);
    private static readonly Color MetalLight = new Color(0.5f, 0.52f, 0.56f, 1f);

    private static Sprite board;
    private static Sprite mast;
    private static Sprite hub;
    private static Sprite blade;

    public static Sprite Board() => board != null ? board : board = SpriteOf(BoardTexture(BoardPixels), new Vector2(0.5f, 0.5f));

    public static Sprite Mast() => mast != null ? mast : mast = SpriteOf(MastTexture(), new Vector2(0.5f, 0f));

    public static Sprite Hub() => hub != null ? hub : hub = SpriteOf(HubTexture(), new Vector2(0.5f, 0.5f));

    // A blade hangs from its left end, so scaling it in x about the hub swings its tip.
    public static Sprite Blade() => blade != null ? blade : blade = SpriteOf(BladeTexture(), new Vector2(0f, 0.5f));

    private static Sprite SpriteOf(Texture2D texture, Vector2 pivot) {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, PixelsPerUnit);
    }

    // ---- The board ------------------------------------------------------------------------------------------

    // Pixel positions (from the bottom left) of the middle of each dot of the bullseye: one in the centre, then rings of
    // six and of twelve.
    public static Vector2[] DotCenters(int size) {
        float unit = size / 64f;
        Vector2 middle = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        Vector2[] centers = new Vector2[1 + 6 + 12];
        centers[0] = middle;
        int next = 1;
        for (int dot = 0; dot < 6; dot++) {
            float angle = (dot / 6f + 1f / 12f) * 2f * Mathf.PI;
            centers[next++] = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (14f * unit);
        }
        for (int dot = 0; dot < 12; dot++) {
            float angle = dot / 12f * 2f * Mathf.PI;
            centers[next++] = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (24f * unit);
        }
        return centers;
    }

    // The radius (pixels) of the dot at an index of DotCenters.
    public static float DotRadius(int index, int size) {
        float unit = size / 64f;
        return (index == 0 ? 3.2f : index < 7 ? 2.4f : 1.8f) * unit;
    }

    // The width (pixels) of the board's darker frame.
    public static int FrameWidth(int size) => Mathf.Max(1, size / 16);

    public static Texture2D BoardTexture(int size) {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HoverBoard", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        int frame = FrameWidth(size);
        Vector2[] centers = DotCenters(size);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color color = IsFrame(x, y, size, frame) ? FrameColor(x, y, size, frame) : WoodColor(x, y);
                for (int dot = 0; dot < centers.Length; dot++) {
                    float cover = Mathf.Clamp01(DotRadius(dot, size) + 0.5f - Vector2.Distance(new Vector2(x, y), centers[dot]));
                    if (cover > 0f) {
                        color = Color.Lerp(color, DotColor, cover);
                    }
                }
                pixels[y * size + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static bool IsFrame(int x, int y, int size, int frame) => x < frame || y < frame || x >= size - frame || y >= size - frame;

    // The frame: a lighter line along its outer rim and its inner edge catching the light, with a touch of noise.
    private static Color FrameColor(int x, int y, int size, int frame) {
        int depth = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
        Color color = depth == frame - 1 || depth == 0 ? FrameEdge : Frame;
        float noise = 0.92f + 0.08f * Hash(x, y);
        return new Color(color.r * noise, color.g * noise, color.b * noise, 1f);
    }

    // Horizontal grain: long streaks that drift a little along x, a few darker rows, and a touch of noise.
    private static Color WoodColor(int x, int y) {
        float streak = 0.5f + 0.5f * Mathf.Sin(y * 0.85f + ValueNoise(x * 0.06f, y * 0.4f) * 6f);
        float row = Hash(0, y / 3) < 0.14f ? 0.88f : 1f;
        float shade = (1f + (Hash(x, y) - 0.5f) * 0.07f) * row;
        Color color = Color.Lerp(WoodLight, WoodDark, streak * 0.8f);
        return new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
    }

    // Deterministic noise in 0 to 1 from integer coordinates.
    private static float Hash(int x, int y) {
        unchecked {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // Smooth noise: the hash at the four corners of a cell, blended.
    private static float ValueNoise(float x, float y) {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float low = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx);
        float high = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx);
        return Mathf.Lerp(low, high, fy);
    }

    // ---- The rotor ------------------------------------------------------------------------------------------

    // A thin steel post with a lighter side.
    public static Texture2D MastTexture() {
        Texture2D texture = Blank("HoverMast", MastWidth, MastHeight);
        for (int y = 0; y < MastHeight; y++) {
            for (int x = 0; x < MastWidth; x++) {
                bool edge = x == 0 || x == MastWidth - 1;
                Color color = edge ? Metal * 0.8f : x < MastWidth / 2 ? MetalLight : Metal;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, 1f));
            }
        }
        texture.Apply(false, false);
        return texture;
    }

    // A round hub with a bright cap.
    public static Texture2D HubTexture() {
        Texture2D texture = Blank("HoverHub", HubPixels, HubPixels);
        float radius = (HubPixels - 1) / 2f;
        for (int y = 0; y < HubPixels; y++) {
            for (int x = 0; x < HubPixels; x++) {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                float cover = Mathf.Clamp01(radius + 0.5f - distance);
                Color color = distance < radius * 0.45f ? MetalLight : Metal;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, cover));
            }
        }
        texture.Apply(false, false);
        return texture;
    }

    // One blade, long and thin, thinning towards its tip, with a bright edge along its top.
    public static Texture2D BladeTexture() {
        Texture2D texture = Blank("HoverBlade", BladeLength, BladeThickness);
        float middle = (BladeThickness - 1) / 2f;
        for (int x = 0; x < BladeLength; x++) {
            float half = Mathf.Lerp(BladeThickness / 2f, BladeThickness * 0.28f, x / (float)(BladeLength - 1));
            for (int y = 0; y < BladeThickness; y++) {
                float offset = y - middle;
                float cover = Mathf.Clamp01(half + 0.5f - Mathf.Abs(offset));
                Color color = offset > half * 0.35f ? MetalLight : Metal;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, cover));
            }
        }
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D Blank(string name, int width, int height) {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] clear = new Color32[width * height];
        texture.SetPixels32(clear);
        return texture;
    }
}
