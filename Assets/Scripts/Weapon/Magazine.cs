using UnityEngine;

// The rounds in a pistol and the reload that refills them. Time is passed in, so it needs no clock of its own.
public sealed class Magazine {
    private float reloadStart;
    private float reloadDuration;

    public Magazine(int capacity) {
        Capacity = Mathf.Max(1, capacity);
        Rounds = Capacity;
    }

    public int Capacity { get; private set; }
    public int Rounds { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsEmpty => Rounds <= 0;
    public bool IsFull => Rounds >= Capacity;

    // A tuned capacity takes effect at once; a full magazine stays full, a shrunk one drops the extra rounds.
    public void Resize(int capacity) {
        capacity = Mathf.Max(1, capacity);
        if (capacity == Capacity) {
            return;
        }
        bool wasFull = IsFull;
        Capacity = capacity;
        Rounds = wasFull ? capacity : Mathf.Min(Rounds, capacity);
    }

    // 0 at the start of a reload to 1 when it is done; 0 when not reloading.
    public float ReloadProgress(float now) {
        if (!IsReloading) {
            return 0f;
        }
        return reloadDuration <= 0f ? 1f : Mathf.Clamp01((now - reloadStart) / reloadDuration);
    }

    // False while already reloading or when the magazine is full.
    public bool TryStartReload(float now, float duration) {
        if (IsReloading || IsFull) {
            return false;
        }
        IsReloading = true;
        reloadStart = now;
        reloadDuration = Mathf.Max(0f, duration);
        return true;
    }

    // Takes a round for a shot; false when empty or mid-reload.
    public bool TryConsumeRound() {
        if (IsReloading || IsEmpty) {
            return false;
        }
        Rounds--;
        return true;
    }

    // Refills the magazine once the reload has run its duration; true on the call that finishes it.
    public bool CompleteReloadIfDue(float now) {
        if (!IsReloading || now - reloadStart < reloadDuration) {
            return false;
        }
        IsReloading = false;
        Rounds = Capacity;
        return true;
    }
}
