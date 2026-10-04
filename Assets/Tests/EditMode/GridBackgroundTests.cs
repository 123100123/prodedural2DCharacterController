using NUnit.Framework;
using UnityEngine;

public class GridBackgroundTests {
    [Test]
    public void SnapMovesInWholePeriods() {
        Assert.That(GridMath.Snap(new Vector2(12.4f, -7.4f), 5f), Is.EqualTo(new Vector2(10f, -5f)));
        Assert.That(GridMath.Snap(new Vector2(12.6f, -7.6f), 5f), Is.EqualTo(new Vector2(15f, -10f)));
        Assert.That(GridMath.Snap(Vector2.zero, 5f), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void TheSnappedPointIsNeverMoreThanHalfAPeriodFromTheCamera() {
        for (float x = -40f; x <= 40f; x += 0.37f) {
            Vector2 snapped = GridMath.Snap(new Vector2(x, x * 0.6f), 5f);
            Assert.That(Mathf.Abs(snapped.x - x), Is.LessThanOrEqualTo(2.5f + 0.0001f));
            Assert.That(Mathf.Abs(snapped.y - x * 0.6f), Is.LessThanOrEqualTo(2.5f + 0.0001f));
            Assert.That(Mathf.Abs(snapped.x / 5f - Mathf.Round(snapped.x / 5f)), Is.LessThan(0.0001f), "on the lattice");
        }
    }

    [Test]
    public void ASmallerThanThePeriodMoveDoesNotShiftThePattern() {
        Assert.That(GridMath.Snap(new Vector2(0.4f, 0.4f), 5f), Is.EqualTo(Vector2.zero));
        Assert.That(GridMath.Snap(new Vector2(2.4f, -2.4f), 5f), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void ABadPeriodNeverDividesByZero() {
        Vector2 snapped = GridMath.Snap(new Vector2(1f, 1f), 0f);
        Assert.That(float.IsNaN(snapped.x) || float.IsInfinity(snapped.x), Is.False);
    }

    [Test]
    public void TheBackgroundLayerSitsBehindTheLevelAndIsShadowedByIt() {
        Assert.That(EnvironmentLayers.BackToFront, Is.EqualTo(new[] { "Background", "Default" }));
        Assert.That(EnvironmentLayers.ShadowedByBlocks, Does.Contain(EnvironmentLayers.Background));
        Assert.That(EnvironmentLayers.ShadowedByBlocks, Does.Contain(EnvironmentLayers.Level));
        Assert.That(EnvironmentLayers.Obsolete, Does.Not.Contain(EnvironmentLayers.Background));
    }
}
