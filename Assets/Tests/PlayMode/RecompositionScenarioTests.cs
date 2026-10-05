using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Edge cases around the character's collaborators: disabling and re-enabling the driver (and the rebuild a script reload
// in play mode causes) leaves exactly one tracer, effects and audio root and a character that still fires; a casing taken
// over from the ground does not tinkle at its old place; aiming with no camera does not throw.
public class RecompositionScenarioTests : ScenarioFixture {
    private const int FramesBeforeFirstClick = 30;

    private CharacterDriver Driver => Stage.Character;

    private static int RootsNamed(string name) {
        int count = 0;
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            count += candidate.name == name && candidate.transform.parent == null ? 1 : 0;
        }
        return count;
    }

    private void AssertOneOfEachRoot(string when) {
        Assert.That(RootsNamed("Tracers"), Is.EqualTo(1), $"one Tracers root {when}");
        Assert.That(RootsNamed("Effects"), Is.EqualTo(1), $"one Effects root {when}");
        Assert.That(RootsNamed("Audio"), Is.EqualTo(1), $"one Audio root {when}");
    }

    // What a script reload in play mode does: the collaborators are gone, their scene objects are not.
    private void ForgetCollaborators() {
        foreach (string field in new[] { "input", "weaponController", "characterEffects", "characterAudio" }) {
            typeof(CharacterDriver).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Driver, null);
        }
    }

    private IEnumerator FireOneShot() {
        int fired = 0;
        Driver.Events.Fired += _ => fired++;
        Vector2 aim = (Vector2)Stage.Stride.transform.position + new Vector2(3f, 1.5f);
        for (int frame = 0; frame < FramesBeforeFirstClick + 3; frame++) {
            Stage.AimAtScreen(ScenarioStage.ScreenPointOf(aim), frame >= FramesBeforeFirstClick);
            yield return null;
        }
        Stage.StopAiming();
        Assert.That(fired, Is.EqualTo(1), "the character still fires");
    }

    [UnityTest]
    public IEnumerator DisablingAndEnablingTheDriverKeepsOneOfEachRootAndTheCharacterFires() {
        yield return Hold(0.5f);
        AssertOneOfEachRoot("to begin with");

        Driver.enabled = false;
        yield return null;
        Driver.enabled = true;
        yield return Hold(0.3f);

        AssertOneOfEachRoot("after disabling and enabling");
        yield return FireOneShot();
    }

    [UnityTest]
    public IEnumerator RecomposingAfterAScriptReloadDestroysTheOrphanedRoots() {
        yield return Hold(0.5f);
        Driver.enabled = false;
        ForgetCollaborators();
        yield return null;
        Driver.enabled = true;
        yield return Hold(0.3f);

        AssertOneOfEachRoot("after a reload recomposed the driver");
        Assert.That(Driver.Effects, Is.Not.Null);
        Assert.That(Driver.Audio, Is.Not.Null);
        yield return FireOneShot();
    }

    [UnityTest]
    public IEnumerator ASuppliedPointerSourceBeforeTheDriverIsEnabledIsKeptAndDoesNotThrow() {
        yield return Hold(0.3f);
        Driver.enabled = false;
        ForgetCollaborators();
        Vector2 screen = new Vector2(123f, 45f);

        Assert.DoesNotThrow(() => Driver.ScenarioPointerSource = () => screen);
        Driver.enabled = true;
        yield return null;
        yield return null;

        Assert.That(Driver.LatestCommands.PointerScreenPosition, Is.EqualTo(screen), "the source was applied when the input reader was built");
    }

    [UnityTest]
    public IEnumerator ACasingTakenOverFromTheGroundDoesNotTinkleAtItsOldPlace() {
        yield return Hold(0.5f);
        CasingPool pool = Driver.Effects.Casings;
        Vector2 spot = new Vector2(Stage.Stride.transform.position.x + 2f, ScenarioStage.GroundTop + 0.3f);
        for (int index = 0; index < CasingPool.Size; index++) {
            pool.Eject(((spot + Vector2.right * (index * 0.4f)), Vector2.zero, 0f), (60f, 1f));
        }
        yield return Hold(2f);

        int contacts = 0;
        pool.FirstContact += _ => contacts++;
        pool.Eject((spot + Vector2.up * 4f, Vector2.zero, 0f), (60f, 1f));
        pool.Tick(0f);
        Assert.That(contacts, Is.Zero, "the old contact on the ground is ignored until physics has stepped");

        yield return Hold(2f);
        Assert.That(contacts, Is.EqualTo(1), "the real landing is heard");
    }

    [Test]
    public void ContactsCountOnlyOnceFixedTimeHasAdvancedPastTheThrow() {
        Assert.That(CasingPool.PhysicsHasSteppedSince((1f, 1f)), Is.False);
        Assert.That(CasingPool.PhysicsHasSteppedSince((1f, 1.02f)), Is.True);
    }

    [UnityTest]
    public IEnumerator AimingWithNoCameraDoesNotThrow() {
        yield return Hold(0.5f);
        Camera view = Camera.main;
        view.enabled = false;
        Assert.That(Camera.main, Is.Null);

        for (int frame = 0; frame < 20; frame++) {
            Stage.AimAtScreen(new Vector2(200f, 200f), false);
            yield return null;
        }
        Assert.That(Driver.LatestCommands.IsAiming, Is.True, "it was aiming the whole time");
        Stage.StopAiming();
        view.enabled = true;
    }
}
