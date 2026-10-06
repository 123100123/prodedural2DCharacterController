using NUnit.Framework;
using UnityEngine;

// The pure push rules: the force that drives a crate (light crates are matched, heavy ones get the push's whole strength
// and no more, nothing ever pulls), the character's speed while pushing, the contact hysteresis and the pose blend.
public class PushTests {
    private const float FixedStep = 0.02f;

    private static PushSettings Settings() => new PushSettings();

    [Test]
    public void ALightCrateGetsJustTheForceThatMatchesTheTargetSpeed() {
        PushSettings settings = Settings();
        float force = PushRules.CrateForce((2f, 0f, 0.3f), (settings.maxForce, FixedStep));
        Assert.That(force, Is.EqualTo(2f * 0.3f / FixedStep).Within(0.001f), "mass x speed change / step");
        Assert.That(force, Is.LessThan(settings.maxForce));
    }

    [Test]
    public void AHeavyCrateGetsThePushsWholeStrengthAndNoMore() {
        PushSettings settings = Settings();
        float force = PushRules.CrateForce((1000f, 0f, 0.3f), (settings.maxForce, FixedStep));
        Assert.That(force, Is.EqualTo(settings.maxForce));
        Assert.That(force / 1000f, Is.LessThan(0.3f), "so its speed changes far less than a light crate's");
    }

    [Test]
    public void APushNeverPullsOrBrakes() {
        Assert.That(PushRules.CrateForce((5f, 3f, 2.2f), (200f, FixedStep)), Is.Zero, "a crate faster than the target");
        Assert.That(PushRules.CrateForce((5f, 2.2f, 2.2f), (200f, FixedStep)), Is.Zero, "at the target");
        Assert.That(PushRules.CrateForce((5f, 0f, 2.2f), (0f, FixedStep)), Is.Zero, "no strength");
        Assert.That(PushRules.CrateForce((5f, 0f, 2.2f), (200f, 0f)), Is.Zero, "no step");
    }

    [Test]
    public void TheCrateIsDrivenNoMoreThanTheSlackAheadOfTheCharacter() {
        PushSettings settings = Settings();
        Assert.That(PushRules.CrateTargetSpeed(0f, settings), Is.EqualTo(settings.speedSlack), "from a stand");
        Assert.That(PushRules.CrateTargetSpeed(1f, settings), Is.EqualTo(1f + settings.speedSlack));
        Assert.That(PushRules.CrateTargetSpeed(9f, settings), Is.EqualTo(settings.pushSpeed), "never past the push speed");
    }

    [Test]
    public void ACrateThatDoesNotMoveHoldsTheCharacterAtTheSkin() {
        PushSettings settings = Settings();
        Assert.That(PushRules.CharacterSpeed((0f, 0f), settings), Is.Zero, "pressed against a heavy crate: standing");
        Assert.That(PushRules.CharacterSpeed((0f, settings.contactSkin), settings), Is.Zero, "at the skin: standing");
        Assert.That(PushRules.CharacterSpeed((0f, settings.contactSkin * 1.5f), settings), Is.EqualTo(settings.speedSlack * 0.5f).Within(0.0001f), "closing in");
    }

    [Test]
    public void TheCharacterFollowsAMovingCrateAndNeverPassesThePushSpeed() {
        PushSettings settings = Settings();
        float skin = settings.contactSkin;
        Assert.That(PushRules.CharacterSpeed((1.5f, skin), settings), Is.EqualTo(1.5f).Within(0.0001f), "the crate's speed at the skin");
        Assert.That(PushRules.CharacterSpeed((1.5f, 5f * skin), settings), Is.EqualTo(1.5f + settings.speedSlack).Within(0.0001f), "plus the slack with room ahead");
        Assert.That(PushRules.CharacterSpeed((1.5f, 0f), settings), Is.EqualTo(1.5f - settings.speedSlack).Within(0.0001f), "minus the slack too close");
        Assert.That(PushRules.CharacterSpeed((6f, 5f * skin), settings), Is.EqualTo(settings.pushSpeed), "never past the push speed");
        Assert.That(PushRules.CharacterSpeed((-2f, 0f), settings), Is.Zero, "never backward");
    }

    [Test]
    public void APushTakesHoldCloseAndLetsGoOnlyFurtherAway() {
        PushSettings settings = Settings();
        float engage = PushRules.EngageDistance(settings);
        float release = PushRules.ReleaseDistance(settings);
        Assert.That(release, Is.GreaterThan(engage));

        Assert.That(PushRules.Holds(false, (true, engage * 0.5f), settings), Is.True, "takes hold within reach");
        Assert.That(PushRules.Holds(false, (true, (engage + release) / 2f), settings), Is.False, "does not take hold between the two");
        Assert.That(PushRules.Holds(true, (true, (engage + release) / 2f), settings), Is.True, "but holds on there once pushing");
        Assert.That(PushRules.Holds(true, (true, release + 0.01f), settings), Is.False, "lets go beyond the release distance");
        Assert.That(PushRules.Holds(true, (false, 0f), settings), Is.False, "lets go when it cannot push (aiming, not pressing, airborne)");
    }

