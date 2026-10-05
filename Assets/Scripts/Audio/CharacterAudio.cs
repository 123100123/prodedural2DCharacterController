using System;
using System.Collections.Generic;
using UnityEngine;

// The character's sound, all synthesized in code: footsteps, landings, the jump and roll, the pistol, the reload, shell
// casings and bullet strikes. Like the effects it listens to the character's events (gameplay never calls it) and plays
// the clips of a SoundBank on a fixed pool of 2D voices, so nothing is allocated while playing. A new sound takes a free
// voice, or steals the oldest when all are busy. Each play varies a little in pitch and volume and pans slightly with where
// it happened on screen.
public sealed class CharacterAudio {
    public const int VoiceCount = 12;
    private const float CasingTinkleLevel = 0.3f;
    private const float RicochetLevel = 0.45f;
    private const float RicochetPitch = 1.6f;
    private const float TargetHitLevel = 0.9f;
    private const float JumpLevel = 0.5f;
    private const float RollLevel = 0.7f;
    private const float WallKickLevel = 0.9f;
    private const float WallCatchLevel = 0.5f;
    private const float DefaultHalfWidth = 10f;

    private readonly CharacterAudioSettings settings;
    private readonly CharacterRig rig;
    private readonly CharacterEvents events;
    private readonly CasingPool casings;
    private readonly GameObject root;
    private readonly SoundBank bank;
    private readonly AudioSource[] voices = new AudioSource[VoiceCount];
    private readonly float[] startedAt = new float[VoiceCount];
    private readonly float[] endsAt = new float[VoiceCount];
    private readonly int[] plays = new int[Enum.GetValues(typeof(SoundId)).Length];
    private readonly int[] lastVariant = new int[Enum.GetValues(typeof(SoundId)).Length];
    private readonly List<IShotReceiver> receivers = new List<IShotReceiver>();
    private AudioListener addedListener;
    private Camera viewCamera;
    private float clock;
    private bool magazineInPending;
    private float magazineInAt;

    public CharacterAudio(CharacterRig rig, CharacterAudioSettings settings, CasingPool casings) {
        this.rig = rig;
        this.settings = settings;
        this.casings = casings;
        events = rig.Events;
        root = new GameObject("Audio");
        int rate = UnityEngine.AudioSettings.outputSampleRate;
        bank = new SoundBank(rate > 0 ? rate : ClipSynth.DefaultSampleRate);
        for (int index = 0; index < VoiceCount; index++) {
            voices[index] = CreateVoice();
            endsAt[index] = float.NegativeInfinity;
            startedAt[index] = float.NegativeInfinity;
        }
        EnsureListener();
        Subscribe();
    }

    // Raised for every sound that is played (not for one the mix silences), for tests and tools.
    public event Action<SoundId> Played;

    public int TotalPlays {
        get {
            int total = 0;
            for (int index = 0; index < plays.Length; index++) {
                total += plays[index];
            }
            return total;
        }
    }

    // True when the scene had no audio listener and this host added one to the main camera.
    public bool AddedListener => addedListener != null;
    public int PlaysOf(SoundId id) => plays[(int)id];

    public void Tick(float deltaTime) {
        clock += deltaTime;
        if (magazineInPending && clock >= magazineInAt) {
            magazineInPending = false;
            PlayAt(SoundId.MagazineIn, (1f, 1f), rig.Body.position.x);
        }
    }

    public void Dispose() {
        Unsubscribe();
        bank.Dispose();
        Discard(addedListener);
        Discard(root);
    }

    private void Subscribe() {
        events.FootPlanted += OnFootPlanted;
        events.Landed += OnLanded;
        events.Jumped += OnJumped;
        events.Rolled += OnRolled;
        events.WallJumped += OnWallJumped;
        events.WallSlideStarted += OnWallSlideStarted;
        events.Fired += OnFired;
        events.DryFired += OnDryFired;
        events.ShotHit += OnShotHit;
        events.ReloadStarted += OnReloadStarted;
        events.ReloadFinished += OnReloadFinished;
        casings.FirstContact += OnCasingContact;
    }

    private void Unsubscribe() {
        events.FootPlanted -= OnFootPlanted;
        events.Landed -= OnLanded;
        events.Jumped -= OnJumped;
        events.Rolled -= OnRolled;
        events.WallJumped -= OnWallJumped;
        events.WallSlideStarted -= OnWallSlideStarted;
        events.Fired -= OnFired;
        events.DryFired -= OnDryFired;
        events.ShotHit -= OnShotHit;
        events.ReloadStarted -= OnReloadStarted;
        events.ReloadFinished -= OnReloadFinished;
        casings.FirstContact -= OnCasingContact;
    }

    // ---- Events ---------------------------------------------------------------------------------------------

    private void OnFootPlanted((Vector2 position, float impactSpeed) plant) {
        float level = AudioMath.FootstepLevel(plant.impactSpeed, settings);
        if (level > 0f) {
            PlayAt(SoundId.Footstep, (level, 1f), plant.position.x);
        }
    }

    private void OnLanded((Vector2 position, float impactSpeed) landing) {
        float level = AudioMath.LandingLevel(landing.impactSpeed, settings);
        if (level > 0f) {
            PlayAt(SoundId.Landing, (level, AudioMath.LandingPitch(landing.impactSpeed, settings)), landing.position.x);
        }
    }

    private void OnJumped(Vector2 launchVelocity) => PlayAt(SoundId.Jump, (JumpLevel, 1f), rig.Body.position.x);

