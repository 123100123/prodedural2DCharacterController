using UnityEngine;

// The pictures of the sidearm, drawn in code like the hover target's and the effects' textures: a small dark-grey pistol
// (slide, barrel, grip, trigger guard, a highlight along the slide) and the leather holster it rests in. Both share one
// canvas and one pivot, at the grip, with the barrel pointing along +x, so the holster lines up with the pistol by being
// placed at the same point. Each sprite is made once and shared; they are point filtered.
public static class PistolTextures {
    public const int Width = 48;
    public const int Height = 32;
    // Pixels per unit, so the pistol is 0.75 units long at a scale of 1.
    public const float PixelsPerUnit = 64f;
    // The grip's centre in pixels from the bottom left: the sprite's pivot, and where the hand holds it.
    public static readonly Vector2Int GripPixel = new Vector2Int(13, 11);
    // The row through the middle of the barrel.
    public const int BarrelRow = 21;
    // The top edge of the slide and the middle of the ejection port in it, in pixels.
    private const int SlideTop = 26;
    private const float PortCenterX = 25.5f;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 Outline = new Color32(14, 14, 18, 255);
    private static readonly Color32 Slide = new Color32(58, 60, 68, 255);
    private static readonly Color32 SlideShade = new Color32(40, 42, 48, 255);
    private static readonly Color32 Highlight = new Color32(128, 134, 148, 255);
    private static readonly Color32 Barrel = new Color32(82, 86, 96, 255);
    private static readonly Color32 Frame = new Color32(46, 48, 54, 255);
    private static readonly Color32 Grip = new Color32(34, 34, 40, 255);
    private static readonly Color32 GripDot = new Color32(52, 52, 60, 255);
    private static readonly Color32 Port = new Color32(10, 10, 12, 255);
    private static readonly Color32 Trigger = new Color32(110, 112, 122, 255);
    private static readonly Color32 Leather = new Color32(112, 68, 34, 255);
    private static readonly Color32 LeatherDark = new Color32(70, 40, 20, 255);
    private static readonly Color32 LeatherLight = new Color32(160, 112, 66, 255);
    private static readonly Color32 Snap = new Color32(190, 190, 196, 255);

    private static Sprite pistol;
    private static Sprite holster;

    public static Sprite PistolSprite() => pistol != null ? pistol : pistol = SpriteOf(PistolTexture());

    public static Sprite HolsterSprite() => holster != null ? holster : holster = SpriteOf(HolsterTexture());

    // The muzzle in the pistol's local space (units from the grip): the outer edge of the barrel's tip.
    public static Vector2 BarrelTip(Texture2D pistolTexture) {
        int tip = -1;
        for (int x = 0; x < pistolTexture.width; x++) {
            if (pistolTexture.GetPixel(x, BarrelRow).a > 0.5f) {
                tip = x;
            }
        }
        return ToLocal(new Vector2(tip + 1, BarrelRow + 0.5f));
    }

    // The same, for the generated texture (the tip is a constant of the drawing).
    public static Vector2 BarrelTip() => ToLocal(new Vector2(BarrelTipEdge, BarrelRow + 0.5f));

    // Where spent casings leave: the middle of the port, on top of the slide.
    public static Vector2 EjectionPort() => ToLocal(new Vector2(PortCenterX, SlideTop));

    // The outer edge of the outlined barrel tip, in pixels.
    public const int BarrelTipEdge = 47;

    public static Vector2 ToLocal(Vector2 pixel) => (pixel - (Vector2)GripPixel) / PixelsPerUnit;

