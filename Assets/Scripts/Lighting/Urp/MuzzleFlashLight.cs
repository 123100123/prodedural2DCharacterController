using UnityEngine;
using UnityEngine.Rendering.Universal;

// The warm flash at the pistol's muzzle: lights up on every shot, from the position the Fired event reports, and dies
// away in a few frames. It only listens to the character's Fired event; the weapon knows nothing about the light, and
// the light never touches the ambient light. A new shot restarts the flash at full brightness.
public sealed class MuzzleFlashLight : MonoBehaviour {
    private readonly FlashEnvelope envelope = new FlashEnvelope();
    private Light2D flash;
    private CharacterDriver character;
    private LightingSettings settings;
    private CharacterEvents subscribedTo;
    // A shot fired this frame is shown at full brightness before the flash starts to age.
    private bool firedThisFrame;

    // The brightness of the flash right now.
    public float Intensity => flash != null && flash.enabled ? flash.intensity : 0f;
    // Shots seen since the scene loaded, for tests and tools.
    public int Shots { get; private set; }

    public void Initialize((Light2D flash, CharacterDriver character, LightingSettings settings) parts) {
        flash = parts.flash;
        character = parts.character;
        settings = parts.settings;
        flash.enabled = false;
        Subscribe();
    }

    // Reads the settings again (they were changed): the flash in flight keeps its place and fades with the new values.
    public void Refresh() {
        if (flash == null) {
            return;
        }
        flash.color = settings.muzzleColor;
        flash.shadowsEnabled = settings.muzzleCastsShadows;
        flash.shadowIntensity = settings.muzzleShadowIntensity;
        flash.falloffIntensity = 0.5f;
        Show();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable() {
        if (subscribedTo != null) {
            subscribedTo.Fired -= OnFired;
            subscribedTo = null;
        }
    }

    private void Subscribe() {
        CharacterEvents events = character != null ? character.Events : null;
        if (events == null || events == subscribedTo || !isActiveAndEnabled) {
            return;
        }
        if (subscribedTo != null) {
            subscribedTo.Fired -= OnFired;
        }
        events.Fired += OnFired;
        subscribedTo = events;
    }

    private void OnFired((Vector2 muzzle, Vector2 direction) shot) {
        Shots++;
        transform.position = new Vector3(shot.muzzle.x, shot.muzzle.y, transform.position.z);
        envelope.Trigger();
        firedThisFrame = true;
        Show();
    }

    // The flash is on screen the frame the shot was fired, at full brightness, and ages from the next frame on.
    private void LateUpdate() {
        if (flash == null) {
            return;
        }
        if (firedThisFrame) {
            firedThisFrame = false;
        }
        else {
            envelope.Advance(Time.deltaTime);
        }
        Show();
        // The character may have been composed after this component enabled.
        Subscribe();
    }

    private void Show() {
        if (!envelope.IsActive(settings)) {
            flash.enabled = false;
            return;
        }
        float level = envelope.Level(settings);
        flash.enabled = true;
        flash.intensity = settings.muzzlePeakIntensity * level;
        flash.pointLightOuterRadius = settings.muzzleOuterRadius * Mathf.Lerp(1f - settings.muzzleRadiusDecay, 1f, level);
        flash.pointLightInnerRadius = Mathf.Min(settings.muzzleInnerRadius, flash.pointLightOuterRadius - 0.01f);
    }
}
