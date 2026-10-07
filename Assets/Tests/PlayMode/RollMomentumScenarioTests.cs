using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// The roll carries the momentum it starts with, on the real character: from standing, a walk and a full run the body's
// speed has no step at the entry (only the throw's rise), a running roll peaks higher and travels further than a walking
// one, the walk is handed exactly the roll's final speed, and with Shift + D the run picks up from it. Also writes
// pictures of each roll (Temp/LocomotionScenarios/roll-idle.png, roll-walk.png, roll-run.png) to look at.
public class RollMomentumScenarioTests : ScenarioFixture {
    // Frames since the roll began at which the pictures are taken: mid-dive, the hands meeting the ground, the roll over,
    // inverted, getting up, standing.
    private static readonly int[] PictureFrames = { 5, 13, 22, 32, 46, 58 };
    private const int PictureWidth = 640;
    private const int PictureHeight = 360;

    private Vector2 Body => Stage.Stride.transform.position;
    private ICharacterState State => Stage.Character.CurrentState;
    private RollState Roll => Stage.Character.Parts.RollState;
    private LocomotionTelemetry Telemetry => Stage.Inspector.ReadTelemetry();

    private struct RollRun {
        public float incoming;
        public float throwAcceleration;
        public float worstEntryStep;
        public float motionStep;
        public float walkSurge;
        public float peak;
        public float distance;
        public float finalSpeed;
        public float handedBack;
        public float carried;
        public int frames;
        public List<float> speeds;
    }

    private IEnumerator WaitUntil(System.Func<bool> condition, float maxSeconds) {
        for (float elapsed = 0f; elapsed < maxSeconds && !condition(); elapsed += ScenarioStage.FrameTime) {
            yield return null;
        }
    }

