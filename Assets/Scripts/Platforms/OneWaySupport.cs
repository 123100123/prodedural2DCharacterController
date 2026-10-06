using UnityEngine;

// What a query stands on, for one-way platforms: a platform whose top is at or below Level holds it (the feet are on or
// above that top), a higher one is passed through. Platforms the character is dropping through are ignored.
public readonly struct OneWaySupport {
    private readonly OneWayPassage passage;
    private readonly float now;

    public OneWaySupport(float level, (OneWayPassage passage, float now) dropping) {
        Level = level;
        passage = dropping.passage;
        now = dropping.now;
    }

    public float Level { get; }

    // One-way platforms never hold this query: the motor's hull, wall and ledge searches.
    public static OneWaySupport None => new OneWaySupport(float.NegativeInfinity, (null, 0f));

    // Any platform top at or below the level holds the query.
    public static OneWaySupport At(float level) => new OneWaySupport(level, (null, 0f));

    // A body in flight whose feet are at feetBefore as the frame starts and travel to feetAfter. Swept: a platform whose top
    // the feet cross from above holds them, whatever the distance they cover in the frame (a top at or below feetBefore
    // is within the sweep of any fall that reaches it). A top the feet have already sunk below by up to catchDepth holds
    // them too, for a jump that only just cleared it, but only on the way down: a body rising (feetAfter above
    // feetBefore) never catches a platform above its feet, it passes up through it, and keeps only the ground it stands on.
    public static OneWaySupport Sweeping((float before, float after) feet, float catchDepth, (OneWayPassage passage, float now) dropping) {
        bool isRising = feet.after > feet.before;
        float grace = isRising ? OneWayFilter.StandingTolerance : Mathf.Max(0f, catchDepth);
        return new OneWaySupport(feet.before + grace, dropping);
    }

    public bool Ignores(Collider2D platform) => passage != null && passage.Ignores(platform, now);
}
