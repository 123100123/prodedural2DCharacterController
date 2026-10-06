using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

// The pure parts of the hover targets: the pictures drawn in code, the routes, the points rule, the rotor swing and the
// popup rise and fade; and that the target-effects settings travel through presets.
public class HoverTargetTests {
    private static readonly HoverTargetSettings Defaults = new HoverTargetSettings();

    private static HoverTargetSettings Route(HoverPathKind kind, Vector2 extent) {
        return new HoverTargetSettings { path = kind, extent = extent, period = 8f, startShare = 0f };
    }

    // ---- Pictures -------------------------------------------------------------------------------------------

    [Test]
    public void TheBoardIsASolidSquareOfWarmWoodWithADarkerFrame() {
        Texture2D board = HoverTextures.BoardTexture(HoverTextures.BoardPixels);
        try {
            Assert.That(board.width, Is.EqualTo(HoverTextures.BoardPixels));
            Assert.That(board.height, Is.EqualTo(HoverTextures.BoardPixels));
            foreach (Color32 pixel in board.GetPixels32()) {
                Assert.That(pixel.a, Is.EqualTo(255), "no transparent pixel");
            }
            int size = HoverTextures.BoardPixels;
            Color wood = board.GetPixel(size / 2 + 8, size / 2 - 20);
            Color frame = board.GetPixel(1, size / 2);
            Assert.That(wood.r, Is.GreaterThan(wood.b + 0.15f), "warm wood");
            Assert.That(frame.grayscale, Is.LessThan(wood.grayscale), "the frame is darker than the wood");
        }
        finally {
            Object.DestroyImmediate(board);
        }
    }

    [Test]
    public void TheDotsAreDarkerThanTheWoodAroundThem() {
        int size = HoverTextures.BoardPixels;
        Texture2D board = HoverTextures.BoardTexture(size);
        try {
            Vector2[] centers = HoverTextures.DotCenters(size);
            Assert.That(centers.Length, Is.GreaterThanOrEqualTo(7), "a bullseye of dots");
            float woodBrightness = 0f;
            int woodPixels = 0;
            int frame = HoverTextures.FrameWidth(size);
            for (int y = frame; y < size - frame; y++) {
                for (int x = frame; x < size - frame; x++) {
                    bool nearDot = false;
                    for (int dot = 0; dot < centers.Length; dot++) {
                        nearDot |= Vector2.Distance(new Vector2(x, y), centers[dot]) < HoverTextures.DotRadius(dot, size) + 2f;
                    }
                    if (!nearDot) {
                        woodBrightness += board.GetPixel(x, y).grayscale;
                        woodPixels++;
                    }
                }
            }
            woodBrightness /= woodPixels;
            for (int dot = 0; dot < centers.Length; dot++) {
                Color pixel = board.GetPixel(Mathf.RoundToInt(centers[dot].x), Mathf.RoundToInt(centers[dot].y));
                Assert.That(pixel.grayscale, Is.LessThan(woodBrightness * 0.5f), $"dot {dot} is dark against the wood ({woodBrightness:F2})");
                Assert.That(centers[dot].x, Is.GreaterThan(frame).And.LessThan(size - frame), $"dot {dot} sits inside the frame");
                Assert.That(centers[dot].y, Is.GreaterThan(frame).And.LessThan(size - frame), $"dot {dot} sits inside the frame");
            }
        }
        finally {
            Object.DestroyImmediate(board);
        }
    }

