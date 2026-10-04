using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class JumpHeightTests {
    private const float Gravity = 30f;
    private static readonly JumpSettings Jump = new JumpSettings();

    private static float Held(float holdTime) => JumpMath.HeldJumpHeight(holdTime, (Jump.minHopHeight, Jump.jumpHeight, Jump.fullHoldTime));

    [Test]
    public void ATapRisesTheHopHeightAndAFullHoldTheWholeJump() {
        Assert.That(Held(0f), Is.EqualTo(Jump.minHopHeight).Within(0.0001f));
        Assert.That(Held(Jump.fullHoldTime), Is.EqualTo(Jump.jumpHeight).Within(0.0001f));
        Assert.That(Held(Jump.fullHoldTime * 5f), Is.EqualTo(Jump.jumpHeight).Within(0.0001f));
    }

    [Test]
    public void TheHeldHeightGrowsSmoothlyWithTheHold() {
        float previous = Held(0f);
        for (float hold = 0.005f; hold <= Jump.fullHoldTime; hold += 0.005f) {
            float height = Held(hold);
            Assert.That(height, Is.GreaterThanOrEqualTo(previous));
            previous = height;
        }
    }

    [Test]
    public void AHopNeverRisesAboveTheFullHeight() {
        Assert.That(JumpMath.HeldJumpHeight(0f, (5f, 2f, 0.2f)), Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void ReleasingCutsTheRiseToTheSpeedThatReachesTheHeldHeight() {
        float fullSpeed = JumpMath.LaunchSpeedForHeight(Gravity, Jump.jumpHeight);
        float cut = JumpMath.CutRise(fullSpeed, Gravity, Jump.minHopHeight);
        Assert.That(cut, Is.LessThan(fullSpeed));
        Assert.That(cut * cut / (2f * Gravity), Is.EqualTo(Jump.minHopHeight).Within(0.0001f), "rises the hop height");
    }

    [Test]
    public void ReleasingLateOrFallingNeverSpeedsTheCharacterUp() {
        float slow = JumpMath.LaunchSpeedForHeight(Gravity, 0.1f);
        Assert.That(JumpMath.CutRise(slow, Gravity, Jump.minHopHeight), Is.EqualTo(slow));
        Assert.That(JumpMath.CutRise(-5f, Gravity, Jump.minHopHeight), Is.EqualTo(-5f));
    }

    // CopyFrom lists every field by hand, so a field added to a settings class and forgotten there would be silently
    // dropped by the tuning panel.
    [Test]
    public void CopyFromCopiesEveryFieldOfTheSettingsClasses() {
        AssertCopiesEveryField(new JumpSettings(), new JumpSettings(), (target, source) => target.CopyFrom(source));
        AssertCopiesEveryField(new AirborneSettings(), new AirborneSettings(), (target, source) => target.CopyFrom(source));
    }

    private static void AssertCopiesEveryField<T>(T target, T source, System.Action<T, T> copy) {
        foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            field.SetValue(source, Distinct(field.FieldType, field.GetValue(target)));
        }
        copy(target, source);
        foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            Assert.That(field.GetValue(target), Is.EqualTo(field.GetValue(source)), $"{typeof(T).Name}.{field.Name} is copied");
        }
    }

    private static object Distinct(System.Type type, object current) {
        if (type == typeof(float)) {
            return (float)current + 1.2345f;
        }
        if (type == typeof(Vector2)) {
            return (Vector2)current + new Vector2(0.5f, 0.25f);
        }
        Assert.Fail($"the test does not know how to change a {type.Name}");
        return current;
    }

    [Test]
    public void ACloneIsIndependent() {
        JumpSettings original = new JumpSettings();
        JumpSettings clone = original.Clone();
        clone.jumpHeight = 9f;
        Assert.That(original.jumpHeight, Is.EqualTo(new JumpSettings().jumpHeight));
    }
}
