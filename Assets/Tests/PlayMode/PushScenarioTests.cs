using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Pushing crates on the real character, with crates built for each test: walking into a light crate pushes it along with
// the body kept at its face, a very heavy crate stops the character dead without jitter, walking away lets go and the pose
// blends out, a crate is a platform to jump onto, and a bullet still knocks one. Also writes a picture of the push
// (Temp/LocomotionScenarios/push-pose.png) to look at.
public class PushScenarioTests : ScenarioFixture {
    private static readonly Vector2 CrateSize = new Vector2(2.4f, 2.4f);
    // The hull's half width: the body's side is this far ahead of its pivot.
    private const float HullHalfWidth = 0.4f;

    private Vector2 Body => Stage.Stride.transform.position;
    private CratePusher Pusher => Stage.Character.Parts.Pusher;
    private PushPose Pose => Stage.Character.Parts.PushPose;
    private PushSettings Push => Stage.Character.Parts.Push;

    // How far the right side of the hull is from the crate's left face (negative: inside the crate).
    private static float GapTo(Rigidbody2D crate, float bodyX) {
        float face = crate.position.x - CrateSize.x / 2f;
        return face - (bodyX + HullHalfWidth);
    }

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator WalkingIntoALightCratePushesItAlongWithTheBodyAtItsFace() {
        int started = 0;
        Stage.Character.Events.PushStarted += _ => started++;
        yield return Hold(0.5f);
        Rigidbody2D crate = Stage.AddCrate((Body.x + 2.5f, 1), (CrateSize, 5f));
        ShowCrate(crate);
        float crateStart = crate.position.x;
        yield return Hold(0.3f);

        Stage.Press(Key.D);
        yield return WaitUntil(() => Pusher.IsPushing, 3f);
        Assert.That(Pusher.IsPushing, Is.True, "took hold of the crate");
        Assert.That(started, Is.EqualTo(1), "PushStarted fired once");

        float deepest = float.PositiveInfinity;
        float widest = float.NegativeInfinity;
        float fastestBody = 0f;
        (float min, float max) gaitSpeed = (float.PositiveInfinity, float.NegativeInfinity);
        float deepestKnee = float.NegativeInfinity;
        bool heldOn = true;
        float previousX = Body.x;
        for (float elapsed = 0f; elapsed < 3f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            float gap = GapTo(crate, Body.x);
            deepest = Mathf.Min(deepest, gap);
            if (elapsed > 1f) {
                widest = Mathf.Max(widest, gap);
                fastestBody = Mathf.Max(fastestBody, (Body.x - previousX) / ScenarioStage.FrameTime);
            }
            deepestKnee = Mathf.Max(deepestKnee, Mathf.Max(KneeX(Stage.Stride.FrontLeg), KneeX(Stage.Stride.BackLeg)) - (crate.position.x - CrateSize.x / 2f));
            if (elapsed > 1.5f) {
                gaitSpeed = (Mathf.Min(gaitSpeed.min, Stage.Stride.GroundSpeed), Mathf.Max(gaitSpeed.max, Stage.Stride.GroundSpeed));
            }
            previousX = Body.x;
            heldOn &= Pusher.IsPushing;
        }
        float handGap = Mathf.Abs(Stage.Character.Parts.Pusher.VisibleFace.faceX - FrontHandX());
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "LocomotionScenarios");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "push-pose.png"), Capture(new Vector2(Body.x + 1f, ScenarioStage.GroundTop + 2.4f), 3.2f).EncodeToPNG());
        Report($"crate moved {crate.position.x - crateStart:F2}, crate speed {crate.linearVelocity.x:F2}, gap {deepest:F3}..{widest:F3}, " +
               $"fastest body {fastestBody:F2} u/s, gait speed {gaitSpeed.min:F2}..{gaitSpeed.max:F2}, knee at most {deepestKnee:F3} into the crate, pose {Pose.Weight:F2}, front hand {handGap:F3} off the face, started {started}");
        Assert.That(heldOn, Is.True, "never let go while walking into it");
        Assert.That(started, Is.EqualTo(1), "no flicker in and out of contact");
        Assert.That(crate.position.x - crateStart, Is.GreaterThan(3f), "the crate moved along");
        Assert.That(crate.linearVelocity.x, Is.GreaterThan(Push.pushSpeed * 0.6f), "it is still being pushed");
        Assert.That(deepest, Is.GreaterThan(-0.01f), "the body never enters the crate");
        Assert.That(widest, Is.LessThan(PushRules.ReleaseDistance(Push) + 0.35f), "the body keeps up with the crate");
        Assert.That(fastestBody, Is.LessThan(Push.pushSpeed + 1f), "pushing walks slower than a walk");
        Assert.That(gaitSpeed.min, Is.GreaterThan(Push.pushSpeed * 0.6f), "the gait never stalls against the crate");
        Assert.That(gaitSpeed.max - gaitSpeed.min, Is.LessThan(0.5f), "and keeps an even pace");
        Assert.That(Pose.Weight, Is.GreaterThan(0.95f), "the push pose is in");
        Assert.That(handGap, Is.LessThan(0.25f), "the hand is on the crate's face");
    }

    [UnityTest]
    public IEnumerator AVeryHeavyCrateBarelyMovesAndTheCharacterStopsAtItWithoutJitter() {
        yield return Hold(0.5f);
        Rigidbody2D crate = Stage.AddCrate((Body.x + 2.5f, 1), (CrateSize, 1000f));
        yield return Hold(0.3f);
        float crateStart = crate.position.x;

        Stage.Press(Key.D);
        yield return WaitUntil(() => Pusher.IsPushing, 3f);
        Assert.That(Pusher.IsPushing, Is.True, "took hold of the crate");
        yield return Hold(1.5f);

        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        float deepest = float.PositiveInfinity;
        bool heldOn = true;
        for (float elapsed = 0f; elapsed < 1f; elapsed += ScenarioStage.FrameTime) {
            yield return null;
            lowest = Mathf.Min(lowest, Body.x);
            highest = Mathf.Max(highest, Body.x);
            deepest = Mathf.Min(deepest, GapTo(crate, Body.x));
            heldOn &= Pusher.IsPushing;
        }
        Report($"crate moved {crate.position.x - crateStart:F4}, body x range {highest - lowest:F4}, gap {deepest:F3}, last force {Pusher.LastForce:F0} N");
        Assert.That(heldOn, Is.True, "keeps pushing");
        Assert.That(Pusher.LastForce, Is.EqualTo(Push.maxForce).Within(0.01f), "pushes as hard as it can");
        Assert.That(crate.position.x - crateStart, Is.LessThan(0.05f), "the crate barely moves");
        Assert.That(highest - lowest, Is.LessThan(0.02f), "the body stands still at the crate");
        Assert.That(deepest, Is.GreaterThan(-0.01f), "and never enters it");
    }

    [UnityTest]
    public IEnumerator WalkingAwayLetsGoAndThePoseBlendsOut() {
        int stopped = 0;
        Stage.Character.Events.PushStopped += () => stopped++;
        yield return Hold(0.5f);
        Rigidbody2D crate = Stage.AddCrate((Body.x + 2.5f, 1), (CrateSize, 5f));
        yield return Hold(0.3f);

        Stage.Press(Key.D);
        yield return WaitUntil(() => Pusher.IsPushing, 3f);
        yield return Hold(1f);
        Assert.That(Pusher.IsPushing, Is.True);
        Assert.That(Pose.Weight, Is.GreaterThan(0.9f));
        Assert.That(stopped, Is.Zero);

        Stage.Release(Key.D);
        Stage.Press(Key.A);
        yield return null;
        yield return null;
        Assert.That(Pusher.IsPushing, Is.False, "let go at once");
        Assert.That(stopped, Is.EqualTo(1), "PushStopped fired");
        float weightSoonAfter = Pose.Weight;
        yield return Hold(Push.blendTime * 2.5f);
        float weightLater = Pose.Weight;
        yield return Hold(1f);

        Report($"pose {weightSoonAfter:F2} just after letting go, {weightLater:F3} after {Push.blendTime * 2.5f:F2} s; crate speed {crate.linearVelocity.x:F2}");
        Assert.That(weightSoonAfter, Is.GreaterThan(0.5f), "the pose blends out rather than snapping");
        Assert.That(weightLater, Is.LessThan(0.1f), "and is gone soon after");
        Assert.That(Mathf.Abs(crate.linearVelocity.x), Is.LessThan(0.05f), "the crate slid to a stop");
        Assert.That(Body.x, Is.LessThan(crate.position.x - CrateSize.x / 2f - 2f), "the character walked away");
        Assert.That(stopped, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TheCharacterCanJumpOntoACrateAndStandOnIt() {
        Vector2 size = new Vector2(2.4f, 1.8f);
        yield return Hold(0.5f);
        Rigidbody2D crate = Stage.AddCrate((Body.x + 3f, 1), (size, 1000f));
        float top = ScenarioStage.GroundTop + size.y;
        yield return Hold(0.3f);
        float crateStart = crate.position.x;

        Stage.Press(Key.D);
        yield return Hold(0.3f);
        Stage.Press(Key.Space);
        yield return Hold(0.3f);
        Stage.Release(Key.Space);
        yield return WaitUntil(() => Stage.Stride.IsDrivingGait && Mathf.Abs(Stage.Stride.StanceHeight - top) < 0.1f, 4f);
        Stage.Release(Key.D);
        yield return Hold(1f);

        Report($"stance {Stage.Stride.StanceHeight:F3} (crate top {top:F3}), state {Stage.Character.CurrentState}, crate moved {crate.position.x - crateStart:F3}");
        Assert.That(Stage.Stride.IsDrivingGait, Is.True, "standing");
        Assert.That(Stage.Stride.StanceHeight, Is.EqualTo(top).Within(0.06f), "on top of the crate");
        Assert.That(Mathf.Abs(Body.x - crate.position.x), Is.LessThan(size.x / 2f + 0.3f), "over the crate");
        Assert.That(Mathf.Abs(crate.position.x - crateStart), Is.LessThan(0.1f), "the crate stayed put");
    }

    [UnityTest]
    public IEnumerator ABulletStillKnocksACrate() {
        yield return Hold(0.5f);
        Rigidbody2D crate = Stage.AddCrate((Body.x + 4f, 1), (CrateSize, 1f));
        PushableCrate pushable = crate.GetComponent<PushableCrate>();
        yield return Hold(0.3f);
        Vector2 aim = crate.position;

        float fastest = 0f;
        for (int frame = 0; frame < 60; frame++) {
            Stage.AimAndFire(aim, frame >= 30 && frame < 33);
            yield return null;
            fastest = Mathf.Max(fastest, crate.linearVelocity.x);
        }
        Stage.StopAiming();

        Report($"hits {pushable.HitCount}, fastest {fastest:F2} u/s");
        Assert.That(pushable.HitCount, Is.EqualTo(1), "the crate took the bullet");
        Assert.That(fastest, Is.GreaterThan(1f), "the impulse knocked it away");
        Assert.That(Pusher.IsPushing, Is.False, "aiming pushes nothing");
    }

    // A plain square sprite, so the crate shows in the picture.
    private static void ShowCrate(Rigidbody2D crate) {
        Texture2D texture = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int index = 0; index < pixels.Length; index++) {
            pixels[index] = new Color(0.55f, 0.4f, 0.25f, 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        crate.GetComponent<SpriteRenderer>().sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f / CrateSize.x);
    }

    private static Texture2D Capture(Vector2 center, float orthographicSize) {
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

    // The leg's knee joint: the bone after the hip in its IK chain.
    private static float KneeX(LegStepper leg) {
        Transform hip = (Transform)typeof(LegStepper).GetField("hip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(leg);
        return hip.GetChild(0).position.x;
    }

    private float FrontHandX() {
        ArmLimb arm = (ArmLimb)typeof(CharacterDriver).GetField("frontArm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Stage.Character);
        return arm.HandPosition.x;
    }
}
