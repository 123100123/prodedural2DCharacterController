using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Walking and running over hills of 15°, 30° and 45° (up-ramp, flat top, down-ramp, each ramp 10 units across): the gait
// is bounded by the legs' reach on the slope, so a planted trailing leg never stretches past the release extension, planted
// feet stay on the surface, the steady part of a ramp needs no balance step, and no planted foot moves (snaps) through the
// flat-to-slope and slope-to-flat changes. Writes sheets of eight frames from the middle of each ramp of the runs
// (Temp/LocomotionScenarios/slope-run-up-<angle>.png, slope-run-down-<angle>.png).
public class SlopeGaitScenarioTests : ScenarioFixture {
    private const float RampRun = 10f;
    private const float TopWidth = 5f;
    // The steady part of a ramp: the body this far in from both of its ends.
    private const float SteadyMargin = 2f;
    // Feet are measured on a ramp this far in from its ends (the corners are neither slope nor flat).
    private const float FootMargin = 0.3f;
    private const float FootOnSurfaceTolerance = 0.05f;
    // How far past the release extension a planted trailing leg may be seen at the end of a frame.
    private const float ExtensionSlack = 0.02f;
    // A planted foot's target never moves; this is float noise.
    private const float PlantedFootStill = 1e-4f;
    // The foot bone reaches its target this many frames after it plants (the IK catches the landing up), as SlopeScenarioTests measures it.
    private const int SettledFootFrames = 3;
    // The pictures: eight frames, two apart, from the middle of each ramp of a run, in a 4 x 2 sheet.
    private const int PictureWidth = 480;
    private const int PictureHeight = 360;
    private const int SheetFrames = 8;
    private const int SheetFrameSpacing = 2;

    private Vector2 Body => Stage.Stride.transform.position;
    private SlopeSettings Slope => Stage.Character.Parts.Slope;

    // What one ramp (up or down) measured.
    private sealed class Ramp {
        public float WorstTrailingExtension;
        public float WorstSteadyTrailingExtension;
        public float WorstSteadyTrailingLag;
        public float SteadyMaxLag = float.NaN;
        public float WorstFootOffSurface;
        public float WorstFootReach;
        public string WorstReachAt = "";
        public float WorstLeadingExtension;
        public string WorstLeadAt = "";
        public string WorstTrailAt = "";
        public int SteadyBalanceSteps;
        public int SteadyFrames;
        public int PlantedRampFeet;

        public string Describe(string label) {
            return $"{label}: trailing extension worst {WorstTrailingExtension:F3} ({WorstTrailAt}), steady {WorstSteadyTrailingExtension:F3} " +
                   $"(lag {WorstSteadyTrailingLag:F3}, MaxLag {SteadyMaxLag:F3}); feet off the surface {WorstFootOffSurface:F3}, " +
                   $"foot reach {WorstFootReach:F3} ({WorstReachAt}) over {PlantedRampFeet} planted ramp frames; leading extension worst {WorstLeadingExtension:F3} ({WorstLeadAt}); " +
                   $"steady balance steps {SteadyBalanceSteps} over {SteadyFrames} frames";
        }
    }

