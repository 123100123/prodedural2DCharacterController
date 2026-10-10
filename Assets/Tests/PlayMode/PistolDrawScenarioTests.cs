using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The draw and holster cycle on the real character (plan §12-13, §23): holstered on the front thigh at rest, aim draws
// (the hand reaches the holster, grips, carries the pistol up, the support hand joins), release holsters, every
// interruption continues from where the hand is, a buffered click fires once on arrival, a roll quick-holsters and the
// pistol is drawn again after it, and the pistol never jumps within what carries it. Also writes pictures
// (Temp/LocomotionScenarios/holster-*.png of the thigh holster, pistol-draw-*.png sheets of the draw and holster).
public class PistolDrawScenarioTests : ScenarioFixture {
    // The most the pistol may move in one frame within the holster, or across a change of parent (plan §27).
    private const float MaxJump = 0.03f;
    private const int MaxDrawFrames = 60;

    private CharacterDriver Driver => Stage.Character;
    private PistolView View => Driver.Parts.PistolView;
    private PistolHolder Holder => Driver.Parts.PistolHolder;
    private PistolHandlingSettings Settings => Driver.Parts.PistolHandling;
    private Vector2 Body => Stage.Stride.transform.position;
    private WeaponController Weapon => Driver.Parts.WeaponController;
    private ArmLimb FrontArm => Arm("frontArm");
    private ArmLimb BackArm => Arm("backArm");