    private static Sprite SpriteOf(Texture2D texture) {
        Vector2 pivot = new Vector2(GripPixel.x / (float)texture.width, GripPixel.y / (float)texture.height);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, PixelsPerUnit);
    }

    // ---- The pistol -----------------------------------------------------------------------------------------

    public static Texture2D PistolTexture() {
        Color32[] pixels = new Color32[Width * Height];
        Fill(pixels, (12, 15, 31, 17), Frame);
        Fill(pixels, (10, 18, 42, 25), Slide);
        Fill(pixels, (10, 18, 42, 18), SlideShade);
        Fill(pixels, (11, 25, 41, 25), Highlight);
        Fill(pixels, (43, 19, 45, 24), Barrel);
        Fill(pixels, (45, 21, 45, 22), SlideShade);
        for (int x = 14; x <= 18; x += 2) {
            Fill(pixels, (x, 19, x, 24), SlideShade);
        }
        Fill(pixels, (23, 22, 28, 24), Port);
        Fill(pixels, (11, 26, 12, 27), Slide);
        Fill(pixels, (40, 26, 41, 27), Slide);
        Fill(pixels, (41, 27, 41, 27), Highlight);
        for (int y = 1; y <= 14; y++) {
            int left = Mathf.RoundToInt(10f - (14 - y) * 0.35f);
            Fill(pixels, (left, y, left + 7, y), Grip);
            for (int x = left + 1; x <= left + 6; x++) {
                if (y >= 3 && y <= 11 && (x + y) % 2 == 0) {
                    pixels[y * Width + x] = GripDot;
                }
            }
        }
        Fill(pixels, (25, 9, 26, 14), Frame);
        Fill(pixels, (17, 8, 26, 9), Frame);
        Fill(pixels, (21, 11, 21, 14), Trigger);
        Texture2D texture = Wrap("Pistol", Outlined(pixels));
        return texture;
    }

    // ---- The holster ----------------------------------------------------------------------------------------

    // A leather pouch over the slide and barrel with its mouth at the grip, a retention strap across the mouth with a
    // snap, and a line of stitching along its top edge.
    public static Texture2D HolsterTexture() {
        Color32[] pixels = new Color32[Width * Height];
        Fill(pixels, (18, 13, 45, 27), Leather);
        Fill(pixels, (18, 13, 45, 14), LeatherDark);
        Fill(pixels, (44, 13, 45, 27), LeatherDark);
        Fill(pixels, (18, 27, 45, 27), LeatherDark);
        for (int x = 20; x <= 42; x += 2) {
            pixels[25 * Width + x] = LeatherLight;
        }
        Fill(pixels, (18, 13, 19, 27), LeatherDark);
        Fill(pixels, (15, 12, 18, 29), Leather);
        Fill(pixels, (15, 12, 15, 29), LeatherDark);
        pixels[20 * Width + 17] = Snap;
        pixels[21 * Width + 17] = Snap;
        // Rounded toe.
        foreach ((int x, int y) corner in new[] { (45, 13), (45, 27), (44, 13), (44, 27) }) {
            pixels[corner.y * Width + corner.x] = Clear;
        }
        return Wrap("Holster", Outlined(pixels));
    }

    // ---- Helpers --------------------------------------------------------------------------------------------

    private static void Fill(Color32[] pixels, (int left, int bottom, int right, int top) area, Color32 color) {
        for (int y = Mathf.Max(0, area.bottom); y <= Mathf.Min(Height - 1, area.top); y++) {
            for (int x = Mathf.Max(0, area.left); x <= Mathf.Min(Width - 1, area.right); x++) {
                pixels[y * Width + x] = color;
            }
        }
    }

    // The picture with a one pixel dark line around everything drawn.
    private static Color32[] Outlined(Color32[] pixels) {
        Color32[] result = (Color32[])pixels.Clone();
        for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
                if (pixels[y * Width + x].a > 0) {
                    continue;
                }
                bool touches = (x > 0 && pixels[y * Width + x - 1].a > 0) || (x < Width - 1 && pixels[y * Width + x + 1].a > 0)
                    || (y > 0 && pixels[(y - 1) * Width + x].a > 0) || (y < Height - 1 && pixels[(y + 1) * Width + x].a > 0);
                if (touches) {
                    result[y * Width + x] = Outline;
                }
            }
        }
        return result;
    }

    private static Texture2D Wrap(string name, Color32[] pixels) {
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
