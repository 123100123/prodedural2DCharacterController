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
        Assert.That(original.walk.speed, Is.EqualTo(GaitProfile.Walk.speed));
        Assert.That(original.posture.maxTotalLean, Is.EqualTo(PostureSettings.Default.maxTotalLean));
        Assert.That(original.wall.maxSlideSpeed, Is.EqualTo(new WallSettings().maxSlideSpeed));
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
