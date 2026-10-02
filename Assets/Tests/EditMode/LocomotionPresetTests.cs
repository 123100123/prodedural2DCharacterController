using NUnit.Framework;

public class LocomotionPresetTests {
    private static LocomotionPreset Sample() {
        LocomotionPreset preset = new LocomotionPreset {
            walk = GaitProfile.Walk,
            run = GaitProfile.Run,
            transitions = GaitTransitionSettings.Default,
            balance = BalanceSettings.Default,
            idle = IdleStanceSettings.Default,
            bodyHeight = 2.58f,
            body = BodyMassSettings.Default,
            posture = PostureSettings.Default,
            chestSpring = new SpringSettings(3f, 0.8f, 0f),
            breathing = BreathingSettings.Default,
            head = HeadMotionSettings.Default,
            arms = ArmSwingSettings.Default,
        };
        preset.run.lean = 17.5f;
        preset.body.verticalSpring.frequency = 7.25f;
        preset.head.stabilization = 0.35f;
        return preset;
    }

    [Test]
    public void SurvivesAJsonRoundTrip() {
        LocomotionPreset original = Sample();
        LocomotionPreset loaded = LocomotionPreset.FromJson(original.ToJson());
        Assert.That(loaded.ToJson(), Is.EqualTo(original.ToJson()));
        Assert.That(loaded.run.lean, Is.EqualTo(17.5f));
        Assert.That(loaded.body.verticalSpring.frequency, Is.EqualTo(7.25f));
        Assert.That(loaded.head.stabilization, Is.EqualTo(0.35f));
    }

    [Test]
    public void ACopyIsIndependent() {
        LocomotionPreset original = Sample();
        LocomotionPreset copy = original.Copy();
        copy.walk.speed = 99f;
        copy.posture.maxTotalLean = 1f;
        Assert.That(original.walk.speed, Is.EqualTo(GaitProfile.Walk.speed));
        Assert.That(original.posture.maxTotalLean, Is.EqualTo(PostureSettings.Default.maxTotalLean));
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