    private void OnRolled((Vector2 position, int direction) roll) => PlayAt(SoundId.Roll, (RollLevel, 1f), roll.position.x);

    // The feet striking the wall, then the whoosh of the jump.
    private void OnWallJumped((Vector2 position, int direction) kick) {
        PlayAt(SoundId.Footstep, (WallKickLevel, 0.9f), kick.position.x);
        PlayAt(SoundId.Jump, (JumpLevel, 1f), kick.position.x);
    }

    // A soft scuff as the feet catch the wall.
    private void OnWallSlideStarted((Vector2 contact, int wallSide) slide) => PlayAt(SoundId.Footstep, (WallCatchLevel, 0.8f), slide.contact.x);

    private void OnFired((Vector2 muzzle, Vector2 direction) shot) => PlayAt(SoundId.Gunshot, (1f, 1f), shot.muzzle.x);

    private void OnDryFired((Vector2 muzzle, Vector2 direction) shot) => PlayAt(SoundId.DryClick, (0.8f, 1f), shot.muzzle.x);

    // The magazine drops at once; the new one seats near the end of the reload (see Tick).
    private void OnReloadStarted(float duration) {
        PlayAt(SoundId.MagazineOut, (0.8f, 1f), rig.Body.position.x);
        magazineInPending = true;
        magazineInAt = clock + AudioMath.MagazineInDelay(duration);
    }

    // A reload that ends before its scheduled click (a short one, or a long frame) still seats the magazine.
    private void OnReloadFinished(int rounds) {
        if (magazineInPending) {
            magazineInPending = false;
            PlayAt(SoundId.MagazineIn, (1f, 1f), rig.Body.position.x);
        }
    }

    // A thud when the bullet struck something that reacts to it, a high ricochet tick when it struck plain level.
    private void OnShotHit(ShotHit hit) {
        if (HasReceiver(hit.Collider)) {
            PlayAt(SoundId.TargetHit, (TargetHitLevel, 1f), hit.Point.x);
        }
        else {
            PlayAt(SoundId.Ricochet, (RicochetLevel, RicochetPitch), hit.Point.x);
        }
    }

    private void OnCasingContact(Vector2 position) => PlayAt(SoundId.Casing, (CasingTinkleLevel, UnityEngine.Random.Range(0.9f, 1.15f)), position.x);

    private bool HasReceiver(Collider2D collider) {
        if (collider == null) {
            return false;
        }
        collider.GetComponents(receivers);
        if (receivers.Count > 0) {
            return true;
        }
        Rigidbody2D body = collider.attachedRigidbody;
        if (body == null || body.gameObject == collider.gameObject) {
            return false;
        }
        body.GetComponents(receivers);
        return receivers.Count > 0;
    }

    // ---- Playback -------------------------------------------------------------------------------------------

    // Plays one variant of a sound at a level (before the mix) and a pitch, panned for where it happened.
    private void PlayAt(SoundId id, (float level, float pitch) tone, float x) {
        float gain = AudioMath.Gain(AudioMath.CategoryOf(id), settings) * tone.level;
        if (gain <= 0f) {
            return;
        }
        float jitter = settings.pitchJitter;
        float pitch = Mathf.Max(0.1f, tone.pitch * (1f + UnityEngine.Random.Range(-jitter, jitter)));
        float volume = Mathf.Clamp01(gain * (1f + UnityEngine.Random.Range(-jitter, jitter) * 0.5f));
        AudioClip clip = bank.Clip(id, NextVariant(id));

        int voice = AudioMath.PickVoice(startedAt, endsAt, clock);
        AudioSource source = voices[voice];
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.panStereo = AudioMath.Pan(x, ViewOf());
        source.Play();
        startedAt[voice] = clock;
        endsAt[voice] = clock + clip.length / pitch;

        plays[(int)id]++;
        Played?.Invoke(id);
    }

    // A random variant that is not the one played last, so a run of footsteps never repeats a step.
    private int NextVariant(SoundId id) {
        int count = bank.VariantCount(id);
        if (count <= 1) {
            return 0;
        }
        int variant = (lastVariant[(int)id] + 1 + UnityEngine.Random.Range(0, count - 1)) % count;
        lastVariant[(int)id] = variant;
        return variant;
    }

    private (float cameraX, float halfWidth) ViewOf() {
        if (viewCamera == null) {
            viewCamera = Camera.main;
        }
        if (viewCamera == null) {
            return (rig.Body.position.x, DefaultHalfWidth);
        }
        float halfWidth = viewCamera.orthographic ? viewCamera.orthographicSize * viewCamera.aspect : DefaultHalfWidth;
        return (viewCamera.transform.position.x, halfWidth);
    }

    // ---- Setup ----------------------------------------------------------------------------------------------

    private AudioSource CreateVoice() {
        AudioSource source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 100;
        return source;
    }

    // Sound needs an audio listener; the scene's camera normally has one, and one is added only when nothing does.
    private void EnsureListener() {
        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null) {
            return;
        }
        Camera camera = Camera.main;
        GameObject host = camera != null ? camera.gameObject : root;
        addedListener = host.AddComponent<AudioListener>();
    }

    // Edit-mode tests build the host outside play mode, where Destroy is not allowed.
    private static void Discard(UnityEngine.Object unwanted) {
        if (unwanted == null) {
            return;
        }
        if (Application.isPlaying) {
            UnityEngine.Object.Destroy(unwanted);
        }
        else {
            UnityEngine.Object.DestroyImmediate(unwanted);
        }
    }
}
