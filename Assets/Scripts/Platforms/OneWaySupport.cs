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

    public bool Ignores(Collider2D platform) => passage != null && passage.Ignores(platform, now);
}
