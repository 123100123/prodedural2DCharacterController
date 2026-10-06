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

    // Whether the sweep of the feet from before to after is held by the platform's top, as a ray cast down from the feet
    // would find it: a top the ray reaches (at or above where the feet end up) and the support lets through.
    private static bool SweepLandsOnTop((float before, float after) feet, float catchDepth = 0.2f) {
        OneWaySupport support = OneWaySupport.Sweeping(feet, catchDepth, (null, 0f));
        bool isReached = Top >= feet.after;
        return isReached && OneWayFilter.Blocks((true, false), (Mathf.Max(0.001f, feet.before - Top), 1f, Top), (-1f, support.Level));
    }

    [Test]
    public void FeetCrossingATopFromAboveLandOnItAtAnyFallSpeed([Values(0.01f, 0.3f, 2f, 20f, 60f, 5000f)] float fall, [Values(0f, 0.4f, 3f)] float aboveTop) {
        float before = Top + aboveTop;
        Assert.That(SweepLandsOnTop((before, before - fall)), Is.EqualTo(fall >= aboveTop), $"fell {fall} from {aboveTop} above the top");
    }

    [Test]
    public void FeetThatOnlyJustSankBelowATopOnTheWayDownStillLandOnItWithinTheCatch() {
        Assert.That(SweepLandsOnTop((Top - 0.15f, Top - 0.5f)), Is.True, "just cleared it");
        Assert.That(SweepLandsOnTop((Top - 0.25f, Top - 0.5f)), Is.False, "well below it");
        Assert.That(SweepLandsOnTop((Top - 0.25f, Top - 0.5f), 0.3f), Is.True, "a wider catch");
    }

    [Test]
    public void ABodyRisingThroughAPlatformNeverCatchesIt() {
        Assert.That(SweepLandsOnTop((Top - 0.05f, Top + 0.5f)), Is.False, "feet just under the top, rising");
        Assert.That(SweepLandsOnTop((Top - 0.01f, Top + 3f), 5f), Is.False, "rising fast, however wide the catch");
        OneWaySupport rising = OneWaySupport.Sweeping((Top + 0.5f, Top + 0.7f), 0.2f, (null, 0f));
        Assert.That(OneWayFilter.Blocks((true, false), (0.5f, 1f, Top), (-1f, rising.Level)), Is.True, "still stands on a top just under the feet");
    }

    [Test]
    public void ABodyFallingThroughADroppedThroughPlatformIsNotCaught() {
        OneWaySupport support = OneWaySupport.Sweeping((Top + 1f, Top - 5f), 0.2f, (null, 0f));
        Assert.That(OneWayFilter.Blocks((true, true), (1f, 1f, Top), (-1f, support.Level)), Is.False);
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

    [Test]
    public void APassThroughColliderNeverBlocksAndIsSkippedByTheSolidChecks() {
        GameObject board = new GameObject("passThrough", typeof(BoxCollider2D));
        GameObject wall = new GameObject("solid", typeof(BoxCollider2D));
        try {
            Collider2D boardCollider = board.GetComponent<Collider2D>();
            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            Assert.That(CharacterPassThrough.AnyActive, Is.False, "nothing registered: the plain queries");
            Assert.That(OneWayFilter.IsSkipped(boardCollider), Is.False);

            CharacterPassThrough.Register(boardCollider);
            Assert.That(CharacterPassThrough.AnyActive, Is.True);
            Assert.That(OneWayFilter.IsSkipped(boardCollider), Is.True);
            Assert.That(OneWayFilter.IsSkipped(wallCollider), Is.False, "other colliders still block");
            Assert.That(OneWayFilter.AnySolid(new[] { boardCollider }, 1), Is.False, "headroom ignores it");
            Assert.That(OneWayFilter.AnySolid(new[] { boardCollider, wallCollider }, 2), Is.True, "but not the wall beside it");
            Assert.That(OneWayFilter.AnySolid(new[] { boardCollider, wallCollider }, 1), Is.False, "only the first count are looked at");

            CharacterPassThrough.Unregister(boardCollider);
            Assert.That(CharacterPassThrough.AnyActive, Is.False);
            Assert.That(OneWayFilter.IsSkipped(boardCollider), Is.False);
        }
        finally {
            CharacterPassThrough.Unregister(board.GetComponent<Collider2D>());
            Object.DestroyImmediate(board);
            Object.DestroyImmediate(wall);
        }
    }
}
