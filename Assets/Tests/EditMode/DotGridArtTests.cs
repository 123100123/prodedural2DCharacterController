using NUnit.Framework;
using UnityEngine;

// The look of the background, checked as numbers: it must be told apart from the light grey character and from the
// near-black ground, and the dots must read without taking over. (The art generator is an Editor tool; it is plain code.)
public class DotGridArtTests {
    private static float Luminance(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;

    [Test]
    public void TheFieldIsDarkerThanTheCharacterAndLighterThanTheGround() {
        float field = Luminance(PlaceholderArt.GridBase);
        Assert.That(field, Is.InRange(0.2f, 0.4f), "mid-dark: the grey character (about 0.6 and up) stands out and the black ground stands out against it");
        Assert.That(field, Is.GreaterThan(0.2f), "and shadows can still darken it without it turning black");
    }

    [Test]
    public void TheDotsAreAStepLighterThanTheFieldAndTheCornerDotsAnotherStep() {
        float field = Luminance(PlaceholderArt.GridBase);
        float dot = Luminance(PlaceholderArt.GridDot);
        float major = Luminance(PlaceholderArt.GridMajorDot);
        Assert.That(dot - field, Is.InRange(0.1f, 0.3f), "visible, not shouting");
        Assert.That(major, Is.GreaterThan(dot));
        Assert.That(major, Is.LessThan(0.75f), "still darker than the character");
    }

    [Test]
    public void TheTileHasADotOnEveryCrossingAndIsMostlyPlainField() {
        Texture2D tile = PlaceholderArt.DotGrid(32, 5);
        Assert.That(tile.width, Is.EqualTo(160));
        Assert.That(tile.height, Is.EqualTo(160));

        Color corner = tile.GetPixel(0, 0);
        Color crossing = tile.GetPixel(32, 32);
        Color between = tile.GetPixel(16, 16);
        Assert.That(Luminance(corner), Is.GreaterThan(Luminance(crossing)), "the tile's corner dot is the biggest");
        Assert.That(Luminance(crossing), Is.GreaterThan(Luminance(between) + 0.05f), "every crossing has a dot");
        Assert.That(Luminance(between), Is.EqualTo(Luminance(PlaceholderArt.GridBase)).Within(0.002f), "and between them it is plain");

        int plain = 0;
        for (int y = 0; y < tile.height; y++) {
            for (int x = 0; x < tile.width; x++) {
                plain += Mathf.Abs(Luminance(tile.GetPixel(x, y)) - Luminance(PlaceholderArt.GridBase)) < 0.002f ? 1 : 0;
            }
        }
        Assert.That(plain / (float)(tile.width * tile.height), Is.GreaterThan(0.9f), "the dots are small");
        Object.DestroyImmediate(tile);
    }

    [Test]
    public void TheTileRepeatsSeamlessly() {
        Texture2D tile = PlaceholderArt.DotGrid(32, 5);
        int last = tile.width - 1;
        for (int index = 0; index < tile.width; index++) {
            Assert.That(Luminance(tile.GetPixel(0, index)), Is.EqualTo(Luminance(tile.GetPixel(last + 1 - 1, index))).Within(0.35f), "dots straddle the edges");
        }
        // The dot on the corner is drawn half at the start and half at the end: the pixels either side of the seam match.
        Assert.That(Luminance(tile.GetPixel(0, 0)), Is.EqualTo(Luminance(tile.GetPixel(1, 0))).Within(0.1f));
        Assert.That(Luminance(tile.GetPixel(last, 0)), Is.EqualTo(Luminance(tile.GetPixel(last - 1, 0))).Within(0.1f));
        Assert.That(Luminance(tile.GetPixel(0, last)), Is.EqualTo(Luminance(tile.GetPixel(0, last - 1))).Within(0.1f));
        Object.DestroyImmediate(tile);
    }
}
