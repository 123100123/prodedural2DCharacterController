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

    [Test]
    public void ThePushSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.push.pushSpeed = 1.75f;
        original.push.maxForce = 512f;
        original.push.contactSkin = 0.125f;
        original.push.lean = 18.5f;
        original.push.handHeight = -0.625f;
        original.push.blendTime = 0.25f;
        original.push.enabled = false;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.push.pushSpeed, Is.EqualTo(1.75f));
        Assert.That(loaded.push.maxForce, Is.EqualTo(512f));
        Assert.That(loaded.push.contactSkin, Is.EqualTo(0.125f));
        Assert.That(loaded.push.lean, Is.EqualTo(18.5f));
        Assert.That(loaded.push.handHeight, Is.EqualTo(-0.625f));
        Assert.That(loaded.push.blendTime, Is.EqualTo(0.25f));
        Assert.That(loaded.push.enabled, Is.False);
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 9, before the push settings existed, loads them at their defaults.
    [Test]
    public void AVersionNinePresetLoadsThePushDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 17.5f;
        saved.oneWay.dropThroughTime = 0.375f;
        saved.version = 9;
        string versionNine = WithoutObject(saved.ToJson(), "push");
        Assert.That(versionNine, Does.Not.Contain("\"push\""));
        Assert.That(versionNine, Does.Contain("\"oneWay\""), "only the push settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionNine);
        PushSettings defaults = new PushSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(9));
        Assert.That(loaded.version, Is.EqualTo(9), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(17.5f));
        Assert.That(loaded.oneWay.dropThroughTime, Is.EqualTo(0.375f));
        Assert.That(loaded.push, Is.Not.Null);
        Assert.That(loaded.push.enabled, Is.True);
        Assert.That(loaded.push.pushSpeed, Is.EqualTo(defaults.pushSpeed));
        Assert.That(loaded.push.maxForce, Is.EqualTo(defaults.maxForce));
        Assert.That(loaded.push.contactSkin, Is.EqualTo(defaults.contactSkin));
        Assert.That(loaded.push.releaseDistance, Is.EqualTo(defaults.releaseDistance));
        Assert.That(loaded.push.lean, Is.EqualTo(defaults.lean));
        Assert.That(loaded.push.blendTime, Is.EqualTo(defaults.blendTime));
    }

    [Test]
    public void TheRollAndClimbSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.roll.enabled = true;
        original.roll.cooldown = 0.625f;
        original.roll.diveSpeed = 7.75f;
        original.roll.leadFootTucked = new UnityEngine.Vector2(0.5f, -0.875f);
        original.roll.recoverCrouch = 0.5f;
        original.climb.maxGrabRise = 5.25f;
        original.climb.pullDuration = 0.5f;
        original.climb.mounted = new UnityEngine.Vector2(0.375f, 1.75f);
        original.climb.hangDrop = 2.75f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.roll.enabled, Is.True);
        Assert.That(loaded.roll.cooldown, Is.EqualTo(0.625f));
        Assert.That(loaded.roll.diveSpeed, Is.EqualTo(7.75f));
        Assert.That(loaded.roll.leadFootTucked, Is.EqualTo(new UnityEngine.Vector2(0.5f, -0.875f)));
        Assert.That(loaded.roll.recoverCrouch, Is.EqualTo(0.5f));
        Assert.That(loaded.climb.maxGrabRise, Is.EqualTo(5.25f));
        Assert.That(loaded.climb.pullDuration, Is.EqualTo(0.5f));
        Assert.That(loaded.climb.mounted, Is.EqualTo(new UnityEngine.Vector2(0.375f, 1.75f)));
        Assert.That(loaded.climb.hangDrop, Is.EqualTo(2.75f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 10, before the roll and climb settings existed, loads them at their code defaults:
    // the roll stays off, whatever the character in the scene had.
    [Test]
    public void AVersionTenPresetLoadsTheRollAndClimbDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 16.5f;
        saved.push.pushSpeed = 2.25f;
        saved.version = 10;
        string versionTen = WithoutObject(WithoutObject(saved.ToJson(), "roll"), "climb");
        Assert.That(versionTen, Does.Not.Contain("\"roll\""));
        Assert.That(versionTen, Does.Not.Contain("\"climb\""));
        Assert.That(versionTen, Does.Contain("\"push\""), "only the new settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionTen);
        RollSettings rollDefaults = new RollSettings();
        ClimbSettings climbDefaults = new ClimbSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(10));
        Assert.That(loaded.version, Is.EqualTo(10), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(16.5f));
        Assert.That(loaded.push.pushSpeed, Is.EqualTo(2.25f));
        Assert.That(loaded.roll, Is.Not.Null);
        Assert.That(loaded.roll.enabled, Is.True, "the code default");
        Assert.That(loaded.roll.cooldown, Is.EqualTo(rollDefaults.cooldown));
        Assert.That(loaded.roll.diveSpeed, Is.EqualTo(rollDefaults.diveSpeed));
        Assert.That(loaded.roll.handsTucked, Is.EqualTo(rollDefaults.handsTucked));
        Assert.That(loaded.climb, Is.Not.Null);
        Assert.That(loaded.climb.maxGrabRise, Is.EqualTo(climbDefaults.maxGrabRise));
        Assert.That(loaded.climb.pullDuration, Is.EqualTo(climbDefaults.pullDuration));
        Assert.That(loaded.climb.standingRoom, Is.EqualTo(climbDefaults.standingRoom));
    }

    [Test]
    public void TheSlopeSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.slope.maxSlopeAngle = 42.5f;
        original.slope.uphillSpeedFactor = 0.625f;
        original.slope.strideShortening = 0.375f;
        original.slope.ankleBlendTime = 0.125f;
        original.slope.slideMaxSpeed = 13.5f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.slope.maxSlopeAngle, Is.EqualTo(42.5f));
        Assert.That(loaded.slope.uphillSpeedFactor, Is.EqualTo(0.625f));
        Assert.That(loaded.slope.strideShortening, Is.EqualTo(0.375f));
        Assert.That(loaded.slope.ankleBlendTime, Is.EqualTo(0.125f));
        Assert.That(loaded.slope.slideMaxSpeed, Is.EqualTo(13.5f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 11, before slopes existed, loads the slope settings at their code defaults.
    [Test]
    public void AVersionElevenPresetLoadsTheSlopeDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 15.5f;
        saved.roll.diveSpeed = 7.25f;
        saved.version = 11;
        string versionEleven = WithoutObject(saved.ToJson(), "slope");
        Assert.That(versionEleven, Does.Not.Contain("\"slope\""));
        Assert.That(versionEleven, Does.Contain("\"roll\""), "only the new settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionEleven);
        SlopeSettings defaults = new SlopeSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(11));
        Assert.That(loaded.version, Is.EqualTo(11), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(15.5f));
        Assert.That(loaded.roll.diveSpeed, Is.EqualTo(7.25f));
        Assert.That(loaded.slope, Is.Not.Null);
        Assert.That(loaded.slope.maxSlopeAngle, Is.EqualTo(defaults.maxSlopeAngle));
        Assert.That(loaded.slope.uphillSpeedFactor, Is.EqualTo(defaults.uphillSpeedFactor));
        Assert.That(loaded.slope.downhillSpeedFactor, Is.EqualTo(defaults.downhillSpeedFactor));
        Assert.That(loaded.slope.strideShortening, Is.EqualTo(defaults.strideShortening));
        Assert.That(loaded.slope.ankleAlignWeight, Is.EqualTo(defaults.ankleAlignWeight));
        Assert.That(loaded.slope.slideSlopeAcceleration, Is.EqualTo(defaults.slideSlopeAcceleration));
    }

    [Test]
    public void TheRollMomentumAndRunExitSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.rollMomentum.inheritShare = 0.75f;
        original.rollMomentum.runningThrow = 5.25f;
        original.rollMomentum.settleBrake = 6.5f;
        original.rollRunExit.enabled = false;
        original.rollRunExit.runExitShare = 0.5f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.rollMomentum.inheritShare, Is.EqualTo(0.75f));
        Assert.That(loaded.rollMomentum.runningThrow, Is.EqualTo(5.25f));
        Assert.That(loaded.rollMomentum.settleBrake, Is.EqualTo(6.5f));
        Assert.That(loaded.rollRunExit.enabled, Is.False);
        Assert.That(loaded.rollRunExit.runExitShare, Is.EqualTo(0.5f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 12, before the roll carried momentum and before the run exit was saved, loads both at
    // their code defaults.
    [Test]
    public void AVersionTwelvePresetLoadsTheRollMomentumAndRunExitDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.run.lean = 14.5f;
        saved.roll.cooldown = 0.45f;
        saved.version = 12;
        string versionTwelve = WithoutObject(WithoutObject(saved.ToJson(), "rollMomentum"), "rollRunExit");
        Assert.That(versionTwelve, Does.Not.Contain("\"rollMomentum\""));
        Assert.That(versionTwelve, Does.Not.Contain("\"rollRunExit\""));
        Assert.That(versionTwelve, Does.Contain("\"roll\""), "only the new settings are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionTwelve);
        RollMomentumSettings momentumDefaults = new RollMomentumSettings();
        RollRunExitSettings runExitDefaults = new RollRunExitSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(12));
        Assert.That(loaded.version, Is.EqualTo(12), "what the file says");
        Assert.That(loaded.run.lean, Is.EqualTo(14.5f));
        Assert.That(loaded.roll.cooldown, Is.EqualTo(0.45f));
        Assert.That(loaded.rollMomentum, Is.Not.Null);
        Assert.That(loaded.rollMomentum.inheritShare, Is.EqualTo(momentumDefaults.inheritShare));
        Assert.That(loaded.rollMomentum.standingThrow, Is.EqualTo(momentumDefaults.standingThrow));
        Assert.That(loaded.rollMomentum.maxEntrySpeed, Is.EqualTo(momentumDefaults.maxEntrySpeed));
        Assert.That(loaded.rollMomentum.minRollSpeed, Is.EqualTo(momentumDefaults.minRollSpeed));
        Assert.That(loaded.rollRunExit, Is.Not.Null);
        Assert.That(loaded.rollRunExit.enabled, Is.True);
        Assert.That(loaded.rollRunExit.runExitShare, Is.EqualTo(runExitDefaults.runExitShare));
        Assert.That(loaded.rollRunExit.maxStandGap, Is.EqualTo(runExitDefaults.maxStandGap));
    }

    [Test]
    public void TheSlopeReachSettingsSurviveAJsonRoundTrip() {
        MovementPreset original = MovementPreset.CodeDefaults();
        original.slope.releaseExtension = 1.0625f;
        original.slope.slopeLean = 0.25f;

        MovementPreset loaded = MovementPreset.FromJson(original.ToJson());
        Assert.That(loaded.slope.releaseExtension, Is.EqualTo(1.0625f));
        Assert.That(loaded.slope.slopeLean, Is.EqualTo(0.25f));
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
    }

    // A preset saved at version 13, before the gait was bounded by the legs' reach on slopes, has slope settings without
    // the release extension and the slope lean: they load at their code defaults, and the legacy values it saved (step
    // shortening, pelvis drop) still load as saved.
    [Test]
    public void AVersionThirteenPresetLoadsTheSlopeReachDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.slope.uphillSpeedFactor = 0.625f;
        saved.slope.strideShortening = 0.375f;
        saved.version = 13;
        string versionThirteen = WithoutField(WithoutField(saved.ToJson(), "releaseExtension"), "slopeLean");
        Assert.That(versionThirteen, Does.Not.Contain("\"releaseExtension\""));
        Assert.That(versionThirteen, Does.Not.Contain("\"slopeLean\""));
        Assert.That(versionThirteen, Does.Contain("\"slope\""), "only the new fields are taken out");

        MovementPreset loaded = MovementPreset.FromJson(versionThirteen);
        SlopeSettings defaults = new SlopeSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(13));
        Assert.That(loaded.version, Is.EqualTo(13), "what the file says");
        Assert.That(loaded.slope.uphillSpeedFactor, Is.EqualTo(0.625f));
        Assert.That(loaded.slope.strideShortening, Is.EqualTo(0.375f), "the legacy value loads as saved");
        Assert.That(loaded.slope.releaseExtension, Is.EqualTo(defaults.releaseExtension));
        Assert.That(loaded.slope.slopeLean, Is.EqualTo(defaults.slopeLean));
    }

    // A preset saved at version 15 placed the holster in the pelvis bone's space; the holster hangs from the front thigh
    // since version 16, so the old offset and angle load at their code defaults, while the rest of the pistol handling it
    // saved (the grip, the draw timings) loads as saved.
    [Test]
    public void AVersionFifteenPresetLoadsTheThighHolsterDefaultsAndKeepsTheRestOfThePistolHandling() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.pistolHandling.holsterOffset = new UnityEngine.Vector2(-0.12f, -0.4f);
        saved.pistolHandling.holsterAngle = -184f;
        saved.pistolHandling.gripOffset = new UnityEngine.Vector2(0.0625f, 0.125f);
        saved.pistolHandling.drawDuration = 0.5f;
        saved.version = 15;

        MovementPreset loaded = MovementPreset.FromJson(saved.ToJson());
        PistolHandlingSettings defaults = new PistolHandlingSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(15));
        Assert.That(loaded.version, Is.EqualTo(15), "what the file says");
        Assert.That(loaded.pistolHandling.holsterOffset, Is.EqualTo(defaults.holsterOffset), "the pelvis-space offset is dropped");
        Assert.That(loaded.pistolHandling.holsterAngle, Is.EqualTo(defaults.holsterAngle));
        Assert.That(loaded.pistolHandling.gripOffset, Is.EqualTo(new UnityEngine.Vector2(0.0625f, 0.125f)));
        Assert.That(loaded.pistolHandling.drawDuration, Is.EqualTo(0.5f));

        // At the current version a tuned holster loads as saved.
        saved.version = MovementPreset.CurrentVersion;
        Assert.That(MovementPreset.FromJson(saved.ToJson()).pistolHandling.holsterAngle, Is.EqualTo(-184f));
    }

    // A preset saved at version 14 has no pistol handling at all: it loads at the code defaults.
    [Test]
    public void AVersionFourteenPresetLoadsThePistolHandlingDefaults() {
        MovementPreset saved = MovementPreset.CodeDefaults();
        saved.version = 14;
        string versionFourteen = WithoutObject(saved.ToJson(), "pistolHandling");
        Assert.That(versionFourteen, Does.Not.Contain("\"pistolHandling\""));

        MovementPreset loaded = MovementPreset.FromJson(versionFourteen);
        PistolHandlingSettings defaults = new PistolHandlingSettings();
        Assert.That(MovementPreset.CurrentVersion, Is.GreaterThan(14));
        Assert.That(loaded.pistolHandling.holsterOffset, Is.EqualTo(defaults.holsterOffset));
        Assert.That(loaded.pistolHandling.drawDuration, Is.EqualTo(defaults.drawDuration));
        Assert.That(loaded.pistolHandling.insertShare, Is.EqualTo(defaults.insertShare));
    }

    [Test]
    public void CopyFromCoversEveryPistolHandlingField() {
        AssertCopyFromCoversEveryField(new PistolHandlingSettings(), (target, source) => target.CopyFrom(source), new string[0]);
    }

    // The JSON with one "name": value line taken out (ToJson writes one field per line).
    private static string WithoutField(string json, string name) {
        string[] lines = json.Split('\n');
        System.Text.StringBuilder kept = new System.Text.StringBuilder();
        foreach (string line in lines) {
            if (!line.TrimStart().StartsWith("\"" + name + "\":", System.StringComparison.Ordinal)) {
                kept.Append(line).Append('\n');
            }
        }
        return kept.ToString();
    }

    [Test]
    public void CopyFromCoversEveryRollMomentumAndRunExitField() {
        AssertCopyFromCoversEveryField(new RollMomentumSettings(), (target, source) => target.CopyFrom(source), new string[0]);
        AssertCopyFromCoversEveryField(new RollRunExitSettings(), (target, source) => target.CopyFrom(source), new string[0]);
    }

    [Test]
    public void CopyFromCoversEverySlopeField() {
        AssertCopyFromCoversEveryField(new SlopeSettings(), (target, source) => target.CopyFrom(source), new string[0]);
    }

    // CopyFrom writes every field of the settings the states hold by reference; a field added later and forgotten
    // there would silently stop following the panel.
    [Test]
    public void CopyFromCoversEveryRollAndClimbField() {
        AssertCopyFromCoversEveryField(new RollSettings(), (target, source) => target.CopyFrom(source), new string[0]);
        AssertCopyFromCoversEveryField(new ClimbSettings(), (target, source) => target.CopyFrom(source), new[] { "ledgeMask" });
    }

    private static void AssertCopyFromCoversEveryField<T>(T template, System.Action<T, T> copyFrom, string[] untouched) where T : new() {
        T source = new T();
        foreach (System.Reflection.FieldInfo field in typeof(T).GetFields()) {
            object different = field.FieldType == typeof(bool) ? !(bool)field.GetValue(template)
                : field.FieldType == typeof(float) ? (object)((float)field.GetValue(template) + 1.5f)
                : field.FieldType == typeof(UnityEngine.Vector2) ? (object)((UnityEngine.Vector2)field.GetValue(template) + new UnityEngine.Vector2(1.5f, 2.5f))
                : field.GetValue(template);
            field.SetValue(source, different);
        }
        T target = new T();
        copyFrom(target, source);
        foreach (System.Reflection.FieldInfo field in typeof(T).GetFields()) {
            bool expectsCopy = System.Array.IndexOf(untouched, field.Name) < 0;
            Assert.That(field.GetValue(target), expectsCopy ? Is.EqualTo(field.GetValue(source)) : Is.EqualTo(field.GetValue(template)), $"{typeof(T).Name}.{field.Name}");
        }
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
        copy.push.pushSpeed = 99f;
        copy.roll.diveSpeed = 99f;
        copy.rollMomentum.inheritShare = 99f;
        copy.rollRunExit.runExitShare = 99f;
        copy.climb.pullDuration = 99f;
        copy.slope.maxSlopeAngle = 99f;
        Assert.That(original.walk.speed, Is.EqualTo(GaitProfile.Walk.speed));
        Assert.That(original.ledgeHang.regrabLockout, Is.EqualTo(new LedgeHangSettings().regrabLockout));
        Assert.That(original.oneWay.dropThroughTime, Is.EqualTo(new OneWaySettings().dropThroughTime));
        Assert.That(original.push.pushSpeed, Is.EqualTo(new PushSettings().pushSpeed));
        Assert.That(original.roll.diveSpeed, Is.EqualTo(new RollSettings().diveSpeed));
        Assert.That(original.rollMomentum.inheritShare, Is.EqualTo(new RollMomentumSettings().inheritShare));
        Assert.That(original.rollRunExit.runExitShare, Is.EqualTo(new RollRunExitSettings().runExitShare));
        Assert.That(original.climb.pullDuration, Is.EqualTo(new ClimbSettings().pullDuration));
        Assert.That(original.slope.maxSlopeAngle, Is.EqualTo(new SlopeSettings().maxSlopeAngle));
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