    // A body swaying with its steps around the hold's distances flickers in and out of contact without the hysteresis;
    // with it, the push takes hold once and stays.
    [Test]
    public void ARoomSwayingAcrossTheEngageDistanceDoesNotFlicker() {
        PushSettings settings = Settings();
        float engage = PushRules.EngageDistance(settings);
        bool pushing = false;
        int changes = 0;
        for (int frame = 0; frame < 120; frame++) {
            float room = engage + 0.1f * Mathf.Sin(frame * 0.7f);
            bool next = PushRules.Holds(pushing, (true, room), settings);
            changes += next != pushing ? 1 : 0;
            pushing = next;
        }
        Assert.That(pushing, Is.True);
        Assert.That(changes, Is.EqualTo(1), "took hold once and never let go");
    }

    [Test]
    public void ReleaseIsAlwaysBeyondEngageWhateverTheSettings() {
        PushSettings settings = new PushSettings { contactSkin = 0.3f, releaseDistance = 0.1f };
        Assert.That(PushRules.ReleaseDistance(settings), Is.GreaterThan(PushRules.EngageDistance(settings)));
    }

    [Test]
    public void ThePoseBlendsInOverAboutTheBlendTimeWithoutOvershoot() {
        PushSettings settings = Settings();
        SecondOrderDynamics blend = new SecondOrderDynamics(0f);
        float dt = 1f / 60f;
        float highest = 0f;
        float atHalf = 0f;
        float atBlendTime = 0f;
        for (float time = dt; time <= 1f; time += dt) {
            float value = blend.Update((1f, dt), PushRules.BlendSpring(settings.blendTime));
            highest = Mathf.Max(highest, value);
            if (atHalf == 0f && time >= settings.blendTime * 0.5f) {
                atHalf = value;
            }
            if (atBlendTime == 0f && time >= settings.blendTime) {
                atBlendTime = value;
            }
        }
        Assert.That(atHalf, Is.InRange(0.2f, 0.85f), "halfway in at half the time, not snapped");
        Assert.That(atBlendTime, Is.GreaterThan(0.85f), "nearly in at the blend time");
        Assert.That(highest, Is.LessThan(1.01f), "no overshoot");
    }

    [Test]
    public void PressingNeedsMoreThanTheDeadZone() {
        Assert.That(PushRules.PressedSide(0.1f), Is.Zero);
        Assert.That(PushRules.PressedSide(0.5f), Is.EqualTo(1));
        Assert.That(PushRules.PressedSide(-1f), Is.EqualTo(-1));
    }

    [Test]
    public void ASlopeAddsTheCratesWeightAlongItAndFlatGroundAddsNothing() {
        Assert.That(PushRules.SlopeLoad((2f, 10f), 0f), Is.Zero, "flat");
        Assert.That(PushRules.SlopeLoad((2f, 10f), 30f), Is.EqualTo(10f).Within(0.01f), "weight 20 times sin 30");
        Assert.That(PushRules.SlopeLoad((2f, 10f), 45f), Is.EqualTo(14.142f).Within(0.01f));
        Assert.That(PushRules.SlopeLoad((0f, 10f), 45f), Is.Zero, "no mass, no load");
    }

    [Test]
    public void AtItsTargetSpeedUpASlopeTheCrateGetsExactlyItsLoadAndNothingMore() {
        float force = PushRules.SlopeForce((2f, 1.5f, 1.5f), (14f, 200f, FixedStep));
        Assert.That(force, Is.EqualTo(14f).Within(1e-4f));
    }

    [Test]
    public void ASlopeCrateRunningAheadOfTheHandsIsHeldBackBelowItsLoadAndNeverPulled() {
        Assert.That(PushRules.SlopeForce((2f, 3f, 1.5f), (14f, 200f, FixedStep)), Is.LessThan(14f), "faster than the target: less than the load");
        Assert.That(PushRules.SlopeForce((2f, 30f, 1.5f), (14f, 200f, FixedStep)), Is.Zero, "far faster: no push at all");
    }

    [Test]
    public void ASlopeForceStaysWithinThePushsStrength() {
        Assert.That(PushRules.SlopeForce((1000f, 0f, 2f), (5000f, 200f, FixedStep)), Is.EqualTo(200f));
        Assert.That(PushRules.SlopeForce((1f, 0f, 2f), (5f, 200f, 0f)), Is.Zero, "no time step, no force");
    }
}