    // Approaches with the given keys held for the given time, then rolls with them still held. Measures the body's
    // horizontal speed every frame from its motion, the speed the walk hands over (its body velocity, what Walk.Exit
    // writes to the motor), the roll's own speed, and what the walk was handed back.
    private IEnumerator RollFrom((string name, Key[] held, float approach) entry, System.Action<RollRun> result) {
        if (entry.held.Length > 0) {
            Stage.Press(entry.held);
        }
        yield return Hold(entry.approach);

        // The approach's own roughness: the gait surges the pelvis with every step.
        List<float> bodySpeeds = new List<float>();
        float previousX = Body.x;
        float walkVelocity = Stage.Stride.BodyVelocity.x;
        for (int frame = 0; frame < 30; frame++) {
            yield return null;
            bodySpeeds.Add((Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            walkVelocity = Stage.Stride.BodyVelocity.x;
        }
        float walkSurge = 0f;
        for (int index = 1; index < bodySpeeds.Count; index++) {
            walkSurge = Mathf.Max(walkSurge, Mathf.Abs(bodySpeeds[index] - bodySpeeds[index - 1]));
        }

        Stage.Press(Key.LeftAlt);
        for (int frame = 0; frame < 20 && !(State is RollState); frame++) {
            yield return null;
            bodySpeeds.Add((Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            if (State is WalkState) {
                walkVelocity = Stage.Stride.BodyVelocity.x;
            }
        }
        Stage.Release(Key.LeftAlt);
        Assert.That(State, Is.InstanceOf<RollState>(), $"{entry.name}: rolls");
        // The walk handed over this frame before moving the body; the roll carried it instead. The frame before is the
        // walk's last motion.
        int rollStartIndex = bodySpeeds.Count - 1;
        float startX = Body.x;
        RollLaunch launch = Roll.Readout.Launch;

        List<float> rollSpeeds = new List<float>();
        Texture2D sheet = new Texture2D(PictureWidth * 3, PictureHeight * 2, TextureFormat.RGB24, false);
        int frames = 0;
        float lastRollSpeed = 0f;
        List<string> phaseStarts = new List<string>();
        RollMotionPhase lastPhase = RollMotionPhase.Dive;
        while (State is RollState && frames < 180) {
            yield return null;
            frames++;
            if (State is RollState && Roll.Readout.Phase != lastPhase) {
                lastPhase = Roll.Readout.Phase;
                phaseStarts.Add($"{lastPhase} at frame {frames}");
            }
            bodySpeeds.Add((Body.x - previousX) / ScenarioStage.FrameTime);
            previousX = Body.x;
            lastRollSpeed = Roll.Readout.Speed;
            rollSpeeds.Add(lastRollSpeed);
            int picture = System.Array.IndexOf(PictureFrames, frames);
            if (picture >= 0 && State is RollState) {
                Paste(sheet, Capture(Body + new Vector2(0f, -0.4f), 3.2f), picture);
            }
        }
        Assert.That(State, Is.InstanceOf<WalkState>(), $"{entry.name}: back on its feet");
        WriteSheet("roll-" + entry.name, sheet);

        // From the velocity the walk handed over through the throw: the roll's speed, measured from the body's motion.
        float worstEntryStep = Mathf.Abs(bodySpeeds[rollStartIndex] - walkVelocity);
        int throwFrames = Mathf.CeilToInt(Stage.Character.Parts.RollMomentum.throwTime / ScenarioStage.FrameTime);
        for (int index = rollStartIndex + 1; index <= Mathf.Min(bodySpeeds.Count - 1, rollStartIndex + throwFrames + 3); index++) {
            worstEntryStep = Mathf.Max(worstEntryStep, Mathf.Abs(bodySpeeds[index] - bodySpeeds[index - 1]));
        }
        // The same step against the walk's last frame of motion rather than its velocity: half a frame of the gait's
        // own surge apart, so it is held to the larger of the throw and that surge.
        float motionStep = Mathf.Abs(bodySpeeds[rollStartIndex] - bodySpeeds[rollStartIndex - 1]);
        RollReadout readout = Roll.Readout;
        RollRun run = new RollRun {
            incoming = launch.Incoming,
            throwAcceleration = launch.ThrowAcceleration,
            worstEntryStep = worstEntryStep,
            motionStep = motionStep,
            walkSurge = walkSurge,
            peak = rollSpeeds.Max(),
            distance = Body.x - startX,
            finalSpeed = lastRollSpeed,
            handedBack = readout.HandedBack,
            carried = Telemetry.CarriedSpeed,
            frames = frames,
            speeds = rollSpeeds,
        };
        Report($"{entry.name}: u_in {launch.Incoming:F2} (walk velocity {walkVelocity:F2}, its last frame's motion {bodySpeeds[rollStartIndex - 1]:F2}, the roll's first {bodySpeeds[rollStartIndex]:F2}; the approach surges up to {walkSurge:F2} per frame), inherited {launch.Inherited:F2}, throw {launch.Throw:F2} -> {launch.Target:F2}, " +
               $"worst entry step {worstEntryStep:F3} u/s per frame (throw allows {launch.ThrowAcceleration * ScenarioStage.FrameTime:F3}), peak {run.peak:F2}, " +
               $"recovery {readout.RecoverySpeed:F2}, final {run.finalSpeed:F2}, handed {run.handedBack:F2}, carried {run.carried:F2}, distance {run.distance:F2} in {frames} frames");
        Report($"{entry.name} phases: " + string.Join(", ", phaseStarts));
        Report($"{entry.name} speeds: " + string.Join(" ", rollSpeeds.Select(speed => speed.ToString("F2"))));
        result(run);
    }

    [UnityTest]
    public IEnumerator TheRollCarriesTheIncomingSpeedThrowsHarderFromARunAndHandsItsSpeedOn() {
        RollRun idle = default;
        yield return RollFrom(("idle", new Key[0], 1f), r => idle = r);
        yield return Hold(1f);
        RollRun walk = default;
        yield return RollFrom(("walk", new[] { Key.D }, 1.5f), r => walk = r);
        Stage.ReleaseAll();
        yield return Hold(1.5f);
        RollRun run = default;
        yield return RollFrom(("run", new[] { Key.D, Key.LeftShift }, 2f), r => run = r);

        // Shift + D still held: the run picks up from the speed the roll handed over, with no drop.
        float lowest = float.PositiveInfinity;
        for (int frame = 0; frame < 90; frame++) {
            yield return null;
            lowest = Mathf.Min(lowest, Mathf.Abs(Stage.Stride.GroundSpeed));
        }
        float runningOn = Mathf.Abs(Stage.Stride.GroundSpeed);
        Report($"run on: lowest {lowest:F2} after the hand-over of {run.carried:F2}, then {runningOn:F2} u/s (run {Stage.Stride.Run.speed:F2})");
        Stage.ReleaseAll();

        foreach ((string name, RollRun roll) entry in new[] { ("idle", idle), ("walk", walk), ("run", run) }) {
            RollRun roll = entry.roll;
            float throwStep = roll.throwAcceleration * ScenarioStage.FrameTime;
            Assert.That(roll.worstEntryStep, Is.LessThanOrEqualTo(throwStep + 0.05f), $"{entry.name}: no speed step at the entry");
            Assert.That(roll.motionStep, Is.LessThanOrEqualTo(Mathf.Max(throwStep, roll.walkSurge) + 0.05f), $"{entry.name}: the entry is no rougher than the approach");
            Assert.That(roll.handedBack, Is.EqualTo(roll.finalSpeed).Within(0.05f), $"{entry.name}: hands back its final speed");
            Assert.That(roll.carried, Is.EqualTo(roll.finalSpeed).Within(0.05f), $"{entry.name}: the walk carries that speed");
        }
        Assert.That(idle.incoming, Is.LessThan(0.2f), "standing still");
        Assert.That(walk.incoming, Is.EqualTo(Stage.Stride.Walk.speed).Within(0.4f), "at a walk");
        Assert.That(run.incoming, Is.GreaterThan(Stage.Stride.Run.speed * 0.75f), "at a run (the body's own speed, which surges with each step)");
        Assert.That(run.peak, Is.GreaterThanOrEqualTo(walk.peak * 1.25f), "a running roll throws harder");
        Assert.That(walk.peak, Is.GreaterThan(idle.peak), "a walking roll more than a standing one");
        Assert.That(idle.peak, Is.GreaterThanOrEqualTo(Stage.Character.Parts.RollMomentum.minRollSpeed - 0.01f), "a standing roll still travels");
        Assert.That(run.distance, Is.GreaterThan(walk.distance), "further from a run");
        Assert.That(walk.distance, Is.GreaterThan(idle.distance), "further from a walk than from standing");
        Assert.That(lowest, Is.GreaterThanOrEqualTo(run.carried - 0.1f), "the run starts from the roll's speed, no drop");
        Assert.That(runningOn, Is.GreaterThan(Stage.Stride.Run.speed * 0.9f), "and runs on");
    }

    private static void Paste(Texture2D sheet, Texture2D picture, int slot) {
        sheet.SetPixels(slot % 3 * PictureWidth, (1 - slot / 3) * PictureHeight, PictureWidth, PictureHeight, picture.GetPixels());
        Object.Destroy(picture);
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
