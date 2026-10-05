using NUnit.Framework;
using UnityEngine;

public class OneWayPlatformTests {
    private const float Top = 2f;

    // A ray down onto the top of the platform from feet at the given level.
    private static bool DownOntoTop(float supportLevel, (bool isOneWay, bool isIgnored) platform) {
        return OneWayFilter.Blocks(platform, (1f, 1f, Top), (-1f, supportLevel));
    }

    [Test]
    public void SolidCollidersAlwaysBlock() {
        Assert.That(OneWayFilter.Blocks((false, false), (0f, -1f, Top), (1f, float.NegativeInfinity)), Is.True);
    }

    [Test]
    public void APlatformHoldsFeetOnOrAboveItsTop() {
        Assert.That(DownOntoTop(Top + 1f, (true, false)), Is.True, "falling onto it from above");
        Assert.That(DownOntoTop(Top, (true, false)), Is.True, "standing on it");
        Assert.That(DownOntoTop(Top - 0.3f, (true, false)), Is.False, "feet still below the top: rising or passing through");
        Assert.That(DownOntoTop(float.NegativeInfinity, (true, false)), Is.False, "queries that never stand on platforms");
    }

    [Test]
    public void APlatformNeverBlocksFromBelowOrTheSideOrFromInside() {
        Assert.That(OneWayFilter.Blocks((true, false), (1f, -1f, Top - 0.2f), (1f, 100f)), Is.False, "moving up into its underside");
        Assert.That(OneWayFilter.Blocks((true, false), (1f, 0f, Top - 0.1f), (0f, 100f)), Is.False, "moving sideways into its end");
        Assert.That(OneWayFilter.Blocks((true, false), (0f, 1f, Top), (-1f, 100f)), Is.False, "a ray starting inside it");
        Assert.That(OneWayFilter.Blocks((true, false), (1f, 0.2f, Top), (-1f, 100f)), Is.False, "a steep side is not a top");
    }

    [Test]
    public void APlatformDroppedThroughDoesNotHold() {
        Assert.That(DownOntoTop(Top + 1f, (true, true)), Is.False);
    }

    [Test]
    public void DownAndJumpDropsOnlyWhenEveryFootIsOnAOneWayPlatform() {
        Assert.That(OneWayPassage.CanDropThrough((true, true), (true, true)), Is.True);
        Assert.That(OneWayPassage.CanDropThrough((true, true), (false, false)), Is.True, "the other foot in the air");
        Assert.That(OneWayPassage.CanDropThrough((true, true), (true, false)), Is.False, "one foot on solid ground");
        Assert.That(OneWayPassage.CanDropThrough((false, false), (false, false)), Is.False, "nothing under the feet");
    }

    [Test]
    public void ADroppedThroughPlatformIsIgnoredForTheDropTimeOnly() {
        GameObject platform = new GameObject("platform");
        GameObject other = new GameObject("other");
        try {
            BoxCollider2D platformCollider = platform.AddComponent<BoxCollider2D>();
            BoxCollider2D otherCollider = other.AddComponent<BoxCollider2D>();
            OneWayPassage passage = new OneWayPassage(new OneWaySettings { dropThroughTime = 0.25f });
            Assert.That(passage.Ignores(platformCollider, 0f), Is.False, "nothing dropped through yet");

            passage.DropThrough((platformCollider, null), 5f);
            Assert.That(passage.IsDropping(5.1f), Is.True);
            Assert.That(passage.Ignores(platformCollider, 5.2f), Is.True);
            Assert.That(passage.Ignores(otherCollider, 5.2f), Is.False, "another platform below still holds");
            Assert.That(passage.Ignores(null, 5.2f), Is.False);
            Assert.That(passage.Ignores(platformCollider, 5.25f), Is.False, "held again after the drop time");

            OneWaySupport support = new OneWaySupport(10f, (passage, 5.1f));
            Assert.That(support.Ignores(platformCollider), Is.True, "a support carries the passage");
            Assert.That(OneWaySupport.At(10f).Ignores(platformCollider), Is.False);
        }
        finally {
            Object.DestroyImmediate(platform);
            Object.DestroyImmediate(other);
        }
    }
}
