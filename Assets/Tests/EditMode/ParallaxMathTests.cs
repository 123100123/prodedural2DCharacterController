using NUnit.Framework;
using UnityEngine;

public class ParallaxMathTests {
    [Test]
    public void AFactorOfOneStaysFixedInTheWorld() {
        Assert.That(ParallaxMath.Offset(new Vector2(10f, -4f), Vector2.one), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void AFactorOfZeroFollowsTheCameraExactly() {
        Assert.That(ParallaxMath.Offset(new Vector2(10f, -4f), Vector2.zero), Is.EqualTo(new Vector2(10f, -4f)));
    }

    [Test]
    public void AFactorInBetweenLagsBehindTheCameraLinearly() {
        Assert.That(ParallaxMath.Offset(new Vector2(10f, 8f), new Vector2(0.75f, 0.5f)), Is.EqualTo(new Vector2(2.5f, 4f)));
        Assert.That(ParallaxMath.Offset(new Vector2(20f, 16f), new Vector2(0.75f, 0.5f)), Is.EqualTo(new Vector2(5f, 8f)), "twice the travel, twice the offset");
    }

    [Test]
    public void EachAxisHasItsOwnFactor() {
        Vector2 offset = ParallaxMath.Offset(new Vector2(10f, 10f), new Vector2(0.9f, 0.2f));
        Assert.That(offset.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(offset.y, Is.EqualTo(8f).Within(0.0001f));
    }

    [Test]
    public void ANearerLayerMovesLessThanAFartherOne() {
        Vector2 camera = new Vector2(12f, 3f);
        float wall = ParallaxMath.Offset(camera, new Vector2(0.9f, 0.9f)).x;
        float mid = ParallaxMath.Offset(camera, new Vector2(0.6f, 0.7f)).x;
        float far = ParallaxMath.Offset(camera, new Vector2(0.25f, 0.4f)).x;
        Assert.That(wall, Is.LessThan(mid));
        Assert.That(mid, Is.LessThan(far));
    }

    [Test]
    public void TheLayersAreNamedBackToFrontWithTheShadowedOnesAmongThem() {
        Assert.That(EnvironmentLayers.BackToFront, Is.EqualTo(new[] { "BackgroundFar", "BackgroundMid", "BackgroundWall", "Default" }));
        Assert.That(EnvironmentLayers.ShadowedByBlocks, Does.Not.Contain(EnvironmentLayers.Far), "distant scenery stays lit");
        Assert.That(EnvironmentLayers.ShadowedByBlocks, Does.Contain(EnvironmentLayers.Wall));
        Assert.That(EnvironmentLayers.ShadowedByBlocks, Does.Contain(EnvironmentLayers.Level));
    }
}