    [Test]
    public void TheRotorPartsAreDrawnAndTheBladeIsSolidAtTheHubEnd() {
        Texture2D blade = HoverTextures.BladeTexture();
        Texture2D mast = HoverTextures.MastTexture();
        Texture2D hub = HoverTextures.HubTexture();
        try {
            Assert.That(blade.width, Is.EqualTo(HoverTextures.BladeLength));
            Assert.That(blade.height, Is.EqualTo(HoverTextures.BladeThickness));
            Assert.That(blade.GetPixel(2, HoverTextures.BladeThickness / 2).a, Is.GreaterThan(0.9f), "solid at the hub end");
            Assert.That(blade.GetPixel(HoverTextures.BladeLength - 1, HoverTextures.BladeThickness / 2).a, Is.GreaterThan(0.5f), "reaches the tip");
            Assert.That(blade.GetPixel(HoverTextures.BladeLength - 1, 0).a, Is.LessThan(0.2f), "thin at the tip");
            Assert.That(mast.GetPixel(HoverTextures.MastWidth / 2, 5).a, Is.EqualTo(1f));
            Assert.That(hub.GetPixel(0, 0).a, Is.Zero, "round");
            Assert.That(hub.GetPixel(HoverTextures.HubPixels / 2, HoverTextures.HubPixels / 2).a, Is.EqualTo(1f));
        }
        finally {
            Object.DestroyImmediate(blade);
            Object.DestroyImmediate(mast);
            Object.DestroyImmediate(hub);
        }
    }

    // ---- Routes ---------------------------------------------------------------------------------------------

    [Test]
    public void APatrolRunsBetweenItsTwoEndsAndEasesInAndOutOfEach() {
        HoverTargetSettings patrol = Route(HoverPathKind.Patrol, new Vector2(4f, 1f));
        Assert.That(HoverMath.PathOffset(patrol, 0f), Is.EqualTo(new Vector2(-4f, -1f)).Using(Vector2EqualityComparer.Instance));
        Assert.That(HoverMath.PathOffset(patrol, patrol.period / 2f), Is.EqualTo(new Vector2(4f, 1f)).Using(Vector2EqualityComparer.Instance));
        Assert.That(HoverMath.PathOffset(patrol, patrol.period), Is.EqualTo(new Vector2(-4f, -1f)).Using(Vector2EqualityComparer.Instance), "and back");

        float nearEnd = Mathf.Abs(HoverMath.PathOffset(patrol, 0.05f).x - HoverMath.PathOffset(patrol, 0f).x);
        float middle = Mathf.Abs(HoverMath.PathOffset(patrol, 2.05f).x - HoverMath.PathOffset(patrol, 2f).x);
        Assert.That(nearEnd, Is.LessThan(middle * 0.2f), "slow at the end, fast in the middle");
    }

