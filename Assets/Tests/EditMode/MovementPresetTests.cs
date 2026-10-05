using NUnit.Framework;

public class MovementPresetTests {
    private static MovementPreset Sample() {
        MovementPreset preset = MovementPreset.CodeDefaults();
        preset.run.lean = 17.5f;
        preset.body.verticalSpring.frequency = 7.25f;
        preset.head.stabilization = 0.35f;
        return preset;
    }

    [Test]
    public void SurvivesAJsonRoundTrip() {
        MovementPreset original = Sample();
        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
        Assert.That(loaded.run.lean, Is.EqualTo(17.5f));
        Assert.That(loaded.body.verticalSpring.frequency, Is.EqualTo(7.25f));
        Assert.That(loaded.head.stabilization, Is.EqualTo(0.35f));
    }

    [Test]
    public void WritesItsFormatVersion() {
        Assert.That(MovementPreset.FromJson(Sample().ToJson()).version, Is.EqualTo(MovementPreset.CurrentVersion));
        Assert.That(Sample().ToJson(), Does.Contain("\"version\""));
    }

    // A file saved before a setting existed (or by hand with only a few values) leaves the rest at the code defaults.
    [Test]
    public void ASettingTheFileDoesNotMentionKeepsItsDefault() {
        MovementPreset loaded = MovementPreset.FromJson("{\"run\":{\"lean\":21}}");
        MovementPreset defaults = MovementPreset.CodeDefaults();

        Assert.That(loaded.run.lean, Is.EqualTo(21f), "what the file says");
        Assert.That(loaded.run.speed, Is.EqualTo(defaults.run.speed), "a field of the same profile it leaves out");
        Assert.That(loaded.walk.speed, Is.EqualTo(defaults.walk.speed), "a profile it leaves out");
        Assert.That(loaded.body.verticalSpring.frequency, Is.EqualTo(defaults.body.verticalSpring.frequency));
        Assert.That(loaded.bodyHeight, Is.EqualTo(defaults.bodyHeight));
    }

