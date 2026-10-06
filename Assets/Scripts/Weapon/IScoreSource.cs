using UnityEngine;

// Points a shot earned: how many, where the target is (the popup floats up from there), and whether the bullet broke it.
public readonly struct ScoreAward {
    public ScoreAward(int points, Vector2 position, bool broke) {
        Points = points;
        Position = position;
        Broke = broke;
    }

    public int Points { get; }
    public Vector2 Position { get; }
    public bool Broke { get; }
}

// Implemented, next to IShotReceiver, by a target that pays out for being shot. The weapon asks once after notifying the
// receivers and raises CharacterEvents.TargetScored with what it gets, so a target never knows the character, its sound
// or its effects. A target that scores also makes its own hit sound (the weapon's generic thud stays silent for it).
public interface IScoreSource {
    // The award the last shot earned, once: false when nothing is waiting.
    bool TryTakeScore(out ScoreAward award);
}
