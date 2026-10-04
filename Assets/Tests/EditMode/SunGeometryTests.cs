using NUnit.Framework;
using UnityEngine;

public class SunGeometryTests {
    [Test]
    public void TheDirectionFollowsTheAngleFromTheRightHorizon() {
        Assert.That(SunGeometry.Direction(0f).x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SunGeometry.Direction(90f).y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SunGeometry.Direction(180f).x, Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(SunGeometry.Direction(125f).magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SunGeometry.Direction(125f).x, Is.LessThan(0f), "up and to the left");
        Assert.That(SunGeometry.Direction(125f).y, Is.GreaterThan(0f));
    }

    [Test]
    public void ThePivotIsTheDistanceAwayTowardTheSunFromTheView() {
        Vector2 view = new Vector2(30f, -4f);
        foreach (float angle in new[] { 0f, 35f, 90f, 125f, 180f }) {
            Vector2 pivot = SunGeometry.Pivot(view, (angle, 120f));
            Assert.That(Vector2.Distance(pivot, view), Is.EqualTo(120f).Within(0.001f), $"at {angle}°");
            Assert.That(Vector2.Dot((pivot - view).normalized, SunGeometry.Direction(angle)), Is.EqualTo(1f).Within(0.0001f));
        }
    }

    [Test]
    public void AnOverheadSunIsStraightAboveTheView() {
        Vector2 pivot = SunGeometry.Pivot(new Vector2(5f, 2f), (90f, 100f));
        Assert.That(pivot.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(pivot.y, Is.EqualTo(102f).Within(0.001f));
    }

    [Test]
    public void TheLitAreaIsTheCoverageRectangleAroundTheViewWhereverThePivotIs() {
        Vector2 coverage = new Vector2(140f, 60f);
        foreach (float angle in new[] { 10f, 90f, 125f, 170f }) {
            foreach (Vector2 view in new[] { Vector2.zero, new Vector2(-50f, 12f), new Vector2(200f, -30f) }) {
                Vector2 pivot = SunGeometry.Pivot(view, (angle, 120f));
                Vector3[] local = SunGeometry.LocalShape((angle, 120f), coverage);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector3 point in local) {
                    Vector2 world = pivot + (Vector2)point;
                    min = Vector2.Min(min, world);
                    max = Vector2.Max(max, world);
                }
                Assert.That(min.x, Is.EqualTo(view.x - 70f).Within(0.01f), $"{angle}° at {view}");
                Assert.That(max.x, Is.EqualTo(view.x + 70f).Within(0.01f));
                Assert.That(min.y, Is.EqualTo(view.y - 30f).Within(0.01f));
                Assert.That(max.y, Is.EqualTo(view.y + 30f).Within(0.01f));
            }
        }
    }

    [Test]
    public void TheShapeScalesWithTheCoverageAndIsASimpleQuadrilateral() {
        Vector3[] small = SunGeometry.LocalShape((125f, 120f), new Vector2(40f, 20f));
        Vector3[] big = SunGeometry.LocalShape((125f, 120f), new Vector2(80f, 40f));
        Assert.That(small.Length, Is.EqualTo(4));
        Assert.That(Vector3.Distance(big[0], big[1]), Is.EqualTo(2f * Vector3.Distance(small[0], small[1])).Within(0.001f));
        Assert.That(Vector3.Distance(big[1], big[2]), Is.EqualTo(2f * Vector3.Distance(small[1], small[2])).Within(0.001f));
        float area = Mathf.Abs((big[1].x - big[0].x) * (big[2].y - big[1].y));
        Assert.That(area, Is.EqualTo(80f * 40f).Within(0.01f));
    }

    [Test]
    public void ShadowsAreThrownDirectlyAwayFromTheSun() {
        Vector2 shadow = SunGeometry.ShadowDirection(125f);
        Assert.That(shadow.x, Is.GreaterThan(0f), "a sun up-left throws shadows down and to the right");
        Assert.That(shadow.y, Is.LessThan(0f));
        Assert.That(Vector2.Dot(shadow, SunGeometry.Direction(125f)), Is.EqualTo(-1f).Within(0.0001f));
    }

    [Test]
    public void ThePivotMovesWithTheCameraSoTheDirectionNeverChanges() {
        Vector2 first = SunGeometry.Pivot(new Vector2(0f, 0f), (125f, 120f)) - Vector2.zero;
        Vector2 later = SunGeometry.Pivot(new Vector2(80f, 10f), (125f, 120f)) - new Vector2(80f, 10f);
        Assert.That(later, Is.EqualTo(first).Using(new Vector2EqualityComparer(0.0001f)));
    }

    [Test]
    public void CoversTellsWhetherAPointIsInsideTheLitRectangle() {
        Vector2 view = new Vector2(10f, 0f);
        Vector2 coverage = new Vector2(140f, 60f);
        Assert.That(SunGeometry.Covers(new Vector2(10f, 0f), view, coverage), Is.True);
        Assert.That(SunGeometry.Covers(new Vector2(79f, 29f), view, coverage), Is.True);
        Assert.That(SunGeometry.Covers(new Vector2(81f, 0f), view, coverage), Is.False);
        Assert.That(SunGeometry.Covers(new Vector2(10f, -31f), view, coverage), Is.False);
    }

    private sealed class Vector2EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector2> {
        private readonly float tolerance;

        public Vector2EqualityComparer(float tolerance) => this.tolerance = tolerance;

        public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) <= tolerance;

        public int GetHashCode(Vector2 value) => 0;
    }
}