    private ArmLimb Arm(string field) => (ArmLimb)typeof(CharacterDriver).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Driver);

    // The aim button is a tap toggle on real (unscaled) time: a press released within its tap time latches aim on. Every
    // release here comes after the button has been held past it, so a release really lets go of aim.
    private const float PastTheTap = 0.3f;
    private float aimPressedAt = -1f;

    // The fixture instance is shared by the tests in it.
    [SetUp]
    public void ForgetTheAimPress() => aimPressedAt = -1f;

    private bool HeldPastTheTap => aimPressedAt >= 0f && Time.unscaledTime - aimPressedAt > PastTheTap;

    private void Aim(Vector2 offset, bool fire = false) {
        if (aimPressedAt < 0f) {
            aimPressedAt = Time.unscaledTime;
        }
        Stage.AimAndFire(Body + offset, fire);
    }

    private void ReleaseAim() {
        Assert.That(HeldPastTheTap, Is.True, "aim held long enough not to be a tap");
        Stage.StopAiming();
        aimPressedAt = -1f;
    }

    // Aims at the offset until the pistol is at the aim (and the button is held past a tap).
    private IEnumerator DrawAt(Vector2 offset) {
        for (int frame = 0; frame < 600 && !(Holder.IsEquipped && HeldPastTheTap); frame++) {
            Aim(offset);
            yield return null;
        }
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), "drawn to the aim");
    }

    // Holds aim at the offset until the condition holds and the button is past a tap.
    private IEnumerator AimUntil(Vector2 offset, System.Func<bool> condition) {
        for (int frame = 0; frame < 600 && !(condition() && HeldPastTheTap); frame++) {
            Aim(offset);
            yield return null;
        }
    }

    private IEnumerator HolsterFully() {
        ReleaseAim();
        for (int frame = 0; frame < MaxDrawFrames && Holder.State != DrawState.Holstered; frame++) {
            yield return null;
        }
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered), "holstered again");
    }

    private static IEnumerator Frames(int count, System.Action each = null) {
        for (int frame = 0; frame < count; frame++) {
            each?.Invoke();
            yield return null;
        }
    }

    // Slows the draw and holster so an interruption can land at a chosen point after the aim button is past a tap.
    private void SlowTheCycle(float seconds) {
        Settings.drawDuration = seconds;
        Settings.holsterDuration = seconds;
    }

    // ---- Rest and the holster on the thigh --------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator AtRestThePistolIsHolsteredOnTheOutsideOfTheFrontThigh() {
        yield return Hold(1f);
        Transform thigh = Stage.Stride.FrontLeg.Hip;
        Assert.That(thigh.name, Does.Contain("thighF"), "the front thigh bone");
        Assert.That(View.Holster.parent, Is.SameAs(thigh), "the holster hangs from the front thigh");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(View.Parent, Is.SameAs(View.Holster));
        Assert.That(Vector2.Distance(View.Pistol.position, View.HolsterGrip), Is.LessThan(0.01f), "in the holster");

        Vector2 hip = thigh.position;
        Vector2 knee = thigh.GetChild(0).position;
        Vector2 along = knee - hip;
        float down = Vector2.Dot((Vector2)View.HolsterGrip - hip, along.normalized) / along.magnitude;
        Vector2 middle = ((Vector2)View.Muzzle.position + (Vector2)View.HolsterGrip) / 2f;
        float middleDown = Vector2.Dot(middle - hip, along.normalized) / along.magnitude;
        float across = Mathf.Abs(Vector3.Cross(along.normalized, middle - hip).z);
        Report($"grip {down:F2} and the pistol's middle {middleDown:F2} of the way down the thigh, {across:F3} off its line; barrel along the thigh within {Vector2.Angle(View.HolsteredDirection, along):F1}°");
        Assert.That(down, Is.InRange(0.05f, 0.45f), "the grip up near the hip");
        Assert.That(middleDown, Is.InRange(0.25f, 0.65f), "about a third of the way down the thigh");
        Assert.That(across, Is.LessThan(0.15f), "on the thigh, not off it");
        Assert.That(Vector2.Angle(View.HolsteredDirection, along), Is.LessThan(25f), "the barrel points down the thigh");
        SpriteRenderer thighSprite = FindSprite("thighF");
        SpriteRenderer forearmSprite = FindSprite("forearmF");
        SpriteRenderer pistolSprite = View.Pistol.GetComponent<SpriteRenderer>();
        Assert.That(pistolSprite.sortingOrder, Is.GreaterThan(thighSprite.sortingOrder), "in front of the thigh");
        Assert.That(pistolSprite.sortingOrder, Is.LessThan(forearmSprite.sortingOrder), "behind the front forearm");
        Assert.That(View.Holster.GetComponentInChildren<SpriteRenderer>().sortingOrder, Is.EqualTo(pistolSprite.sortingOrder));
    }

    private static SpriteRenderer FindSprite(string name) {
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) {
            if (renderer.name == name) {
                return renderer;
            }
        }
        Assert.Fail($"no sprite {name}");
        return null;
    }

    [UnityTest]
    public IEnumerator WalkingAndRunningTheHolsteredPistolSwingsWithTheThighWithoutJumping() {
        yield return Hold(0.5f);
        PistolTrack track = PistolTrack.Follow(View);
        Stage.Press(Key.D);
        yield return Hold(1.5f);
        Stage.Press(Key.LeftShift);
        yield return Hold(1.5f);
        Stage.ReleaseAll();
        yield return Hold(1f);
        Report($"{track}; fastest world move in the holster (carried by the leg) {track.FastestInHolster:F3}");
        Assert.That(track.Swaps, Is.Zero);
        Assert.That(track.WorstInHolster, Is.LessThan(0.001f), "carried by the thigh, still in the holster");
    }

    [UnityTest]
    public IEnumerator SavesPicturesOfTheThighHolsterAtRestAndWalkingBothWays() {
        yield return Hold(1f);
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        Vector2 frame = new Vector2(0f, -0.4f);
        PistolScenarioTests.Write(folder, "holster-rest-right.png", Body + frame, 1.8f);
        Stage.Press(Key.D);
        yield return Hold(0.75f);
        PistolScenarioTests.Write(folder, "holster-walk-right.png", Body + frame, 1.8f);
        yield return Hold(0.2f);
        PistolScenarioTests.Write(folder, "holster-walk-right-2.png", Body + frame, 1.8f);
        Stage.Release(Key.D);
        Stage.Press(Key.A);
        yield return Hold(1.2f);
        PistolScenarioTests.Write(folder, "holster-walk-left.png", Body + frame, 1.8f);
        Stage.Release(Key.A);
        yield return Hold(1f);
        Assert.That(Driver.transform.localScale.x, Is.LessThan(0f), "faces left");
        PistolScenarioTests.Write(folder, "holster-rest-left.png", Body + frame, 1.8f);
        // The hand at the holster as it grips, then drawn.
        for (int count = 0; count < MaxDrawFrames && !View.IsInHand; count++) {
            Aim(new Vector2(-6f, 1f));
            yield return null;
        }
        PistolScenarioTests.Write(folder, "holster-grip-left.png", Body + frame, 1.8f);
        yield return DrawAt(new Vector2(-6f, 1f));
        PistolScenarioTests.Write(folder, "holster-drawn-left.png", Body + new Vector2(-0.4f, 0f), 1.8f);
        Stage.StopAiming();
        Assert.That(File.Exists(Path.Combine(folder, "holster-rest-left.png")), Is.True);
    }

    // ---- The draw ----------------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator AimDrawsTheHandToTheHolsterThenCarriesThePistolUpToTheAim() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        List<DrawState> states = new List<DrawState>();
        int attachFrame = -1;
        int frames = 0;
        float handToHolsterAtGrip = -1f;
        float handToHolster = float.MaxValue;
        while (!Holder.IsEquipped && frames < MaxDrawFrames) {
            Aim(offset);
            yield return null;
            frames++;
            if (states.Count == 0 || states[states.Count - 1] != Holder.State) {
                states.Add(Holder.State);
            }
            if (!View.IsInHand) {
                Assert.That(View.Parent, Is.SameAs(View.Holster), "the pistol stays in the holster until the grip");
            } else if (attachFrame < 0) {
                attachFrame = frames;
                handToHolsterAtGrip = handToHolster;
                Assert.That(Holder.Cycle.Progress, Is.GreaterThanOrEqualTo(Settings.gripShare), "attached at the grip share");
            }
            handToHolster = Vector2.Distance(View.WeaponPivot.position, View.HolsterGrip);
        }
        float seconds = frames * ScenarioStage.FrameTime;
        float gripFrames = Settings.gripShare * Settings.drawDuration / ScenarioStage.FrameTime;
        Report($"states {string.Join(" > ", states)}; attach at frame {attachFrame} (grip share at {gripFrames:F1}), equipped after {seconds:F3} s; hand to the holster grip at the grip {handToHolsterAtGrip:F3}");
        Assert.That(states, Is.EqualTo(new[] { DrawState.Drawing, DrawState.Equipped }));
        Assert.That(seconds, Is.EqualTo(Settings.drawDuration).Within(2.5f * ScenarioStage.FrameTime), "the draw takes the draw time");
        Assert.That(attachFrame, Is.InRange(Mathf.FloorToInt(gripFrames), Mathf.CeilToInt(gripFrames) + 1), "grips at the grip share");
        Assert.That(handToHolsterAtGrip, Is.LessThan(0.2f), "the hand is at the holster when it grips");
        Assert.That(View.Parent, Is.SameAs(View.WeaponPivot));

        // Equipped: the aim pose holds both hands and the barrel along the aim.
        yield return Frames(20, () => Aim(offset));
        Assert.That(Vector2.Angle(View.BarrelDirection, View.AimDirection), Is.LessThan(1f), "the barrel along the aim");
        WeaponReadout readout = Holder.Readout;
        Report($"equipped: grip error {readout.GripError:F4}, support error {readout.SupportError:F3}, support weight {readout.SupportWeight:F2}; {track}");
        Assert.That(readout.GripError, Is.LessThan(0.01f), "the pistol sits in the hand");
        Assert.That(readout.SupportWeight, Is.EqualTo(1f));
        Assert.That(readout.SupportError, Is.LessThan(0.15f), "the support hand is on the pistol");
        Assert.That(track.Swaps, Is.EqualTo(1));
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump), "no jump at the grip");
        Assert.That(track.WorstInHolster, Is.LessThan(0.001f));
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator EquippedTheShotLeavesFromTheMuzzleTransformAndRepeatedClicksEachFire() {
        yield return Hold(0.6f);
        Vector2 offset = new Vector2(6f, 1.5f);
        yield return DrawAt(offset);
        int shots = 0;
        float worst = 0f;
        System.Action<(Vector2 muzzle, Vector2 direction)> record = fired => {
            shots++;
            worst = Mathf.Max(worst, Vector2.Distance(fired.muzzle, View.Muzzle.position));
        };
        Driver.Events.Fired += record;
        const int Clicks = 5;
        const int FramesBetweenClicks = 16;
        for (int frame = 0; frame < Clicks * FramesBetweenClicks; frame++) {
            Aim(offset, frame % FramesBetweenClicks < 2);
            yield return null;
        }
        Driver.Events.Fired -= record;
        Stage.StopAiming();
        Report($"{shots} shots, the Fired muzzle off the Muzzle transform by at most {worst:F5}");
        Assert.That(shots, Is.EqualTo(Clicks), "every click fires once");
        Assert.That(worst, Is.LessThan(0.001f), "the Fired muzzle is the Muzzle transform");
    }

    [UnityTest]
    public IEnumerator AClickDuringTheDrawIsBufferedAndFiresOnceTheMomentThePistolArrives() {
        yield return Hold(0.6f);
        Vector2 offset = new Vector2(6f, 1f);
        int shots = 0;
        int shotFrame = -1;
        int equippedFrame = -1;
        float muzzleError = -1f;
        int frame = 0;
        Driver.Events.Fired += fired => {
            shots++;
            shotFrame = frame;
            muzzleError = Vector2.Distance(fired.muzzle, View.Muzzle.position);
        };
        bool bufferedWhileDrawing = false;
        for (; frame < 50; frame++) {
            // Three clicks during the draw: they do not stack.
            bool click = frame == 0 || frame == 1 || frame == 5 || frame == 9;
            Aim(offset, click);
            yield return null;
            if (Holder.State == DrawState.Drawing) {
                bufferedWhileDrawing |= Holder.Readout.FireBuffered;
                Assert.That(shots, Is.Zero, "no shot before the pistol is at the aim");
            }
            if (equippedFrame < 0 && Holder.IsEquipped) {
                equippedFrame = frame;
            }
        }
        Stage.StopAiming();
        Report($"equipped at frame {equippedFrame}, shot at frame {shotFrame}, {shots} shots, muzzle error {muzzleError:F5}");
        Assert.That(bufferedWhileDrawing, Is.True, "the click waited for the draw");
        Assert.That(shots, Is.EqualTo(1), "one buffered shot, however many clicks");
        Assert.That(shotFrame, Is.EqualTo(equippedFrame), "fired the frame the pistol reached the aim");
        Assert.That(muzzleError, Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator ABufferedClickIsDroppedWhenAimIsReleasedBeforeThePistolArrives() {
        yield return Hold(0.6f);
        SlowTheCycle(20f);
        Vector2 offset = new Vector2(6f, 1f);
        int shots = 0;
        Driver.Events.Fired += _ => shots++;
        yield return AimUntil(offset, () => true);
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(Holder.Readout.FireBuffered, Is.False, "nothing buffered before a click");
        Aim(offset, true);
        yield return null;
        Assert.That(Holder.Readout.FireBuffered, Is.True, "the click waits for the draw");
        ReleaseAim();
        yield return null;
        Assert.That(Holder.Readout.FireBuffered, Is.False, "dropped with the aim");
        SlowTheCycle(0.35f);
        yield return Hold(0.6f);
        yield return DrawAt(offset);
        yield return Frames(20, () => Aim(offset));
        Stage.StopAiming();
        Assert.That(shots, Is.Zero, "the dropped click never fires, not even on the next draw");
    }

    // ---- The holster ---------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator ReleasingAimHolstersThePistolBackIntoTheHolsterPose() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        yield return DrawAt(new Vector2(6f, 1f));
        yield return Frames(15, () => Aim(new Vector2(6f, 1f)));
        ReleaseAim();
        int frames = 0;
        int detachFrame = -1;
        float handToHolster = -1f;
        float handToHolsterAtInsert = -1f;
        float supportPastRelease = -1f;
        while (Holder.State != DrawState.Holstered && frames < MaxDrawFrames) {
            yield return null;
            frames++;
            if (Holder.State == DrawState.Holstering && Holder.Cycle.Progress >= 0.36f && supportPastRelease < 0f) {
                supportPastRelease = Holder.Cycle.SupportWeight;
            }
            if (detachFrame < 0 && !View.IsInHand) {
                detachFrame = frames;
                handToHolsterAtInsert = handToHolster;
            }
            handToHolster = Vector2.Distance(View.WeaponPivot.position, View.HolsterGrip);
        }
        float seconds = frames * ScenarioStage.FrameTime;
        Report($"holstered after {seconds:F3} s, inserted at frame {detachFrame} with the hand {handToHolsterAtInsert:F3} from the holster; support weight past 0.36: {supportPastRelease:F2}");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(seconds, Is.EqualTo(Settings.holsterDuration).Within(2.5f * ScenarioStage.FrameTime));
        Assert.That(supportPastRelease, Is.EqualTo(0f), "the support hand has let go by about a third of the holster");
        Assert.That(handToHolsterAtInsert, Is.LessThan(0.2f), "the hand brought the pistol to the holster");
        yield return Hold(0.3f);
        Report(track.ToString());
        Assert.That(Vector2.Distance(View.Pistol.position, View.HolsterGrip), Is.LessThan(0.01f), "settled in the holster pose");
        Assert.That(Quaternion.Angle(View.Pistol.localRotation, Quaternion.identity), Is.LessThan(1f));
        Assert.That(View.Pistol.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(PistolView.HolsteredOrder));
        Assert.That(track.Swaps, Is.EqualTo(2));
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump), "no jump at the grip or the insert");
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump), "no jump within the holster as it settles");
    }

    // ---- Interruptions (§13) ---------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator AimReleasedBeforeTheGripLeavesThePistolInTheHolster() {
        yield return Hold(0.6f);
        SlowTheCycle(20f);
        PistolTrack track = PistolTrack.Follow(View);
        yield return AimUntil(new Vector2(6f, 1f), () => true);
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(Holder.Cycle.Progress, Is.LessThan(Settings.gripShare));
        ReleaseAim();
        yield return null;
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered), "straight back to holstered");
        yield return Hold(0.5f);
        Report(track.ToString());
        Assert.That(track.Swaps, Is.Zero, "the pistol never left the holster");
        Assert.That(track.WorstInHolster, Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator AimReleasedAfterTheGripHolstersFromTheMirroredProgressAndAimAgainDrawsFromThere() {
        yield return Hold(0.6f);
        SlowTheCycle(3f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return AimUntil(offset, () => Holder.Cycle.Progress >= 0.6f);
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(View.IsInHand, Is.True);
        float pathBefore = Holder.Cycle.PathPosition;
        ReleaseAim();
        yield return null;
        Report($"released at path {pathBefore:F3}: {Holder.State} at progress {Holder.Cycle.Progress:F3}, path {Holder.Cycle.PathPosition:F3}");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstering));
        Assert.That(Holder.Cycle.PathPosition, Is.EqualTo(pathBefore).Within(0.02f), "continues from where the hand is");
        Assert.That(View.IsInHand, Is.True, "no restart: the pistol stays in the hand");

        // Aim again before the insert: drawing again from the mirrored progress, the pistol still in the hand.
        yield return Hold(0.1f);
        float pathHolstering = Holder.Cycle.PathPosition;
        Aim(offset);
        yield return null;
        Report($"aimed again at path {pathHolstering:F3}: {Holder.State} at progress {Holder.Cycle.Progress:F3}");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(Holder.Cycle.Progress, Is.EqualTo(pathHolstering).Within(0.02f));
        Assert.That(View.IsInHand, Is.True);
        yield return DrawAt(offset);
        Report(track.ToString());
        Assert.That(track.Swaps, Is.EqualTo(1), "only the first grip changed the parent");
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator AimPressedAfterTheInsertRestartsTheDrawFromTheHolster() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return DrawAt(offset);
        ReleaseAim();
        for (int frame = 0; frame < MaxDrawFrames && View.IsInHand; frame++) {
            yield return null;
        }
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstering));
        Assert.That(View.Parent, Is.SameAs(View.Holster), "inserted");
        // The hand on its way back to the swing (just after the insert it would grip again at once).
        for (int frame = 0; frame < MaxDrawFrames && Holder.Cycle.Progress < 0.85f; frame++) {
            yield return null;
        }
        Aim(offset);
        yield return null;
        Report($"aimed after the insert: {Holder.State} at progress {Holder.Cycle.Progress:F3} (grip at {Settings.gripShare})");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        Assert.That(Holder.Cycle.Progress, Is.LessThan(Settings.gripShare), "the draw starts again, reaching for the holster");
        Assert.That(View.Parent, Is.SameAs(View.Holster));
        yield return DrawAt(offset);
        Report(track.ToString());
        Assert.That(track.Swaps, Is.EqualTo(3));
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator FlippingTheFacingDuringTheDrawFinishesTheDrawOnTheNewSide() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        yield return Frames(8, () => Aim(new Vector2(6f, 1f)));
        Assert.That(Holder.State, Is.EqualTo(DrawState.Drawing));
        yield return DrawAt(new Vector2(-6f, 1f));
        yield return Frames(20, () => Aim(new Vector2(-6f, 1f)));
        Report($"barrel {View.BarrelDirection}; {track}");
        Assert.That(Driver.transform.localScale.x, Is.LessThan(0f), "faces left");
        Assert.That(View.BarrelDirection.x, Is.LessThan(-0.9f), "points left");
        Assert.That(View.Pistol.TransformVector(Vector3.up).y, Is.GreaterThan(0f), "the slide on top");
        yield return HolsterFully();
        yield return Hold(0.3f);
        Report(track.ToString());
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
    }

    [UnityTest]
    public IEnumerator JumpingWhileDrawingCarriesOnAndTheDrawFinishesInTheAir() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Stage.Press(Key.Space);
        bool airborneWhileDrawing = false;
        for (int frame = 0; frame < MaxDrawFrames && !Holder.IsEquipped; frame++) {
            Aim(new Vector2(6f, 1f));
            yield return null;
            airborneWhileDrawing |= Driver.CurrentState is AirborneState;
        }
        Stage.Release(Key.Space);
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped));
        Assert.That(airborneWhileDrawing, Is.True, "the draw went on through the jump");
        yield return Frames(60, () => Aim(new Vector2(6f, 1f)));
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), "still drawn after landing");
        Report(track.ToString());
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator ARollQuickHolstersThePistolAndItIsDrawnAgainAfterwardsWhileAimIsHeld() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return DrawAt(offset);
        Stage.Press(Key.D);
        yield return Frames(20, () => Aim(offset));
        Stage.Press(Key.LeftAlt);
        bool rolled = false;
        bool quick = false;
        int rollStart = -1;
        int holsteredAt = -1;
        int shots = 0;
        float lowestHand = float.MaxValue;
        Driver.Events.Fired += _ => shots++;
        for (int frame = 0; frame < 150; frame++) {
            // Clicks well inside the roll are ignored (not buffered for the redraw after it).
            Aim(offset, rolled && frame - rollStart < 30 && frame % 6 == 0);
            yield return null;
            bool rolling = Driver.CurrentState is RollState;
            if (rolling) {
                if (!rolled) {
                    rollStart = frame;
                }
                rolled = true;
                lowestHand = Mathf.Min(lowestHand, FrontArm.HandPosition.y - ScenarioStage.GroundTop);
                quick |= Holder.Readout.IsQuick;
                if (holsteredAt < 0 && Holder.State == DrawState.Holstered) {
                    holsteredAt = frame - rollStart;
                }
                Assert.That(Holder.CanFire, Is.False, "no shots while rolling");
                Assert.That(Holder.FiresNow, Is.False);
            }
            if (frame == 2) {
                Stage.Release(Key.LeftAlt);
            }
            if (rolled && !rolling && Holder.IsEquipped) {
                break;
            }
        }
        Stage.ReleaseAll();
        Stage.StopAiming();
        float holsterSeconds = holsteredAt * ScenarioStage.FrameTime;
        Report($"rolled {rolled}, quick {quick}, holstered {holsterSeconds:F3} s into the roll, then {Holder.State}; the front hand came within {lowestHand:F3} of the ground; {shots} shots; {track}");
        Assert.That(rolled, Is.True);
        Assert.That(quick, Is.True, "a quick holster");
        Assert.That(lowestHand, Is.LessThan(Driver.Parts.Roll.contactTolerance), "the quick holster did not keep the front hand off the ground");
        Assert.That(shots, Is.Zero, "clicks in the roll do not fire, nor fire later");
        Assert.That(holsterSeconds, Is.InRange(0f, Settings.quickHolsterDuration + 0.05f), "holstered at the quick time");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), "drawn again after the roll with aim held");
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
    }

    [UnityTest]
    public IEnumerator ClimbingALedgeQuickHolstersWithoutHinderingTheGrabAndTheDrawResumesOnTop() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return DrawAt(offset);
        // A tall ledge just ahead; lifted beside its top, the character falls to its hands and hangs.
        float top = Stage.AddLedge((Body.x + 0.5f, 9f, 30f));
        Stage.Lift(top - 1.5f - Body.y);
        ClimbState climb = Driver.Parts.Climb;
        bool quick = false;
        bool holsteredInTheClimb = false;
        int shots = 0;
        Driver.Events.Fired += _ => shots++;
        for (int frame = 0; frame < 150 && !climb.IsHanging; frame++) {
            Aim(offset);
            yield return null;
            quick |= Driver.CurrentState is ClimbState && Holder.Readout.IsQuick;
        }
        Assert.That(climb.IsHanging, Is.True, "grabbed the ledge and hangs, the pistol in hand as it reached");
        for (int frame = 0; frame < 30; frame++) {
            Aim(offset, frame % 6 == 0);
            yield return null;
            quick |= Holder.Readout.IsQuick;
            holsteredInTheClimb |= Driver.CurrentState is ClimbState && Holder.State == DrawState.Holstered;
            Assert.That(Holder.CanFire, Is.False, "no shots while hanging");
        }
        Stage.Press(Key.W);
        bool onTop = false;
        for (int frame = 0; frame < 300 && !(onTop && Holder.IsEquipped); frame++) {
            Aim(offset);
            yield return null;
            onTop |= Driver.CurrentState is WalkState;
        }
        Stage.ReleaseAll();
        Stage.StopAiming();
        Report($"quick {quick}, holstered in the climb {holsteredInTheClimb}, on top {onTop} ({Stage.Stride.StanceHeight:F2} vs {top:F2}), then {Holder.State}; {shots} shots; {track}");
        Assert.That(quick, Is.True, "a quick holster");
        Assert.That(holsteredInTheClimb, Is.True);
        Assert.That(shots, Is.Zero, "clicks while hanging do not fire");
        Assert.That(onTop, Is.True, "climbed up");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(0.05f), "stands on top");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), "drawn again on top with aim held");
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
    }

    [UnityTest]
    public IEnumerator SlidingWhileAimingDrawsAndFiresAsWalkingDoes() {
        yield return Hold(0.6f);
        Vector2 offset = new Vector2(6f, 0.5f);
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(1.5f);
        Stage.Press(Key.LeftCtrl);
        for (int frame = 0; frame < 12 && !(Driver.CurrentState is SlideState); frame++) {
            yield return null;
        }
        Assert.That(Driver.CurrentState, Is.InstanceOf<SlideState>(), "a crouch pressed at a run slides");
        int shots = 0;
        float worstMuzzle = 0f;
        Driver.Events.Fired += fired => {
            shots++;
            worstMuzzle = Mathf.Max(worstMuzzle, Vector2.Distance(fired.muzzle, View.Muzzle.position));
        };
        // A click on the first frame of the aim is buffered and fires as the pistol arrives, still in the slide.
        bool equippedInTheSlide = false;
        int slideFrames = 0;
        for (int frame = 0; frame < 120 && Driver.CurrentState is SlideState; frame++) {
            Aim(offset, frame == 0);
            yield return null;
            slideFrames++;
            equippedInTheSlide |= Holder.IsEquipped && Driver.CurrentState is SlideState;
        }
        Stage.ReleaseAll();
        Stage.StopAiming();
        Report($"slid {slideFrames} frames aiming, equipped in the slide {equippedInTheSlide}, {shots} shots, muzzle error {worstMuzzle:F5}");
        Assert.That(equippedInTheSlide, Is.True, "drawn to the aim within the slide");
        Assert.That(shots, Is.EqualTo(1), "the buffered click fired");
        Assert.That(worstMuzzle, Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator AReloadStartedHolsteredFinishesOutOfSightAndOneStartedAtTheAimShowsTheMagazine() {
        yield return Hold(0.6f);
        Vector2 offset = new Vector2(6f, 1f);
        int finished = 0;
        Driver.Events.ReloadFinished += _ => finished++;
        yield return DrawAt(offset);
        yield return Frames(4, () => Aim(offset, true));
        yield return Frames(2, () => Aim(offset));
        Assert.That(Weapon.Magazine.Rounds, Is.LessThan(Weapon.Magazine.Capacity), "a round is gone");
        yield return HolsterFully();

        // Holstered: R starts the reload, and the draw half-way through it does not show it.
        Stage.Press(Key.R);
        yield return null;
        Stage.Release(Key.R);
        yield return null;
        Assert.That(Weapon.IsReloading, Is.True, "the magazine timer runs while holstered");
        bool shownWhileHidden = false;
        // Once the support hand has settled on the pistol at the aim, how far it strays from its grip (a shown reload
        // drops it to the magazine, well below).
        int framesAtTheAim = 0;
        float supportOffItsGrip = 0f;
        for (int frame = 0; frame < 150 && Weapon.IsReloading; frame++) {
            Aim(offset);
            yield return null;
            // Only while it runs: the frame it completes the holder already shows the next one (the pistol is at the aim).
            shownWhileHidden |= Holder.ShowsReload && Weapon.IsReloading;
            framesAtTheAim = Holder.IsEquipped ? framesAtTheAim + 1 : 0;
            if (framesAtTheAim > 15) {
                supportOffItsGrip = Mathf.Max(supportOffItsGrip, Holder.Readout.SupportError);
            }
        }
        Report($"a reload started holstered: shown {shownWhileHidden}; the settled support hand off its grip by at most {supportOffItsGrip:F3} over {framesAtTheAim} frames at the aim; finished {finished}");
        Assert.That(Weapon.IsReloading, Is.False, "it completes");
        Assert.That(Weapon.Magazine.Rounds, Is.EqualTo(Weapon.Magazine.Capacity));
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(shownWhileHidden, Is.False, "never shown: it began out of sight");
        Assert.That(framesAtTheAim, Is.GreaterThan(30), "drawn well before the reload ended");
        Assert.That(supportOffItsGrip, Is.LessThan(0.15f), "the support hand did not go down for the magazine");

        // Equipped: a reload shows (the support hand fetches the magazine).
        yield return Frames(20, () => Aim(offset));
        yield return Frames(4, () => Aim(offset, true));
        yield return Frames(2, () => Aim(offset));
        float restSupport = BackArm.HandPosition.y;
        Stage.Press(Key.R);
        Aim(offset);
        yield return null;
        Stage.Release(Key.R);
        bool shown = false;
        float lowest = float.MaxValue;
        for (int frame = 0; frame < 150 && (Weapon.IsReloading || frame < 2); frame++) {
            Aim(offset);
            yield return null;
            shown |= Holder.ShowsReload && Weapon.IsReloading;
            lowest = Mathf.Min(lowest, BackArm.HandPosition.y);
        }
        Stage.StopAiming();
        Report($"a reload at the aim: shown {shown}, the support hand dropped {restSupport - lowest:F3}");
        Assert.That(shown, Is.True);
        Assert.That(restSupport - lowest, Is.GreaterThan(0.2f), "the support hand went down for the magazine");
        Assert.That(finished, Is.EqualTo(2));
    }

    // ---- Moving while drawn ------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator WalkingRunningCrouchingAndJumpingKeepThePistolDrawnAndAimed() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return DrawAt(offset);
        (string name, Key[] keys, int frames)[] moves = {
            ("walk", new[] { Key.D }, 60),
            ("run toward the aim", new[] { Key.D, Key.LeftShift }, 60),
            ("crouch", new[] { Key.LeftCtrl }, 50),
            ("jump", new[] { Key.Space }, 50),
        };
        foreach ((string name, Key[] keys, int frames) move in moves) {
            Stage.Press(move.keys);
            float worstAim = 0f;
            for (int frame = 0; frame < move.frames; frame++) {
                Aim(offset);
                yield return null;
                Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), $"still drawn while {move.name}");
                worstAim = Mathf.Max(worstAim, Vector2.Angle(View.BarrelDirection, View.AimDirection));
            }
            Stage.ReleaseAll();
            Report($"{move.name}: barrel off the aim by at most {worstAim:F2}°");
            // Read before this frame's pose: the barrel is last frame's, the aim this frame's.
            Assert.That(worstAim, Is.LessThan(3f), $"the barrel follows the aim while {move.name}");
            yield return Frames(20, () => Aim(offset));
        }
        Assert.That(track.Swaps, Is.EqualTo(1));
        Stage.StopAiming();
    }
    [UnityTest]
    public IEnumerator CatchingAWallQuickHolstersThePistolAndItIsDrawnAgainAfterTheSlideWhileAimIsHeld() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        yield return DrawAt(offset);
        Stage.AddWall((Body.x + 1f, 1, 30f));
        yield return Frames(5, () => Aim(offset));
        Stage.Lift(6f);
        Stage.Press(Key.D);
        bool slid = false;
        bool quick = false;
        int slideFrames = 0;
        int holsteredAt = -1;
        int shots = 0;
        Driver.Events.Fired += _ => shots++;
        for (int frame = 0; frame < 400; frame++) {
            // Clicks early in the slide are ignored (and not kept for the redraw after it).
            bool sliding = Driver.CurrentState is WallSlideState;
            Aim(offset, sliding && slideFrames < 20 && frame % 4 == 0);
            yield return null;
            sliding = Driver.CurrentState is WallSlideState;
            if (sliding) {
                slid = true;
                slideFrames++;
                quick |= Holder.Readout.IsQuick;
                if (holsteredAt < 0 && Holder.State == DrawState.Holstered) {
                    holsteredAt = slideFrames;
                }
                Assert.That(Holder.CanFire, Is.False, "no shots while wall sliding");
            }
            if (slid && !sliding && Holder.IsEquipped) {
                break;
            }
        }
        Stage.ReleaseAll();
        Stage.StopAiming();
        float holsterSeconds = holsteredAt * ScenarioStage.FrameTime;
        Report($"slid {slid} for {slideFrames} frames, quick {quick}, holstered {holsterSeconds:F3} s into the slide, then {Holder.State}; {shots} shots; {track}");
        Assert.That(slid, Is.True, "caught the wall");
        Assert.That(quick, Is.True, "a quick holster");
        Assert.That(holsterSeconds, Is.InRange(0f, Settings.quickHolsterDuration + 0.05f), "holstered at the quick time");
        Assert.That(shots, Is.Zero, "clicks on the wall do not fire, nor fire later");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped), "drawn again after the slide with aim held");
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
    }

    [UnityTest]
    public IEnumerator PushingACrateQuickHolstersThePistolWithOnlyTheFrontHand() {
        yield return Hold(0.6f);
        PistolTrack track = PistolTrack.Follow(View);
        Vector2 offset = new Vector2(6f, 1f);
        CratePusher pusher = Driver.Parts.Pusher;
        Stage.AddCrate((Body.x + 2.5f, 1), (new Vector2(2.4f, 2.4f), 5f));
        yield return Hold(0.3f);
        yield return DrawAt(offset);
        // Aiming, the character walks up to the crate without taking hold of it.
        Stage.Press(Key.D);
        yield return Frames(90, () => Aim(offset));
        Assert.That(pusher.IsPushing, Is.False, "no push while aiming");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Equipped));
        ReleaseAim();
        bool quick = false;
        bool pushedWhileHolstering = false;
        int frames = 0;
        for (; frames < MaxDrawFrames && Holder.State != DrawState.Holstered; frames++) {
            yield return null;
            quick |= Holder.Readout.IsQuick;
            pushedWhileHolstering |= pusher.IsPushing && Holder.State == DrawState.Holstering;
        }
        yield return Hold(0.5f);
        bool stillPushing = pusher.IsPushing;
        Stage.ReleaseAll();
        float seconds = frames * ScenarioStage.FrameTime;
        Report($"holstered {seconds:F3} s after letting go of aim (holster {Settings.holsterDuration}, quick {Settings.quickHolsterDuration}), quick {quick}, pushing while holstering {pushedWhileHolstering}, then pushing {stillPushing}; {track}");
        Assert.That(pushedWhileHolstering, Is.True, "the push took hold during the holster");
        Assert.That(quick, Is.True, "the push quick-holsters");
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered));
        Assert.That(seconds, Is.LessThan(Settings.holsterDuration), "faster than a normal holster");
        Assert.That(stillPushing, Is.True, "the push goes on once the pistol is away");
        Assert.That(track.WorstSwap, Is.LessThan(MaxJump));
        Assert.That(track.WorstInHolster, Is.LessThan(MaxJump));
    }

    // ---- Pictures of the draw and the holster -------------------------------------------------------------------------

    // Sheets of the draw and the holster, frame by frame, facing right and left, to look at: Temp/LocomotionScenarios/
    // pistol-draw-{draw,holster}-{right,left}.png (5 by 3 tiles, every second frame, read left to right then down; a bar
    // along each tile's top shows the cycle's progress, green drawing, orange holstering, white equipped or holstered).
    [UnityTest]
    public IEnumerator SavesSheetsOfTheDrawAndTheHolsterBothWays() {
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        yield return Hold(1f);
        yield return Sheet(folder, "right", new Vector2(6f, 1f));
        Stage.Press(Key.A);
        yield return Hold(0.4f);
        Stage.Release(Key.A);
        yield return Hold(1.2f);
        Assert.That(Driver.transform.localScale.x, Is.LessThan(0f), "faces left");
        yield return Sheet(folder, "left", new Vector2(-6f, 1f));
        Assert.That(File.Exists(Path.Combine(folder, "pistol-draw-holster-left.png")), Is.True);
    }

    private const int SheetColumns = 5;
    private const int SheetRows = 3;
    private const int Tile = 480;
    private const int FramesPerTile = 2;

    private IEnumerator Sheet(string folder, string side, Vector2 offset) {
        Vector2 center = Body + new Vector2(0.5f * Mathf.Sign(offset.x), 0.3f);
        List<Texture2D> tiles = new List<Texture2D>();
        for (int frame = 0; tiles.Count < SheetColumns * SheetRows; frame++) {
            Aim(offset);
            yield return null;
            if (frame % FramesPerTile == 0) {
                tiles.Add(TileOf(center));
            }
        }
        Save(tiles, Path.Combine(folder, $"pistol-draw-draw-{side}.png"));
        yield return Frames(30, () => Aim(offset));
        Assert.That(Holder.IsEquipped, Is.True, "drawn");
        tiles.Clear();
        ReleaseAim();
        for (int frame = 0; tiles.Count < SheetColumns * SheetRows; frame++) {
            yield return null;
            if (frame % FramesPerTile == 0) {
                tiles.Add(TileOf(center));
            }
        }
        yield return Frames(30);
        Assert.That(Holder.State, Is.EqualTo(DrawState.Holstered), "holstered after the sheet");
        Save(tiles, Path.Combine(folder, $"pistol-draw-holster-{side}.png"));
    }

    // The middle of a capture around the character, with the cycle's progress bar along its top.
    private Texture2D TileOf(Vector2 center) {
        Texture2D picture = PistolScenarioTests.Capture(center, 2.4f);
        Texture2D tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
        tile.SetPixels(picture.GetPixels((picture.width - Tile) / 2, (picture.height - Tile) / 2, Tile, Tile));
        Object.Destroy(picture);
        float progress = Holder.State == DrawState.Equipped ? 1f : Holder.Cycle.Progress;
        Color colour = Holder.State == DrawState.Drawing ? Color.green : Holder.State == DrawState.Holstering ? new Color(1f, 0.55f, 0f) : Color.white;
        int bar = Mathf.RoundToInt(progress * Tile);
        for (int y = Tile - 10; y < Tile; y++) {
            for (int x = 0; x < bar; x++) {
                tile.SetPixel(x, y, colour);
            }
        }
        return tile;
    }

    private static void Save(List<Texture2D> tiles, string path) {
        Texture2D sheet = new Texture2D(Tile * SheetColumns, Tile * SheetRows, TextureFormat.RGB24, false);
        for (int index = 0; index < tiles.Count; index++) {
            int column = index % SheetColumns;
            int row = SheetRows - 1 - index / SheetColumns;
            sheet.SetPixels(column * Tile, row * Tile, Tile, Tile, tiles[index].GetPixels());
            Object.Destroy(tiles[index]);
        }
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.Destroy(sheet);
    }
}
