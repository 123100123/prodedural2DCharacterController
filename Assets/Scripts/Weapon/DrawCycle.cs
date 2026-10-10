using UnityEngine;

public enum DrawState {
    // In the holster; the hand is free.
    Holstered,
    // The hand reaches for the holster (before the grip share), then carries the pistol up to the aim.
    Drawing,
    // Held at the aim: the shooting stance has both hands and the pistol can fire.
    Equipped,
    // The reverse of Drawing: the pistol goes down into the holster (at the insert share), then the hand lets go.
    Holstering,
}

// What one tick of the draw cycle did: the pistol changed hands (attach to the hand, detach into the holster), and
// whether a shot is to be fired this frame (pressed now while equipped, or a press buffered during the draw).
public readonly struct DrawStep {
    public DrawStep((bool attached, bool detached) parent, bool fireNow) {
        Attached = parent.attached;
        Detached = parent.detached;
        FireNow = fireNow;
    }

    public bool Attached { get; }
    public bool Detached { get; }
    public bool FireNow { get; }
}

// The draw and holster cycle on its own (no scene objects): Holstered -> Drawing -> Equipped -> Holstering -> Holstered,
// each of Drawing and Holstering with a progress p from 0 to 1, and the rules for every interruption (plan §13).
// Drawing and Holstering run along the same path in opposite directions: Drawing's grip share and Holstering's insert
// share are the same point on it (the hand at the holster). Turning around mid-way continues from the mirrored progress,
// so the hand never jumps; PathPosition is that common place on the path (0 hand free, 1 at the aim).
public sealed class DrawCycle {
    private const float MinShare = 0.01f;
    private const float MaxShare = 0.99f;

    private readonly PistolHandlingSettings settings;

    public DrawCycle(PistolHandlingSettings settings) {
        this.settings = settings;
    }

    public DrawState State { get; private set; } = DrawState.Holstered;
    // Progress through the current Drawing or Holstering (0 to 1); 0 holstered, 1 equipped.
    public float Progress { get; private set; }
    // Holstering at the quick time because the full-body state took the arms (roll, climb, wall slide).
    public bool IsQuick { get; private set; }
    // A fire press made during the draw, waiting for the pistol to be at the aim.
    public bool FireBuffered { get; private set; }

    public bool CanFire => State == DrawState.Equipped;

    // The pistol is in the hand from the grip on the way up to the insert on the way down.
    public bool ShouldBeInHand {
        get {
            switch (State) {
                case DrawState.Equipped:
                    return true;
                case DrawState.Drawing:
                    return Progress >= GripShare;
                case DrawState.Holstering:
                    return Progress < InsertShare;
                default:
                    return false;
            }
        }
    }

    // Where the hand is on the shared path, in Drawing's progress: 0 hand free (swing), GripShare at the holster, 1 at the
    // aim. Holstering's progress maps onto it piecewise (its insert share is the grip share).
    public float PathPosition {
        get {
            switch (State) {
                case DrawState.Equipped:
                    return 1f;
                case DrawState.Drawing:
                    return Progress;
                case DrawState.Holstering:
                    return HolsteringToPath(Progress);
                default:
                    return 0f;
            }
        }
    }

    // How far the aim pose has taken over: 0 holstered, 1 equipped (the shooting stance poses the arms only at 1).
    public float AimShare => PathPosition;

    // How much the support hand is on the pistol: it joins from the support-join share of the draw up to the aim, and
    // lets go at the mirrored point of the holstering (by about 0.3 of it).
    public float SupportWeight => SupportWeightAt(PathPosition, settings);

    public float GripShare => Mathf.Clamp(settings.gripShare, MinShare, MaxShare);
    public float InsertShare => Mathf.Clamp(settings.insertShare, MinShare, MaxShare);

    public static float SupportWeightAt(float pathPosition, PistolHandlingSettings settings) {
        float join = Mathf.Clamp(settings.supportJoinShare, 0f, MaxShare);
        return Smooth(Mathf.InverseLerp(join, 1f, pathPosition));
    }