    [UnityTest]
    public IEnumerator TheGaitStaysWithinReachOverAHill([Values(15f, 30f, 45f)] float angle, [Values(false, true)] bool running) {
        float approach = running ? 9f : 5f;
        Hill hill = Stage.AddHill((Body.x + approach, RampRun * Mathf.Tan(angle * Mathf.Deg2Rad), angle, TopWidth));
        (Ramp up, Ramp down) ramps = (new Ramp(), new Ramp());
        float worstPlantedJump = 0f;
        (Vector2 front, Vector2 back, bool frontPlanted, bool backPlanted) previous = (default, default, false, false);
        (int balanceSteps, int resyncs, int releases) start = Counters();
        int lastBalanceSteps = start.balanceSteps;
        string label = $"{angle:F0}° {(running ? "run" : "walk")}";
        (Texture2D up, Texture2D down) sheets = running ? (NewSheet(), NewSheet()) : (null, null);
        (int up, int down) sheetFrames = (0, 0);
        (int front, int back) plantedFrames = (0, 0);

        Stage.Press(running ? new[] { Key.D, Key.LeftShift } : new[] { Key.D });
        float goalX = hill.EndX + 3f;
        for (float elapsed = 0f; elapsed < 30f && Body.x < goalX; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            // The feet are measured after the IK has solved this frame.
            yield return new WaitForEndOfFrame();
            StrideController stride = Stage.Stride;
            bool walking = Stage.Character.CurrentState is WalkState;
            int balanceSteps = stride.Diagnostics.BalanceSteps;
            plantedFrames = (stride.FrontLeg.IsGrounded ? plantedFrames.front + 1 : 0, stride.BackLeg.IsGrounded ? plantedFrames.back + 1 : 0);
            if (walking) {
                worstPlantedJump = Mathf.Max(worstPlantedJump, PlantedJump((previous.front, previous.frontPlanted), stride.FrontLeg));
                worstPlantedJump = Mathf.Max(worstPlantedJump, PlantedJump((previous.back, previous.backPlanted), stride.BackLeg));
                Ramp ramp = Body.x < (hill.TopStartX + hill.TopEndX) / 2f ? ramps.up : ramps.down;
                bool steady = hill.IsMidRamp(Body.x, SteadyMargin);
                MeasureFoot((hill, ramp, steady), (stride.FrontLeg, plantedFrames.front));
                MeasureFoot((hill, ramp, steady), (stride.BackLeg, plantedFrames.back));
                if (steady) {
                    ramp.SteadyFrames++;
                    ramp.SteadyBalanceSteps += balanceSteps - lastBalanceSteps;
                    ramp.SteadyMaxLag = stride.Stance.MaxLag;
                }
            }
            lastBalanceSteps = balanceSteps;
            previous = (stride.FrontLeg.FootTarget, stride.BackLeg.FootTarget, stride.FrontLeg.IsGrounded, stride.BackLeg.IsGrounded);

            if (sheets.up != null && Body.x >= (hill.FootX + hill.TopStartX) / 2f) {
                sheetFrames.up = Photograph(sheets.up, sheetFrames.up);
            }
            if (sheets.down != null && Body.x >= (hill.TopEndX + hill.EndX) / 2f) {
                sheetFrames.down = Photograph(sheets.down, sheetFrames.down);
            }
        }
        Stage.ReleaseAll();
        if (running) {
            WriteSheet($"slope-run-up-{angle:F0}", sheets.up);
            WriteSheet($"slope-run-down-{angle:F0}", sheets.down);
        }
        (int balanceSteps, int resyncs, int releases) end = Counters();
        Report(ramps.up.Describe($"{label} up"));
        Report(ramps.down.Describe($"{label} down"));
        Report($"{label}: worst planted-foot jump {worstPlantedJump:F5}; whole traverse balance steps {end.balanceSteps - start.balanceSteps}, " +
               $"resyncs {end.resyncs - start.resyncs}, extension releases {end.releases - start.releases}");

        Assert.That(Body.x, Is.GreaterThanOrEqualTo(goalX), "crossed the hill");
        float limit = Slope.releaseExtension + ExtensionSlack;
        foreach ((Ramp ramp, string side) measured in new[] { (ramps.up, "uphill"), (ramps.down, "downhill") }) {
            Assert.That(measured.ramp.PlantedRampFeet, Is.GreaterThan(5), $"{measured.side}: feet stood on the ramp");
            Assert.That(measured.ramp.SteadyFrames, Is.GreaterThan(10), $"{measured.side}: the steady part was measured");
            Assert.That(measured.ramp.WorstTrailingExtension, Is.LessThanOrEqualTo(limit), $"{measured.side}: a planted trailing leg stays within the release extension");
            Assert.That(measured.ramp.WorstFootOffSurface, Is.LessThanOrEqualTo(FootOnSurfaceTolerance), $"{measured.side}: planted feet stand on the slope");
            Assert.That(measured.ramp.WorstFootReach, Is.LessThanOrEqualTo(PlantedFootTolerance), $"{measured.side}: the legs reach their planted feet");
            Assert.That(measured.ramp.SteadyBalanceSteps, Is.Zero, $"{measured.side}: no balance steps on the steady ramp");
        }
        Assert.That(worstPlantedJump, Is.LessThanOrEqualTo(PlantedFootStill), "no planted foot moves, onto, along or off the ramps");
    }

    private (int balanceSteps, int resyncs, int releases) Counters() {
        GaitDiagnostics diagnostics = Stage.Stride.Diagnostics;
        return (diagnostics.BalanceSteps, diagnostics.PhaseResyncs, diagnostics.ExtensionReleases);
    }

    // How far a foot planted in both frames moved since the last one.
    private static float PlantedJump((Vector2 foot, bool planted) previous, LegStepper leg) {
        return previous.planted && leg.IsGrounded ? Vector2.Distance(previous.foot, leg.FootTarget) : 0f;
    }

