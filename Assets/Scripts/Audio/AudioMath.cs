using UnityEngine;

// The pure relations of the audio: how loud a step or landing is for its impact, how far a sound is panned, when the
// magazine seats during a reload, and which pooled voice a new sound takes.
public static class AudioMath {
    // The widest stereo pan a sound at the edge of the screen gets; the game is 2D, so it is only a hint.
    public const float MaxPan = 0.45f;
    private const float QuietestStep = 0.2f;
    private const float QuietestLanding = 0.35f;
    private const float DeepestLandingPitch = 0.8f;
    private const float LightestLandingPitch = 1.15f;
    // How long before the end of a reload the magazine seats, and the least share of the reload that passes before it does.
    private const float MagazineInLead = 0.3f;
    private const float MagazineInEarliestShare = 0.5f;

    // What the mix lets through for a category: 0 when muted, else the master volume times the category's own.
    public static float Gain(SoundCategory category, CharacterAudioSettings settings) {
        if (settings.mute) {
            return 0f;
        }
        float volume;
        switch (category) {
            case SoundCategory.Steps:
                volume = settings.stepsVolume;
                break;
            case SoundCategory.Landing:
                volume = settings.landingVolume;
                break;
            case SoundCategory.Weapon:
                volume = settings.weaponVolume;
                break;
            default:
                volume = settings.impactsVolume;
                break;
        }
        return Mathf.Max(0f, settings.masterVolume) * Mathf.Max(0f, volume);
    }

    public static SoundCategory CategoryOf(SoundId id) {
        switch (id) {
            case SoundId.Footstep:
            case SoundId.Jump:
            case SoundId.Roll:
                return SoundCategory.Steps;
            case SoundId.Landing:
                return SoundCategory.Landing;
            case SoundId.TargetHit:
            case SoundId.Ricochet:
                return SoundCategory.Impacts;
            default:
                return SoundCategory.Weapon;
        }
    }

    // 0 for a plant at or below the least impact speed (no sound), else rising from a quiet step to a full one. Walking and
    // crouching plant the feet more softly than running, so they come out quieter on their own.
    public static float FootstepLevel(float impactSpeed, CharacterAudioSettings settings) {
        float share = EffectMath.ImpactShare((impactSpeed, settings.footMinImpact, settings.footFullImpact));
        return share <= 0f ? 0f : Mathf.Lerp(QuietestStep, 1f, share);
    }

    // 0 for a landing at or below the least impact speed, else rising from a soft thump to a full one.
    public static float LandingLevel(float impactSpeed, CharacterAudioSettings settings) {
        float share = EffectMath.ImpactShare((impactSpeed, settings.landingMinImpact, settings.landingFullImpact));
        return share <= 0f ? 0f : Mathf.Lerp(QuietestLanding, 1f, share);
    }

    // A harder landing is deeper: the pitch falls as the impact grows.
    public static float LandingPitch(float impactSpeed, CharacterAudioSettings settings) {
        float share = EffectMath.ImpactShare((impactSpeed, settings.landingMinImpact, settings.landingFullImpact));
        return Mathf.Lerp(LightestLandingPitch, DeepestLandingPitch, share);
    }

    // Stereo pan in [-MaxPan, MaxPan] from where the sound is relative to the camera, a half screen width counting as full.
    public static float Pan(float x, (float cameraX, float halfWidth) view) {
        if (view.halfWidth <= 0f) {
            return 0f;
        }
        return Mathf.Clamp((x - view.cameraX) / view.halfWidth, -1f, 1f) * MaxPan;
    }

    // Seconds after a reload starts at which the magazine seats: near the end, but never before the middle of a short one.
    public static float MagazineInDelay(float reloadDuration) {
        float duration = Mathf.Max(0f, reloadDuration);
        return Mathf.Max(duration * MagazineInEarliestShare, duration - MagazineInLead);
    }

    // The voice for a new sound: the first one that has finished, otherwise the one that started longest ago.
    public static int PickVoice(float[] startedAt, float[] endsAt, float now) {
        int oldest = 0;
        for (int index = 0; index < startedAt.Length; index++) {
            if (endsAt[index] <= now) {
                return index;
            }
            if (startedAt[index] < startedAt[oldest]) {
                oldest = index;
            }
        }
        return oldest;
    }
}