    // One frame: aim held, fire pressed this frame, whether the full-body state lets the upper body act.
    public DrawStep Tick((bool aim, bool fire, bool allowsActions) intent, float deltaTime) {
        bool wasInHand = ShouldBeInHand;
        // A buffered shot belongs to this aim: letting go of aim, or a state that takes the arms, drops it.
        if (!intent.aim || !intent.allowsActions) {
            FireBuffered = false;
        }
        Steer(intent);
        Advance(deltaTime);

        bool fireNow = false;
        if (State == DrawState.Equipped) {
            // Pressed now, or pressed during the draw that has just reached the aim: one shot either way.
            fireNow = intent.fire || FireBuffered;
            FireBuffered = false;
        } else if (intent.fire && intent.aim && intent.allowsActions && State == DrawState.Drawing) {
            // One press is kept; more presses during the same draw do not stack.
            FireBuffered = true;
        }

        bool inHand = ShouldBeInHand;
        return new DrawStep((!wasInHand && inHand, wasInHand && !inHand), fireNow);
    }

    // Switches direction or speed from what is wanted now, before the progress moves.
    private void Steer((bool aim, bool fire, bool allowsActions) intent) {
        bool wantsDrawn = intent.aim && intent.allowsActions;
        switch (State) {
            case DrawState.Holstered:
                if (wantsDrawn) {
                    Begin(DrawState.Drawing, 0f);
                }
                break;
            case DrawState.Drawing:
                if (wantsDrawn) {
                    break;
                }
                if (Progress < GripShare) {
                    // The pistol never left the holster: the hand just goes back to its swing.
                    Begin(DrawState.Holstered, 0f);
                } else {
                    Begin(DrawState.Holstering, PathToHolstering(Progress));
                    IsQuick = !intent.allowsActions;
                }
                break;
            case DrawState.Equipped:
                if (!wantsDrawn) {
                    Begin(DrawState.Holstering, 0f);
                    IsQuick = !intent.allowsActions;
                }
                break;
            case DrawState.Holstering:
                if (wantsDrawn) {
                    // Before the insert the hand turns back up with the pistol; after it, the draw starts again from the
                    // hand's place on the way out of the holster, so the reach back to the grip is short.
                    Begin(DrawState.Drawing, HolsteringToPath(Progress));
                } else if (!intent.allowsActions) {
                    IsQuick = true;
                }
                break;
        }
    }

    private void Advance(float deltaTime) {
        if (State == DrawState.Drawing) {
            Progress = Step(Progress, settings.drawDuration, deltaTime);
            if (Progress >= 1f) {
                Begin(DrawState.Equipped, 1f);
            }
        } else if (State == DrawState.Holstering) {
            Progress = Step(Progress, IsQuick ? settings.quickHolsterDuration : settings.holsterDuration, deltaTime);
            if (Progress >= 1f) {
                Begin(DrawState.Holstered, 0f);
            }
        }
    }

    private void Begin(DrawState state, float progress) {
        State = state;
        Progress = Mathf.Clamp01(progress);
        if (state != DrawState.Holstering) {
            IsQuick = false;
        }
    }

    // Holstering's progress on the shared path: its [0, insert] is the carry down from the aim (path 1 to grip), its
    // [insert, 1] the hand letting go (path grip to 0).
    private float HolsteringToPath(float progress) {
        float grip = GripShare;
        float insert = InsertShare;
        if (progress < insert) {
            return 1f - (1f - grip) * (progress / insert);
        }
        return grip * (1f - progress) / (1f - insert);
    }

    private float PathToHolstering(float path) {
        float grip = GripShare;
        float insert = InsertShare;
        if (path >= grip) {
            return insert * (1f - path) / (1f - grip);
        }
        return 1f - (1f - insert) * (path / grip);
    }

    private static float Step(float progress, float duration, float deltaTime) {
        return duration <= 0f ? 1f : Mathf.Clamp01(progress + deltaTime / duration);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