    private void MeasureFoot((Hill hill, Ramp ramp, bool steady) at, (LegStepper leg, int plantedFrames) foot) {
        LegStepper leg = foot.leg;
        if (!leg.IsGrounded) {
            return;
        }
        Vector2 target = leg.FootTarget;
        float lag = leg.HipPosition.x - target.x;
        float extension = Vector2.Distance(leg.HipPosition, target) / leg.LegLength;
        bool onRamp = at.hill.GradientAt(Body.x) != 0f;
        if (lag <= 0f && onRamp && extension > at.ramp.WorstLeadingExtension) {
            at.ramp.WorstLeadingExtension = extension;
            StrideController s = Stage.Stride;
            at.ramp.WorstLeadAt = $"foot {target - leg.HipPosition} from the hip, planted {foot.plantedFrames}, hip {at.hill.HeightAbove(leg.HipPosition):F2} above its ground, " +
                                  $"heights {s.BodyHeights.baseline - at.hill.SurfaceY(Body.x):F2}/{s.BodyHeights.gait - at.hill.SurfaceY(Body.x):F2}/{s.BodyHeights.ceiling - at.hill.SurfaceY(Body.x):F2} pelvis {at.hill.HeightAbove(Body):F2}, drop {s.Stance.Drop:F2} lead {s.Stance.MaxLead:F2} step {s.Stance.Step:F2}";
        }
        if (lag > 0f && onRamp && extension > at.ramp.WorstTrailingExtension) {
            StrideController s = Stage.Stride;
            float ground = at.hill.SurfaceY(Body.x);
            at.ramp.WorstTrailAt = $"lag {lag:F3}, foot {at.hill.HeightAbove(leg.HipPosition) - at.hill.HeightAbove(target):F2} below the hip, hip {at.hill.HeightAbove(leg.HipPosition):F2} above its ground, " +
                                   $"pelvis {Body.y - ground:F2} (heights {s.BodyHeights.baseline - ground:F2}/{s.BodyHeights.gait - ground:F2}/{s.BodyHeights.ceiling - ground:F2}, sink {s.SlopeReadout.CeilingSink:F2}), planted {foot.plantedFrames}, pelvis-hip x {leg.HipPosition.x - Body.x:F2}";
        }
        if (lag > 0f) {
            at.ramp.WorstTrailingExtension = onRamp ? Mathf.Max(at.ramp.WorstTrailingExtension, extension) : at.ramp.WorstTrailingExtension;
            if (at.steady) {
                at.ramp.WorstSteadyTrailingExtension = Mathf.Max(at.ramp.WorstSteadyTrailingExtension, extension);
                at.ramp.WorstSteadyTrailingLag = Mathf.Max(at.ramp.WorstSteadyTrailingLag, lag);
            }
        }
        if (!at.hill.IsMidRamp(target.x, FootMargin)) {
            return;
        }
        at.ramp.PlantedRampFeet++;
        at.ramp.WorstFootOffSurface = Mathf.Max(at.ramp.WorstFootOffSurface, Mathf.Abs(at.hill.NormalDistance(target)));
        float reach = foot.plantedFrames < SettledFootFrames ? 0f : Vector2.Distance(leg.FootBone.position, target);
        if (reach > at.ramp.WorstFootReach) {
            at.ramp.WorstFootReach = reach;
            at.ramp.WorstReachAt = $"foot {target - leg.HipPosition} from the hip, extension {extension:F3}, planted {foot.plantedFrames} frames, drop {Stage.Stride.Stance.Drop:F2}";
        }
    }

    private static Texture2D NewSheet() => new Texture2D(4 * PictureWidth, 2 * PictureHeight, TextureFormat.RGB24, false);

    // Pastes this frame into the sheet when it is one of the pictured frames; returns the frames seen so far.
    private int Photograph(Texture2D sheet, int framesSeen) {
        int slot = framesSeen / SheetFrameSpacing;
        if (framesSeen % SheetFrameSpacing == 0 && slot < SheetFrames) {
            Texture2D picture = Capture(Body + new Vector2(0f, -0.45f), 2.9f);
            sheet.SetPixels(slot % 4 * PictureWidth, (1 - slot / 4) * PictureHeight, PictureWidth, PictureHeight, picture.GetPixels());
            Object.Destroy(picture);
        }
        return framesSeen + 1;
    }

    private static void WriteSheet(string name, Texture2D sheet) {
        sheet.Apply();
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), sheet.EncodeToPNG());
        Object.Destroy(sheet);
    }

    private static Texture2D Capture(Vector2 center, float orthographicSize) {
        GameObject host = new GameObject("pictureCamera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = new Vector3(center.x, center.y, -10f);
        camera.orthographicSize = orthographicSize;
        camera.aspect = (float)PictureWidth / PictureHeight;
        RenderTexture target = new RenderTexture(PictureWidth, PictureHeight, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(PictureWidth, PictureHeight, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, PictureWidth, PictureHeight), 0, 0);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(host);
        return picture;
    }
}
