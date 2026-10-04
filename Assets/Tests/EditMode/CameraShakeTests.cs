using NUnit.Framework;
using UnityEngine;

public class CameraShakeTests {
    private CameraFeelSettings settings;
    private CameraShake shake;

    [SetUp]
    public void Build() {
        settings = new CameraFeelSettings { traumaDecay = 2f, maxShakeOffset = 0.5f, maxShakeRoll = 3f };
        shake = new CameraShake(settings);
    }

    [Test]
    public void TraumaClampsToOne() {
        shake.AddTrauma(0.7f);
        shake.AddTrauma(0.7f);
        Assert.That(shake.Trauma, Is.EqualTo(1f));
    }

    [Test]
    public void ANegativeAmountAddsNothing() {
        shake.AddTrauma(0.4f);
        shake.AddTrauma(-1f);
        Assert.That(shake.Trauma, Is.EqualTo(0.4f));
    }

    [Test]
    public void TraumaDrainsLinearlyAndSettlesAtZeroOnTime() {
        shake.AddTrauma(1f);
        shake.Tick(0.25f);
        Assert.That(shake.Trauma, Is.EqualTo(0.5f).Within(1e-5f), "decay 2 per second");
        shake.Tick(0.25f);
        Assert.That(shake.Trauma, Is.EqualTo(0f).Within(1e-5f), "1 / decay seconds after a full hit");
        shake.Tick(1f);
        Assert.That(shake.Trauma, Is.EqualTo(0f), "never below zero");
    }

    [Test]
    public void TheOffsetAndRollAreZeroWithoutTrauma() {
        for (int step = 0; step < 20; step++) {
            shake.Tick(0.016f);
            Assert.That(shake.Offset, Is.EqualTo(Vector2.zero));
            Assert.That(shake.Roll, Is.EqualTo(0f));
        }
    }

    [Test]
    public void TheShakeStaysWithinTheMaximumAndFallsWithTheSquareOfTrauma() {
        shake.AddTrauma(1f);
        float peak = 0f;
        float peakRoll = 0f;
        for (int step = 0; step < 600; step++) {
            shake.AddTrauma(1f);
            shake.Tick(0.01f);
            Vector2 offset = shake.Offset;
            Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(settings.maxShakeOffset + 1e-5f));
            Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(settings.maxShakeOffset + 1e-5f));
            Assert.That(Mathf.Abs(shake.Roll), Is.LessThanOrEqualTo(settings.maxShakeRoll + 1e-5f));
            peak = Mathf.Max(peak, Mathf.Abs(offset.x), Mathf.Abs(offset.y));
            peakRoll = Mathf.Max(peakRoll, Mathf.Abs(shake.Roll));
        }
        Assert.That(peak, Is.GreaterThan(0.1f), "a full shake moves the camera");
        Assert.That(peakRoll, Is.GreaterThan(0.5f), "and rolls it");

        // The same noise sample at a quarter of the trauma is a sixteenth of the shove.
        CameraShake full = new CameraShake(settings);
        CameraShake quarter = new CameraShake(settings);
        settings.traumaDecay = 0f;
        full.AddTrauma(1f);
        quarter.AddTrauma(0.25f);
        full.Tick(0.37f);
        quarter.Tick(0.37f);
        Assert.That(quarter.Offset.x, Is.EqualTo(full.Offset.x / 16f).Within(1e-5f));
    }

    [Test]
    public void ShakeThatIsSwitchedOffTakesNoTraumaAndMovesNothing() {
        shake.AddTrauma(0.8f);
        settings.shakeEnabled = false;
        Assert.That(shake.Offset, Is.EqualTo(Vector2.zero), "switching it off stops it at once");
        shake.Tick(0.016f);
        Assert.That(shake.Trauma, Is.EqualTo(0f));
        shake.AddTrauma(1f);
        Assert.That(shake.Trauma, Is.EqualTo(0f));
    }

    [Test]
    public void TheShakeAmountScalesTheShake() {
        settings.traumaDecay = 0f;
        shake.AddTrauma(1f);
        shake.Tick(0.37f);
        Vector2 full = shake.Offset;
        settings.shakeAmount = 0.5f;
        Assert.That(shake.Offset.x, Is.EqualTo(full.x * 0.5f).Within(1e-5f));
        settings.shakeAmount = 0f;
        Assert.That(shake.Offset, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void LandingsAddTraumaOnlyAboveTheThresholdSpeed() {
        Assert.That(CameraShake.LandingTrauma(settings.landingTraumaThreshold - 1f, settings), Is.EqualTo(0f));
        Assert.That(CameraShake.LandingTrauma(settings.landingTraumaThreshold, settings), Is.EqualTo(0f));
        Assert.That(CameraShake.LandingTrauma(settings.landingTraumaThreshold + 4f, settings), Is.EqualTo(4f * settings.landingTraumaPerSpeed).Within(1e-5f));
        Assert.That(CameraShake.LandingTrauma(1000f, settings), Is.EqualTo(1f), "capped");
    }

    [Test]
    public void TheAimLeansAFractionOfTheWayToThePointer() {
        Vector2 lean = FollowCamera.AimLean((new Vector2(6f, -4f), 0.3f, new Vector2(3.5f, 2f)));
        Assert.That(lean.x, Is.EqualTo(1.8f).Within(1e-5f));
        Assert.That(lean.y, Is.EqualTo(-1.2f).Within(1e-5f));
    }

    [Test]
    public void TheAimLeanIsHeldWithinTheMaximumOnEachAxis() {
        Vector2 max = new Vector2(3.5f, 2f);
        Vector2 lean = FollowCamera.AimLean((new Vector2(-100f, 100f), 0.3f, max));
        Assert.That(lean, Is.EqualTo(new Vector2(-3.5f, 2f)));
        Assert.That(FollowCamera.AimLean((Vector2.zero, 0.3f, max)), Is.EqualTo(Vector2.zero), "pointer on the character");
    }

    [Test]
    public void TheCameraSettingsSurviveAPresetRoundTripAndCopy() {
        MovementPreset preset = MovementPreset.CodeDefaults();
        preset.cameraFeel.aimFraction = 0.45f;
        preset.cameraFeel.shakeEnabled = false;
        preset.cameraFeel.maxAimOffset = new Vector2(2f, 1f);

        MovementPreset loaded = MovementPreset.FromJson(preset.ToJson());
        Assert.That(loaded.cameraFeel.aimFraction, Is.EqualTo(0.45f));
        Assert.That(loaded.cameraFeel.shakeEnabled, Is.False);
        Assert.That(loaded.cameraFeel.maxAimOffset, Is.EqualTo(new Vector2(2f, 1f)));

        MovementPreset copy = preset.Copy();
        copy.cameraFeel.aimFraction = 0.9f;
        Assert.That(preset.cameraFeel.aimFraction, Is.EqualTo(0.45f), "a copy is independent");
    }

    [Test]
    public void APresetSavedBeforeTheCameraSettingsExistedKeepsTheirDefaults() {
        MovementPreset loaded = MovementPreset.FromJson("{\"version\":3,\"run\":{\"lean\":21}}");
        CameraFeelSettings defaults = new CameraFeelSettings();
        Assert.That(loaded.cameraFeel.aimFraction, Is.EqualTo(defaults.aimFraction));
        Assert.That(loaded.cameraFeel.shakeEnabled, Is.True);
        Assert.That(loaded.cameraFeel.firedTrauma, Is.EqualTo(defaults.firedTrauma));
    }
}
