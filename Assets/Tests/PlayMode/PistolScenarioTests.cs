using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The pistol and holster on the real character: holstered at the hip at rest, in the hand pointing exactly along the
// aim while aiming (both ways), shooting and flashing from its muzzle, casings leaving its port, mirrored with the
// facing, not jumping when it changes parent, and built once however often the character is recomposed. Also writes
// pictures (Temp/LocomotionScenarios/pistol-*.png) to look at.
public class PistolScenarioTests : ScenarioFixture {
    private const int FramesBeforeFirstClick = 30;

    private CharacterDriver Driver => Stage.Character;
    private PistolView View => Driver.Parts.PistolView;
    private Vector2 Body => Stage.Stride.transform.position;

    private ArmLimb FrontArm => (ArmLimb)typeof(CharacterDriver).GetField("frontArm", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Driver);

    private static int CountNamed(string name) {
        int count = 0;
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            count += candidate.name == name ? 1 : 0;
        }
        return count;
    }

    private IEnumerator AimAt(Vector2 offset, int frames = 40) {
        for (int frame = 0; frame < frames; frame++) {
            Stage.AimAndFire(Body + offset, false);
            yield return null;
        }
    }

    private static Vector2 AngleToDirection(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    [UnityTest]
    public IEnumerator AtRestThePistolIsInTheHolsterAtTheHip() {
        yield return Hold(1f);
        PistolView view = View;
        Assert.That(view.IsInHand, Is.False);
        Assert.That(view.Parent, Is.SameAs(view.Holster));
        Assert.That(Vector2.Distance(view.Pistol.position, view.Holster.position), Is.LessThan(0.01f), "in the holster");
        Vector2 offset = (Vector2)view.Holster.position - Body;
        Report($"holster {offset} from the pelvis, pistol sorting {view.Pistol.GetComponent<SpriteRenderer>().sortingOrder}");
        Assert.That(offset.magnitude, Is.LessThan(0.7f), "on the hip, near the pelvis");
        Assert.That(offset.y, Is.LessThan(0.1f), "at hip height or below the pelvis");
        Assert.That(view.Pistol.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(PistolView.HolsteredOrder));
        Assert.That(view.Holster.GetComponentInChildren<SpriteRenderer>().sortingOrder, Is.EqualTo(PistolView.HolsteredOrder));
        Assert.That(view.Pistol.position.z, Is.LessThan(0f), "nearer the camera than the chest sprite");
    }

    [UnityTest]
    public IEnumerator AimingDrawsThePistolIntoTheHandPointingAlongTheAimToTheRightAndTheLeft() {
        yield return Hold(0.6f);
        foreach (float degrees in new[] { 20f, 160f, -30f, 200f }) {
            Vector2 offset = AngleToDirection(degrees) * 6f;
            yield return AimAt(offset);
            PistolView view = View;
            Assert.That(view.IsInHand, Is.True, $"drawn at {degrees}");
            Assert.That(view.Parent, Is.SameAs(view.WeaponPivot));
            float error = Vector2.Angle(view.BarrelDirection, view.AimDirection);
            Vector2 toPointer = Body + offset - (Vector2)FrontArm.ShoulderPosition;
            float aimError = Vector2.Angle(view.AimDirection, toPointer);
            Report($"{degrees}: barrel {Vector2.SignedAngle(Vector2.right, view.BarrelDirection):F1}, aim error {error:F2}, aim vs pointer {aimError:F2}");
            Assert.That(error, Is.LessThan(1f), $"the barrel points along the aim at {degrees}");
            Assert.That(aimError, Is.LessThan(2f), $"the aim is at the pointer at {degrees}");
            Assert.That(view.Pistol.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(PistolView.DrawnOrder));
            Assert.That(view.Pistol.TransformVector(Vector3.up).y, Is.GreaterThan(0f), "the slide stays on top, whichever way it faces");
        }
        Stage.StopAiming();
        yield return Hold(0.6f);
        Assert.That(View.Parent, Is.SameAs(View.Holster), "holstered again");
        Assert.That(Vector2.Distance(View.Pistol.position, View.Holster.position), Is.LessThan(0.01f), "back at the hip");
    }

    [UnityTest]
    public IEnumerator TheShotLeavesTheMuzzleAlongTheBarrelAndTheMuzzleIsInFrontOfTheHandOnBothSides() {
        yield return Hold(0.6f);
        foreach (int side in new[] { 1, -1 }) {
            Vector2 offset = new Vector2(side * 6f, 1.5f);
            yield return AimAt(offset);
            (Vector2 muzzle, Vector2 direction)? shot = null;
            float worst = 0f;
            System.Action<(Vector2 muzzle, Vector2 direction)> record = fired => {
                shot = fired;
                worst = Vector2.Distance(fired.muzzle, View.Muzzle.position);
            };
            Driver.Events.Fired += record;
            for (int frame = 0; frame < 4; frame++) {
                Stage.AimAndFire(Body + offset, frame < 2);
                yield return null;
            }
            Driver.Events.Fired -= record;
            Assert.That(shot.HasValue, Is.True, "a shot was fired");
            Report($"side {side}: muzzle off the transform by {worst:F5}");
            Assert.That(worst, Is.LessThan(0.001f), "the Fired muzzle is the Muzzle transform");
            Assert.That(Vector2.Angle(shot.Value.direction, new Vector2(side, 0.25f)), Is.LessThan(25f), "toward the aim");
            Vector2 hand = FrontArm.HandPosition;
            Assert.That((View.Muzzle.position.x - hand.x) * side, Is.GreaterThan(0.2f), $"the muzzle is in front of the hand facing {side}");
            Stage.StopAiming();
            yield return Hold(0.4f);
            Stage.Character.ScenarioPointerSource = null;
        }
    }

    // The pistol is posed in LateUpdate, after the arms; a shot taken in Update would leave from last frame's barrel, off
    // by the frame's run travel. The shot is taken after the pose, so it leaves from the barrel as rendered that frame.
    [UnityTest]
    public IEnumerator WhileRunningTheShotLeavesTheBarrelAsRenderedThatFrame() {
        yield return Hold(0.6f);
        Stage.Press(Key.D, Key.LeftShift);
        yield return Hold(1f);
        for (int frame = 0; frame < 40; frame++) {
            Stage.AimAndFire(Body + new Vector2(6f, 1.5f), false);
            yield return null;
        }
        Assert.That(View.IsInHand, Is.True, "drawn while running");
        Assert.That(Mathf.Abs(Stage.Stride.GroundSpeed), Is.GreaterThan(Stage.Stride.Run.speed * 0.8f), "running");

        Vector2? fired = null;
        System.Action<(Vector2 muzzle, Vector2 direction)> record = shot => fired = shot.muzzle;
        Driver.Events.Fired += record;
        int shots = 0;
        float worst = 0f;
        float travel = 0f;
        float previousX = Body.x;
        for (int frame = 0; frame < 90; frame++) {
            Stage.AimAndFire(Body + new Vector2(6f, 1.5f), frame % 2 == 0);
            fired = null;
            yield return null;
            // The end of the frame: after every LateUpdate (the arms, the leg IK, the pistol's pose).
            yield return new WaitForEndOfFrame();
            travel = Mathf.Abs(Body.x - previousX);
            previousX = Body.x;
            if (fired.HasValue) {
                shots++;
                // The pose at the end of the frame the shot was taken in.
                worst = Mathf.Max(worst, Vector2.Distance(fired.Value, View.Muzzle.position));
            }
        }
        Driver.Events.Fired -= record;
        Stage.ReleaseAll();
        Report($"{shots} shots while running ({travel:F3} u per frame): worst Fired muzzle error {worst:F5}");
        Assert.That(shots, Is.GreaterThanOrEqualTo(3), "fired several times");
        Assert.That(worst, Is.LessThan(0.002f), "the Fired muzzle is where the barrel is rendered that frame");
    }

    [UnityTest]
    public IEnumerator FlippingTheFacingMirrorsThePistolAndTheHolster() {
        yield return Hold(0.6f);
        float holsterRight = ((Vector2)View.Holster.position - Body).x;
        yield return AimAt(new Vector2(-6f, 0.5f), 30);
        Vector2 barrelLeft = View.BarrelDirection;
        Assert.That(barrelLeft.x, Is.LessThan(-0.9f), "pointing left");
        Stage.StopAiming();
        yield return Hold(0.6f);
        float holsterLeft = ((Vector2)View.Holster.position - Body).x;
        Report($"holster x from the pelvis: facing right {holsterRight:F3}, facing left {holsterLeft:F3}");
        Assert.That(Driver.transform.localScale.x, Is.LessThan(0f), "the rig faces left");
        Assert.That(holsterLeft, Is.EqualTo(-holsterRight).Within(0.15f), "the holster is on the other side of the pelvis");
    }

    [UnityTest]
    public IEnumerator ChangingParentNeverMovesThePistolInItsNewParentsSpaceThatFrame() {
        yield return Hold(0.6f);
        // Measured at the end of each frame, after the arms, the leg IK and the pistol's pose.
        PistolTrack track = PistolTrack.Follow(View);
        for (int frame = 0; frame < 140; frame++) {
            bool aiming = frame >= 10 && frame < 70;
            if (aiming) {
                Stage.AimAndFire(Body + new Vector2(6f, 2f), false);
            } else {
                Stage.StopAiming();
            }
            yield return null;
        }
        Report($"{track}; the fastest frame in the holster, carried by the thigh, {track.FastestInHolster:F3}");
        Assert.That(track.Swaps, Is.EqualTo(2), "drawn once and holstered once");
        Assert.That(track.WorstSwap, Is.LessThan(0.03f), "no jump at a parent change");
        Assert.That(track.WorstInHolster, Is.LessThan(0.03f), "no jump within the holster");
    }

    [UnityTest]
    public IEnumerator ACasingLeavesTheEjectionPort() {
        yield return Hold(0.6f);
        yield return AimAt(new Vector2(6f, 1f));
        Vector2 port = Vector2.zero;
        Driver.Events.Fired += _ => port = View.EjectionPort.position;
        for (int frame = 0; frame < 3; frame++) {
            Stage.AimAndFire(Body + new Vector2(6f, 1f), frame < 2);
            yield return null;
        }
        CasingPool casings = Driver.Effects.Casings;
        Assert.That(casings.ActiveCount, Is.EqualTo(1));
        Vector2 casing = Vector2.zero;
        for (int index = 0; index < CasingPool.Size; index++) {
            if (casings.IsActive(index)) {
                casing = casings.BodyOf(index).position;
            }
        }
        Report($"port {port}, casing {casing}");
        Assert.That(Vector2.Distance(casing, port), Is.LessThan(0.35f), "the casing starts at the port, not at the muzzle");
        Assert.That(Vector2.Distance(casing, View.Muzzle.position), Is.GreaterThan(0.25f));
        Stage.StopAiming();
    }

    [UnityTest]
    public IEnumerator RecomposingLeavesExactlyOnePistolAndHolsterAndTheMarksFollow() {
        yield return Hold(0.5f);
        Assert.That(CountNamed("Pistol"), Is.EqualTo(1));
        Assert.That(CountNamed("Holster"), Is.EqualTo(1));
        Assert.That(CountNamed("WeaponPivot"), Is.EqualTo(1));
        int marked = OwnedRoot.CountOwnedBy(Driver.transform);
        Assert.That(marked, Is.GreaterThanOrEqualTo(6), "the roots and the three objects under the bones are marked");
        Assert.That(View.Holster.parent, Is.SameAs(Stage.Stride.FrontLeg.Hip), "the holster hangs from the front thigh bone");

        Driver.enabled = false;
        yield return null;
        Driver.enabled = true;
        yield return Hold(0.3f);
        Assert.That(CountNamed("Pistol"), Is.EqualTo(1), "after disabling and enabling");
        Assert.That(CountNamed("Holster"), Is.EqualTo(1));

        // What a script reload does: the collaborators are gone, their scene objects are not.
        PistolView before = View;
        Driver.enabled = false;
        foreach (string field in new[] { "input", "weaponController", "characterEffects", "characterAudio", "pistolView" }) {
            typeof(CharacterDriver).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Driver, null);
        }
        yield return null;
        Driver.enabled = true;
        yield return Hold(0.3f);
        Assert.That(View, Is.Not.SameAs(before), "a new view was composed");
        Assert.That(CountNamed("Pistol"), Is.EqualTo(1), "after a reload recomposed the driver");
        Assert.That(CountNamed("Holster"), Is.EqualTo(1));
        Assert.That(CountNamed("WeaponPivot"), Is.EqualTo(1));
        Assert.That(OwnedRoot.CountOwnedBy(Driver.transform), Is.EqualTo(marked), "the same number of marked objects");

        int fired = 0;
        Driver.Events.Fired += _ => fired++;
        for (int frame = 0; frame < FramesBeforeFirstClick + 3; frame++) {
            Stage.AimAndFire(Body + new Vector2(5f, 1f), frame >= FramesBeforeFirstClick);
            yield return null;
        }
        Stage.StopAiming();
        Assert.That(fired, Is.EqualTo(1), "still fires, from the new pistol");
    }

    [UnityTest]
    public IEnumerator SavesPicturesOfTheHolsteredAndTheDrawnPistol() {
        yield return Hold(1.2f);
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        Vector2 center = Body + new Vector2(0.4f, 0.4f);
        Write(folder, "pistol-holstered.png", center, 2.2f);

        yield return AimAt(new Vector2(6f, 1.5f), 45);
        Write(folder, "pistol-aim-right.png", center + new Vector2(0.8f, 0.8f), 2.2f);
        Stage.StopAiming();
        yield return Hold(0.5f);
        yield return AimAt(new Vector2(-6f, 1.5f), 45);
        Write(folder, "pistol-aim-left.png", center + new Vector2(-0.8f, 0.8f), 2.2f);
        Stage.StopAiming();
        yield return Hold(0.6f);
        Write(folder, "pistol-holstered-left.png", center, 2.2f);
        Assert.That(File.Exists(Path.Combine(folder, "pistol-aim-left.png")), Is.True);
    }

    internal static void Write(string folder, string name, Vector2 center, float size) {
        Texture2D picture = Capture(center, size);
        File.WriteAllBytes(Path.Combine(folder, name), picture.EncodeToPNG());
        Object.Destroy(picture);
    }

    internal static Texture2D Capture(Vector2 center, float orthographicSize) {
        GameObject host = new GameObject("pictureCamera");
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = new Vector3(center.x, center.y, -10f);
        camera.orthographicSize = orthographicSize;
        camera.aspect = 16f / 9f;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(host);
        return picture;
    }
}
