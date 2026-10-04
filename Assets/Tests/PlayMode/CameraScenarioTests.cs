using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The follow camera on the real character: aiming leans it toward the pointer and releasing eases it back, and shots
// and hard landings shake it for a moment (unless the shake is switched off).
public class CameraScenarioTests : ScenarioFixture {
    private const int FramesBeforeFirstClick = 30;
    private const int FramesBetweenShots = 14;

    private FollowCamera FollowCamera => Stage.Character.Parts.FollowCamera;
    private CameraFeelSettings Feel => Stage.Character.Parts.CameraFeel;

    // The screen position to hold the pointer at. The editor remaps the virtual mouse's position, so the position the
    // game reads is measured after a few frames of aiming rather than assumed, then held while the camera moves.
    private IEnumerator FindPointerScreenPosition(Vector2 worldOffset, Action<Vector2> found) {
        Vector2 request = (Vector2)Stage.Stride.transform.position + worldOffset;
        // Held for longer than a tap (which would latch the aim on) and left held for the caller to carry on aiming.
        for (int frame = 0; frame < 20; frame++) {
            Stage.AimAndFire(request, false);
            yield return null;
        }
        found(Stage.Character.LatestCommands.PointerScreenPosition);
    }

    private IEnumerator AimFor(Vector2 screen, int frames) {
        for (int frame = 0; frame < frames; frame++) {
            Stage.AimAtScreen(screen, false);
            yield return null;
        }
    }

    // Aims at a screen position and clicks once every few frames; returns the largest trauma and shake offset seen.
    private IEnumerator AimAndClick(Vector2 screen, Action<(float trauma, float offset)> peaks) {
        float peakTrauma = 0f;
        float peakOffset = 0f;
        for (int frame = 0; frame < FramesBeforeFirstClick + 2 * FramesBetweenShots; frame++) {
            bool isClicking = frame >= FramesBeforeFirstClick && (frame - FramesBeforeFirstClick) % FramesBetweenShots < 3;
            Stage.AimAtScreen(screen, isClicking);
            yield return null;
            peakTrauma = Mathf.Max(peakTrauma, FollowCamera.Shake.Trauma);
            peakOffset = Mathf.Max(peakOffset, FollowCamera.Shake.Offset.magnitude);
        }
        Stage.StopAiming();
        peaks((peakTrauma, peakOffset));
    }

    [UnityTest]
    public IEnumerator AimingLeansTheCameraTowardThePointerAndReleasingEasesItBack() {
        yield return Hold(1.5f);
        Assert.That(FollowCamera.AimOffset, Is.EqualTo(Vector2.zero), "not aiming, no lean");
        float restingX = Camera.main.transform.position.x;
        float restingY = Camera.main.transform.position.y;
        Vector2 screen = Vector2.zero;
        yield return FindPointerScreenPosition(new Vector2(3f, 1.5f), position => screen = position);

        yield return AimFor(screen, 120);
        float aimingX = Camera.main.transform.position.x;
        Vector2 lean = FollowCamera.AimOffset;
        Report($"pointer at screen {screen}, camera x {restingX:F2} -> {aimingX:F2}, y {restingY:F2} -> {Camera.main.transform.position.y:F2}, lean {lean}");
        Assert.That(aimingX, Is.GreaterThan(restingX + 0.3f), "the camera moved toward the pointer side");
        Assert.That(lean.x, Is.GreaterThan(0.3f).And.LessThanOrEqualTo(Feel.maxAimOffset.x + 0.001f));
        Assert.That(Mathf.Abs(lean.y), Is.LessThanOrEqualTo(Feel.maxAimOffset.y + 0.001f));

        Stage.StopAiming();
        yield return Hold(3f);
        Assert.That(FollowCamera.AimOffset.magnitude, Is.LessThan(0.05f), "the lean has eased out");
        Assert.That(Camera.main.transform.position.x, Is.EqualTo(restingX).Within(0.2f), "the camera is back with the character");
    }

    [UnityTest]
    public IEnumerator AShotShakesTheCameraAndTheShakeDecaysBackToNothing() {
        yield return Hold(1f);
        Vector2 screen = Vector2.zero;
        yield return FindPointerScreenPosition(new Vector2(3f, 1.5f), position => screen = position);
        Assert.That(FollowCamera.Shake.Offset, Is.EqualTo(Vector2.zero), "no shake before the first shot");

        (float trauma, float offset) peaks = default;
        yield return AimAndClick(screen, result => peaks = result);
        Report($"peak trauma {peaks.trauma:F2}, peak shake offset {peaks.offset:F4}");
        Assert.That(peaks.trauma, Is.GreaterThanOrEqualTo(Feel.firedTrauma - 0.01f), "a shot adds trauma");
        Assert.That(peaks.offset, Is.GreaterThan(0.002f), "and moves the camera");

        yield return Hold(1.5f);
        Assert.That(FollowCamera.Shake.Trauma, Is.EqualTo(0f), "the trauma drained");
        Assert.That(FollowCamera.Shake.Offset, Is.EqualTo(Vector2.zero));
        float roll = Camera.main.transform.eulerAngles.z;
        Assert.That(Mathf.DeltaAngle(roll, 0f), Is.EqualTo(0f).Within(0.001f), "the roll is gone");
    }

    [UnityTest]
    public IEnumerator ShotsDoNotShakeTheCameraWhenTheShakeIsSwitchedOff() {
        Feel.shakeEnabled = false;
        int shots = 0;
        Stage.Character.Events.Fired += _ => shots++;
        yield return Hold(1f);
        Vector2 screen = Vector2.zero;
        yield return FindPointerScreenPosition(new Vector2(3f, 1.5f), position => screen = position);

        (float trauma, float offset) peaks = (1f, 1f);
        yield return AimAndClick(screen, result => peaks = result);
        Assert.That(shots, Is.GreaterThan(0), "shots were fired");
        Assert.That(peaks.trauma, Is.EqualTo(0f));
        Assert.That(peaks.offset, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator OnlyAHardLandingShakesTheCamera() {
        yield return Hold(1f);
        Stage.Character.Events.RaiseLanded((Vector2.zero, Feel.landingTraumaThreshold - 1f));
        Assert.That(FollowCamera.Shake.Trauma, Is.EqualTo(0f), "a soft landing shakes nothing");

        Stage.Character.Events.RaiseLanded((Vector2.zero, 18f));
        Assert.That(FollowCamera.Shake.Trauma, Is.EqualTo(CameraShake.LandingTrauma(18f, Feel)).Within(0.001f));
        Assert.That(FollowCamera.Shake.Trauma, Is.GreaterThan(0.2f), "a hard one does");
        yield return Hold(1.5f);
        Assert.That(FollowCamera.Shake.Trauma, Is.EqualTo(0f));
    }
}
