using NUnit.Framework;
using UnityEngine;

public class JumpMomentumTests {
    private const float RunSpeed = 9f;
    private const float WalkSpeed = 3.5f;
    private static readonly JumpSettings Jump = new JumpSettings();

    private static float Launch(float groundSpeed) => JumpMomentum.LaunchSpeedX((groundSpeed, RunSpeed), Jump);

    [Test]
    public void AStandingJumpGoesStraightUp() {
        Assert.That(Launch(0f), Is.EqualTo(0f));
    }

    [Test]
    public void AJumpKeepsTheGroundSpeedAndAddsForwardImpulseAsTheSpeedGrows() {
        Assert.That(Launch(WalkSpeed), Is.GreaterThan(WalkSpeed), "a little more than the walk");
        Assert.That(Launch(WalkSpeed), Is.LessThan(WalkSpeed + Jump.forwardImpulse * 0.6f), "but mostly the walk speed");
        Assert.That(Launch(RunSpeed), Is.EqualTo(RunSpeed + Jump.forwardImpulse).Within(0.0001f), "a full run gets the whole impulse");
    }

    [Test]
    public void ARunningJumpTravelsFurtherThanAWalkingOneWhichTravelsFurtherThanAStandingOne() {
        Assert.That(Launch(RunSpeed), Is.GreaterThan(Launch(WalkSpeed) * 2f));
        Assert.That(Launch(WalkSpeed), Is.GreaterThan(Launch(0f)));
    }

    [Test]
    public void TheLaunchFollowsTheDirectionOfTheMotion() {
        Assert.That(Launch(-RunSpeed), Is.EqualTo(-Launch(RunSpeed)).Within(0.0001f));
        Assert.That(Launch(-WalkSpeed), Is.LessThan(0f));
    }

    [Test]
    public void TheLaunchNeverPassesTheRunSpeedPlusTheImpulse() {
        Assert.That(Launch(50f), Is.EqualTo(RunSpeed + Jump.forwardImpulse).Within(0.0001f));
        Assert.That(Launch(-50f), Is.EqualTo(-(RunSpeed + Jump.forwardImpulse)).Within(0.0001f));
    }

    [Test]
    public void TheLaunchRisesSmoothlyWithTheGroundSpeed() {
        float previous = Launch(0f);
        for (float speed = 0.05f; speed <= RunSpeed; speed += 0.05f) {
            float launch = Launch(speed);
            Assert.That(launch, Is.GreaterThanOrEqualTo(previous), $"monotone at {speed}");
            Assert.That(launch - previous, Is.LessThan(0.2f), $"no jump at {speed}");
            previous = launch;
        }
    }

    [Test]
    public void ACarryOfZeroDropsTheGroundSpeed() {
        JumpSettings none = new JumpSettings { carry = 0f, forwardImpulse = 0f };
        Assert.That(JumpMomentum.LaunchSpeedX((WalkSpeed, RunSpeed), none), Is.EqualTo(0f));
    }

    [Test]
    public void ACrouchJumpRisesALittleLessThanAStandingOne() {
        Assert.That(JumpMomentum.FullHeight(Jump, 0f), Is.EqualTo(Jump.jumpHeight));
        Assert.That(JumpMomentum.FullHeight(Jump, 1f), Is.EqualTo(Jump.jumpHeight * Jump.crouchJumpScale).Within(0.0001f));
        Assert.That(JumpMomentum.FullHeight(Jump, 0.5f), Is.InRange(Jump.jumpHeight * Jump.crouchJumpScale, Jump.jumpHeight));
    }

    [Test]
    public void TheWindupShrinksAsTheCharacterSpeedsUpAndIsGoneAtARun() {
        float standing = JumpMomentum.WindupDuration((0f, 0f), Jump);
        float walking = JumpMomentum.WindupDuration((WalkSpeed / RunSpeed, 0f), Jump);
        float running = JumpMomentum.WindupDuration((1f, 0f), Jump);

        Assert.That(standing, Is.EqualTo(Jump.windupDuration).Within(0.0001f));
        Assert.That(walking, Is.LessThan(standing));
        Assert.That(walking, Is.GreaterThan(running));
        Assert.That(running, Is.EqualTo(Jump.minWindup).Within(0.0001f), "a run jumps off its stride");
    }

    [Test]
    public void TheWindupDepthFadesTheSameWayAndDisappearsFromACrouch() {
        Assert.That(JumpMomentum.WindupDepth((0f, 0f), Jump), Is.EqualTo(Jump.crouchDepth).Within(0.0001f));
        Assert.That(JumpMomentum.WindupDepth((1f, 0f), Jump), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(JumpMomentum.WindupDepth((0f, 1f), Jump), Is.EqualTo(0f).Within(0.0001f), "already low");
    }

    [Test]
    public void TheWindupScaleIsMonotone() {
        float previous = 1f;
        for (float share = 0f; share <= 1f; share += 0.02f) {
            float scale = JumpMomentum.WindupScale(share, Jump);
            Assert.That(scale, Is.LessThanOrEqualTo(previous + 0.0001f));
            previous = scale;
        }
    }

    [Test]
    public void TheSpeedShareIsClampedAndIgnoresDirection() {
        Assert.That(JumpMomentum.SpeedShare(-RunSpeed / 2f, RunSpeed), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(JumpMomentum.SpeedShare(100f, RunSpeed), Is.EqualTo(1f));
    }
}
