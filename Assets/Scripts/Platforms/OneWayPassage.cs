using UnityEngine;

// The one-way platforms a character is dropping through (down + jump) and until when: its ground queries ignore them for
// that long, by which time the feet are well below their tops and the platforms no longer hold them anyway.
public sealed class OneWayPassage {
    private (Collider2D first, Collider2D second) platforms;
    private float until = float.NegativeInfinity;

    public OneWayPassage(OneWaySettings settings) {
        Settings = settings;
    }

    public OneWaySettings Settings { get; }

    // Down + jump drops through only when everything under the feet is one-way: one foot on solid ground keeps it there.
    public static bool CanDropThrough((bool hasGround, bool isOneWay) front, (bool hasGround, bool isOneWay) back) {
        bool anyGround = front.hasGround || back.hasGround;
        return anyGround && (!front.hasGround || front.isOneWay) && (!back.hasGround || back.isOneWay);
    }

    public bool IsDropping(float now) => now < until;

    public void DropThrough((Collider2D first, Collider2D second) under, float now) {
        platforms = under;
        until = now + Mathf.Max(0f, Settings.dropThroughTime);
    }

    public bool Ignores(Collider2D platform, float now) {
        return platform != null && now < until && (platform == platforms.first || platform == platforms.second);
    }

    public void Clear() {
        platforms = (null, null);
        until = float.NegativeInfinity;
    }
}