    [Test]
    public void TheWallSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.wall.maxSlideSpeed = 5.5f;
        original.wall.jumpVelocity = new UnityEngine.Vector2(8.25f, 12.5f);
        original.wall.steeringLockout = 0.2f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.wall.maxSlideSpeed, Is.EqualTo(5.5f));
        Assert.That(loaded.wall.jumpVelocity, Is.EqualTo(new UnityEngine.Vector2(8.25f, 12.5f)));
        Assert.That(loaded.wall.steeringLockout, Is.EqualTo(0.2f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 6, before the wall settings existed, loads them at their defaults.
    [Test]
    public void AVersionSixPresetLoadsTheWallDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 19f;
        saved.version = 6;
        string versionSix = WithoutObject(saved.ToJson(), "wall");
        Assert.That(versionSix, Does.Not.Contain("\"wall\""));
        Assert.That(versionSix, Does.Contain("\"audio\""), "only the wall settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionSix);
        WallSettings defaults = new WallSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(6));
        Assert.That(loaded.version, Is.EqualTo(6), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(19f));
        Assert.That(loaded.wall, Is.Not.Null);
        Assert.That(loaded.wall.enabled, Is.True);
        Assert.That(loaded.wall.maxSlideSpeed, Is.EqualTo(defaults.maxSlideSpeed));
        Assert.That(loaded.wall.jumpVelocity, Is.EqualTo(defaults.jumpVelocity));
        Assert.That(loaded.wall.coyoteTime, Is.EqualTo(defaults.coyoteTime));
        Assert.That(loaded.wall.footReach, Is.EqualTo(defaults.footReach));
    }

    [Test]
    public void TheSlideSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.slide.boostSpeed = 11.5f;
        original.slide.friction = 6.25f;
        original.slide.leadFoot = new UnityEngine.Vector2(1.5f, 0.125f);
        original.slide.enabled = false;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.slide.boostSpeed, Is.EqualTo(11.5f));
        Assert.That(loaded.slide.friction, Is.EqualTo(6.25f));
        Assert.That(loaded.slide.leadFoot, Is.EqualTo(new UnityEngine.Vector2(1.5f, 0.125f)));
        Assert.That(loaded.slide.enabled, Is.False);
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 7, before the slide settings existed, loads them at their defaults.
    [Test]
    public void AVersionSevenPresetLoadsTheSlideDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 18f;
        saved.wall.maxSlideSpeed = 4.5f;
        saved.version = 7;
        string versionSeven = WithoutObject(saved.ToJson(), "slide");
        Assert.That(versionSeven, Does.Not.Contain("\"slide\""));
        Assert.That(versionSeven, Does.Contain("\"wall\""), "only the slide settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionSeven);
        SlideSettings defaults = new SlideSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(7));
        Assert.That(loaded.version, Is.EqualTo(7), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(18f));
        Assert.That(loaded.wall.maxSlideSpeed, Is.EqualTo(4.5f));
        Assert.That(loaded.slide, Is.Not.Null);
        Assert.That(loaded.slide.enabled, Is.True);
        Assert.That(loaded.slide.boostSpeed, Is.EqualTo(defaults.boostSpeed));
        Assert.That(loaded.slide.friction, Is.EqualTo(defaults.friction));
        Assert.That(loaded.slide.cooldown, Is.EqualTo(defaults.cooldown));
        Assert.That(loaded.slide.leadFoot, Is.EqualTo(defaults.leadFoot));
    }

    [Test]
    public void TheLedgeHangAndOneWaySettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.ledgeHang.regrabLockout = 0.45f;
        original.ledgeHang.swayAmplitude = 0.125f;
        original.ledgeHang.holdable = false;
        original.oneWay.dropThroughTime = 0.375f;
        original.oneWay.landingCatch = 0.5f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.ledgeHang.regrabLockout, Is.EqualTo(0.45f));
        Assert.That(loaded.ledgeHang.swayAmplitude, Is.EqualTo(0.125f));
        Assert.That(loaded.ledgeHang.holdable, Is.False);
        Assert.That(loaded.oneWay.dropThroughTime, Is.EqualTo(0.375f));
        Assert.That(loaded.oneWay.landingCatch, Is.EqualTo(0.5f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 8, before the ledge hang and one-way settings existed, loads them at their defaults.
    [Test]
    public void AVersionEightPresetLoadsTheLedgeHangAndOneWayDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 18.5f;
        saved.slide.boostSpeed = 9.5f;
        saved.version = 8;
        string versionEight = WithoutObject(WithoutObject(saved.ToJson(), "ledgeHang"), "oneWay");
        Assert.That(versionEight, Does.Not.Contain("\"ledgeHang\""));
        Assert.That(versionEight, Does.Not.Contain("\"oneWay\""));
        Assert.That(versionEight, Does.Contain("\"slide\""), "only the new settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionEight);
        LedgeHangSettings hangDefaults = new LedgeHangSettings();
        OneWaySettings oneWayDefaults = new OneWaySettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(8));
        Assert.That(loaded.version, Is.EqualTo(8), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(18.5f));
        Assert.That(loaded.slide.boostSpeed, Is.EqualTo(9.5f));
        Assert.That(loaded.ledgeHang, Is.Not.Null);
        Assert.That(loaded.ledgeHang.holdable, Is.True);
        Assert.That(loaded.ledgeHang.intentThreshold, Is.EqualTo(hangDefaults.intentThreshold));
        Assert.That(loaded.ledgeHang.regrabLockout, Is.EqualTo(hangDefaults.regrabLockout));
        Assert.That(loaded.ledgeHang.swayPeriod, Is.EqualTo(hangDefaults.swayPeriod));
        Assert.That(loaded.oneWay, Is.Not.Null);
        Assert.That(loaded.oneWay.dropThroughTime, Is.EqualTo(oneWayDefaults.dropThroughTime));
        Assert.That(loaded.oneWay.landingCatch, Is.EqualTo(oneWayDefaults.landingCatch));
    }

    // The JSON with one object-valued field (and the comma before it) removed.
    private static string WithoutObject(string json, string field) {
        int key = json.IndexOf("\"" + field + "\"", System.StringComparison.Ordinal);
        Assert.That(key, Is.GreaterThan(0), $"the file has {field}");
        int open = json.IndexOf('{', key);
        int depth = 0;
        int close = open;
        for (; close < json.Length; close++) {
            depth += json[close] == '{' ? 1 : json[close] == '}' ? -1 : 0;
            if (depth == 0) {
                break;
            }
        }
        int comma = json.LastIndexOf(',', key);
        return json.Substring(0, comma) + json.Substring(close + 1);
    }

    [Test]
    public void ACopyIsIndependent() {
        MovementPreset original = Sample();
        MovementPreset copy = original.Copy();
        copy.walk.speed = 99f;
        copy.posture.maxTotalLean = 1f;
        copy.wall.maxSlideSpeed = 99f;
        copy.slide.boostSpeed = 99f;
        copy.ledgeHang.regrabLockout = 99f;
        copy.oneWay.dropThroughTime = 99f;
        Assert.That(original.walk.speed, Is.EqualTo(GaitProfile.Walk.speed));
        Assert.That(original.ledgeHang.regrabLockout, Is.EqualTo(new LedgeHangSettings().regrabLockout));
        Assert.That(original.oneWay.dropThroughTime, Is.EqualTo(new OneWaySettings().dropThroughTime));
        Assert.That(original.posture.maxTotalLean, Is.EqualTo(PostureSettings.Default.maxTotalLean));
        Assert.That(original.wall.maxSlideSpeed, Is.EqualTo(new WallSettings().maxSlideSpeed));
        Assert.That(original.slide.boostSpeed, Is.EqualTo(new SlideSettings().boostSpeed));
    }

    [Test]
    public void ThePreviewHeadsForItsGaitSpeedInTheMoveOrFacingDirection() {
        (float walk, float run) speeds = (3.5f, 8.5f);
        GaitPreview walking = new GaitPreview((GaitPreview.Mode.Walk, null));
        GaitPreview running = new GaitPreview((GaitPreview.Mode.Run, null));
        GaitPreview slowRun = new GaitPreview((GaitPreview.Mode.Run, 5f));
        GaitPreview idle = new GaitPreview((GaitPreview.Mode.Idle, 5f));

        Assert.That(walking.IntendedSpeed((0f, -1, speeds.walk, speeds.run)), Is.EqualTo(-speeds.walk), "facing left");
        Assert.That(running.IntendedSpeed((1f, -1, speeds.walk, speeds.run)), Is.EqualTo(speeds.run), "the move input wins");
        Assert.That(slowRun.IntendedSpeed((0f, 1, speeds.walk, speeds.run)), Is.EqualTo(5f));
        Assert.That(idle.IntendedSpeed((1f, 1, speeds.walk, speeds.run)), Is.EqualTo(0f));
        Assert.That(GaitPreview.Off.IsActive, Is.False);
    }

    [Test]
    public void ClassifiesTheGaitFromMotionAndBlend() {
        Assert.That(LocomotionTelemetry.Classify((false, 0.5f)), Is.EqualTo(LocomotionTelemetry.Mode.Idle));
        Assert.That(LocomotionTelemetry.Classify((true, 0f)), Is.EqualTo(LocomotionTelemetry.Mode.Walk));
        Assert.That(LocomotionTelemetry.Classify((true, 0.5f)), Is.EqualTo(LocomotionTelemetry.Mode.Transition));
        Assert.That(LocomotionTelemetry.Classify((true, 1f)), Is.EqualTo(LocomotionTelemetry.Mode.Run));
    }
}