    [Test]
    public void ALoopAndAFigureEightStayInsideTheirExtentReachItAndComeBackToTheStart() {
        foreach (HoverPathKind kind in new[] { HoverPathKind.Loop, HoverPathKind.FigureEight }) {
            HoverTargetSettings settings = Route(kind, new Vector2(3f, 1.5f));
            float reachX = 0f;
            float reachY = 0f;
            for (float time = 0f; time <= settings.period; time += 0.02f) {
                Vector2 offset = HoverMath.PathOffset(settings, time);
                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(3f + 1e-4f), kind + " x");
                Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(1.5f + 1e-4f), kind + " y");
                reachX = Mathf.Max(reachX, Mathf.Abs(offset.x));
                reachY = Mathf.Max(reachY, Mathf.Abs(offset.y));
            }
            Assert.That(reachX, Is.EqualTo(3f).Within(0.02f), kind + " reaches the full width");
            Assert.That(reachY, Is.EqualTo(1.5f).Within(0.02f), kind + " reaches the full height");
            Assert.That(HoverMath.PathOffset(settings, settings.period), Is.EqualTo(HoverMath.PathOffset(settings, 0f)).Using(Vector2EqualityComparer.Instance), kind + " closes");
        }
    }

    [Test]
    public void AFigureEightCrossesItselfInTheMiddleAndALoopStaysOnItsEllipse() {
        HoverTargetSettings eight = Route(HoverPathKind.FigureEight, new Vector2(3f, 1.5f));
        Assert.That(HoverMath.PathOffset(eight, 0f).magnitude, Is.LessThan(0.01f), "starts at the crossing");
        Assert.That(HoverMath.PathOffset(eight, eight.period / 2f).magnitude, Is.LessThan(0.01f), "and crosses it again halfway");

        HoverTargetSettings loop = Route(HoverPathKind.Loop, new Vector2(3f, 1.5f));
        for (float time = 0f; time < loop.period; time += 0.1f) {
            Vector2 offset = HoverMath.PathOffset(loop, time);
            float ring = (offset.x / 3f) * (offset.x / 3f) + (offset.y / 1.5f) * (offset.y / 1.5f);
            Assert.That(ring, Is.EqualTo(1f).Within(1e-3f), "on the ellipse");
        }
    }

    [Test]
    public void TheStartShareMovesWhereOnTheRouteItBegins() {
        HoverTargetSettings first = Route(HoverPathKind.Loop, new Vector2(3f, 3f));
        HoverTargetSettings second = Route(HoverPathKind.Loop, new Vector2(3f, 3f));
        second.startShare = 0.25f;
        Assert.That(HoverMath.PathOffset(second, 0f), Is.EqualTo(HoverMath.PathOffset(first, first.period * 0.25f)).Using(Vector2EqualityComparer.Instance));
    }

    [Test]
    public void TheBobStaysWithinItsHeightAndTheDescentCountsIt() {
        HoverTargetSettings settings = new HoverTargetSettings { bobHeight = 0.3f, bobPeriod = 2f, extent = new Vector2(2f, 1.2f) };
        float lowest = 0f;
        for (float time = 0f; time < 4f; time += 0.01f) {
            lowest = Mathf.Min(lowest, HoverMath.Bob(settings, time));
        }
        Assert.That(lowest, Is.EqualTo(-0.3f).Within(0.005f));
        Assert.That(HoverMath.Descent(settings), Is.EqualTo(1.5f).Within(1e-4f));
    }

    [Test]
    public void TheBoardLeansIntoItsMotionAndNeverPastTheLimit() {
        Assert.That(HoverMath.Tilt(Defaults, 2f), Is.LessThan(0f), "flying right tips clockwise");
        Assert.That(HoverMath.Tilt(Defaults, -2f), Is.GreaterThan(0f), "flying left tips counter-clockwise");
        Assert.That(HoverMath.Tilt(Defaults, 0f), Is.Zero);
        Assert.That(HoverMath.Tilt(Defaults, 1000f), Is.EqualTo(-Defaults.maxTilt));
        Assert.That(HoverMath.Tilt(Defaults, -1000f), Is.EqualTo(Defaults.maxTilt));
    }

    // ---- Points ---------------------------------------------------------------------------------------------

    [Test]
    public void AHitIsWorthTenAndTheHitThatBreaksTheBoardFifty() {
        Assert.That(HoverMath.Points(Defaults, false), Is.EqualTo(10));
        Assert.That(HoverMath.Points(Defaults, true), Is.EqualTo(50));
        Assert.That(Defaults.hitsToBreak, Is.EqualTo(3));
    }

    // ---- Rotor ----------------------------------------------------------------------------------------------

    [Test]
    public void TheBladesSwingWithTheCosineOfTheSpinHalfATurnApart() {
        Assert.That(HoverMath.BladeScale(0f, 0), Is.EqualTo(1f).Within(1e-5f));
        Assert.That(HoverMath.BladeScale(0f, 1), Is.EqualTo(-1f).Within(1e-5f));
        Assert.That(HoverMath.BladeScale(0.25f, 0), Is.EqualTo(0f).Within(1e-5f), "edge-on at a quarter turn");
        Assert.That(HoverMath.BladeScale(0.5f, 0), Is.EqualTo(-1f).Within(1e-5f));
        for (float turns = 0f; turns < 2f; turns += 0.07f) {
            Assert.That(HoverMath.BladeScale(turns, 0) + HoverMath.BladeScale(turns, 1), Is.EqualTo(0f).Within(1e-5f));
        }
    }

    [Test]
    public void ABladeSmearsWhileTheRotorIsFastAndIsSolidWhenItStops() {
        Assert.That(HoverMath.BladeAlpha(1f, 0f), Is.EqualTo(1f), "a stopped rotor is solid");
        Assert.That(HoverMath.BladeAlpha(0.2f, 1f), Is.LessThan(1f), "a fast one is smeared");
        Assert.That(HoverMath.BladeAlpha(0.2f, 1f), Is.LessThan(HoverMath.BladeAlpha(1f, 1f)), "fainter the shorter it looks");
        Assert.That(HoverMath.BladeAlpha(0.2f, 1f), Is.GreaterThan(0.3f), "but still there");
    }

    // ---- Popups ---------------------------------------------------------------------------------------------

    [Test]
    public void APopupFadesFromFullToNothingOverItsLifetimeWhileRisingAndSlowingDown() {
        TargetEffectsSettings settings = new TargetEffectsSettings();
        float previousAlpha = 2f;
        float previousRise = -1f;
        float previousStep = float.PositiveInfinity;
        for (int step = 0; step <= 12; step++) {
            float age = settings.popupLifetime * step / 12f;
            float alpha = EffectMath.PopupAlpha(age, settings.popupLifetime);
            float rise = EffectMath.PopupRise(age, settings.popupLifetime, settings.popupRise);
            Assert.That(alpha, Is.LessThanOrEqualTo(previousAlpha), "never gets more opaque");
            Assert.That(rise, Is.GreaterThanOrEqualTo(previousRise), "never sinks");
            if (step > 0) {
                float gained = rise - previousRise;
                Assert.That(gained, Is.LessThanOrEqualTo(previousStep + 1e-5f), "ease-out: each step rises less than the one before");
                previousStep = gained;
            }
            previousAlpha = alpha;
            previousRise = rise;
        }
        Assert.That(EffectMath.PopupAlpha(0f, settings.popupLifetime), Is.EqualTo(1f));
        Assert.That(EffectMath.PopupAlpha(settings.popupLifetime, settings.popupLifetime), Is.EqualTo(0f));
        Assert.That(EffectMath.PopupRise(0f, settings.popupLifetime, settings.popupRise), Is.Zero);
        Assert.That(EffectMath.PopupRise(settings.popupLifetime, settings.popupLifetime, settings.popupRise), Is.EqualTo(settings.popupRise).Within(1e-5f));
        Assert.That(EffectMath.PopupAlpha(settings.popupLifetime * 5f, settings.popupLifetime), Is.EqualTo(0f), "stays gone");
    }

    [Test]
    public void ABreakingPopupIsBiggerAndBrighterThanAHitAndLongEnoughToRead() {
        TargetEffectsSettings settings = new TargetEffectsSettings();
        Assert.That(settings.breakTextSize, Is.GreaterThan(settings.hitTextSize));
        Assert.That(settings.breakTextColor.maxColorComponent, Is.GreaterThanOrEqualTo(settings.hitTextColor.maxColorComponent));
        Assert.That(settings.breakTextColor.r - settings.breakTextColor.b, Is.GreaterThan(settings.hitTextColor.r - settings.hitTextColor.b), "more saturated");
        Assert.That(settings.popupLifetime, Is.InRange(0.8f, 2.5f));
        Assert.That(settings.popupRise, Is.InRange(1f, 2.5f));
    }

    [Test]
    public void TheTargetEffectsSettingsSurviveAPresetRoundTripAndCopyAndCloneIsDeep() {
        MovementPreset preset = MovementPreset.CodeDefaults();
        preset.effects.targets.popupRise = 2.2f;
        preset.effects.targets.breakSplinters = 5f;

        MovementPreset loaded = MovementPreset.FromJson(preset.ToJson());
        Assert.That(loaded.effects.targets.popupRise, Is.EqualTo(2.2f));
        Assert.That(loaded.effects.targets.breakSplinters, Is.EqualTo(5f));

        MovementPreset copy = preset.Copy();
        copy.effects.targets.popupRise = 0.5f;
        Assert.That(preset.effects.targets.popupRise, Is.EqualTo(2.2f), "the copy owns its target effects");

        EffectsSettings target = new EffectsSettings();
        TargetEffectsSettings held = target.targets;
        target.CopyFrom(preset.effects);
        Assert.That(target.targets, Is.SameAs(held), "the effects keep the instance they hold");
        Assert.That(held.popupRise, Is.EqualTo(2.2f));

        MovementPreset old = MovementPreset.FromJson("{\"version\":4,\"run\":{\"lean\":21}}");
        Assert.That(old.effects.targets, Is.Not.Null);
        Assert.That(old.effects.targets.popupLifetime, Is.EqualTo(new TargetEffectsSettings().popupLifetime));
    }
}
