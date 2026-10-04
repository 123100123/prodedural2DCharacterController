using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Lights every scene that is loaded while the Universal Render Pipeline is in use: adds the lighting rig when the scene
// has none, gives the scene's sprites the lit material (a sprite on the default material ignores 2D lights) and makes the
// level's solid sprites cast shadows for the lights that cast them. It does nothing under any other pipeline, so the game
// plays the same without the setup.
public static class LightingBootstrap {
    private const string LitSpriteShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string UnlitSpriteShader = "Sprites/Default";
    // Debug marker sprites stay unlit so they read the same in any light.
    private const string MarkerSpritePrefix = "icon";

    private static Material litMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() {
        litMaterial = null;
        SceneManager.sceneLoaded -= LightScene;
        SceneManager.sceneLoaded += LightScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LightFirstScene() => LightScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

    public static bool IsUniversalPipelineActive {
        get {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null && pipeline.GetType().Name.Contains("Universal");
        }
    }

    private static void LightScene(Scene scene, LoadSceneMode mode) {
        if (!IsUniversalPipelineActive) {
            return;
        }
        UseLitSprites(scene);
        AddShadowCasters(scene);
        AddLightingRigIfMissing();
    }

    // The level's solid box colliders on sprites (not part of the character) shadow the lights that cast shadows. Their
    // own surfaces still catch the light: they do not shadow themselves. The character has no casters, so a flash at
    // the gun lights it instead of being blocked by it.
    private static void AddShadowCasters(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (BoxCollider2D box in root.GetComponentsInChildren<BoxCollider2D>(true)) {
                bool isLevel = !box.isTrigger && box.GetComponentInParent<CharacterDriver>() == null;
                if (isLevel && box.TryGetComponent(out SpriteRenderer _) && box.GetComponentInChildren<ShadowCaster2D>(true) == null) {
                    ShadowCasterFactory.TryAdd(box);
                }
            }
        }
    }

    private static void AddLightingRigIfMissing() {
        CharacterDriver character = Object.FindAnyObjectByType<CharacterDriver>();
        if (character == null || Object.FindAnyObjectByType<SceneLightingController>() != null) {
            return;
        }
        // Inactive while it is set up, so it wakes with its character.
        GameObject host = new GameObject("SceneLighting");
        host.SetActive(false);
        host.AddComponent<SceneLightingController>().Bind(character);
        host.SetActive(true);
    }

    private static void UseLitSprites(Scene scene) {
        Material lit = LitMaterial();
        if (lit == null) {
            return;
        }
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) {
                if (IsOnTheDefaultMaterial(renderer) && !IsMarker(renderer)) {
                    renderer.sharedMaterial = lit;
                }
            }
        }
    }

    private static bool IsOnTheDefaultMaterial(SpriteRenderer renderer) {
        Material material = renderer.sharedMaterial;
        return material != null && material.shader != null && material.shader.name == UnlitSpriteShader;
    }

    private static bool IsMarker(SpriteRenderer renderer) {
        return renderer.sprite != null && renderer.sprite.name.StartsWith(MarkerSpritePrefix);
    }

    private static Material LitMaterial() {
        if (litMaterial == null) {
            Shader shader = Shader.Find(LitSpriteShader);
            litMaterial = shader != null ? new Material(shader) { name = "Sprite-Lit (runtime)" } : null;
        }
        return litMaterial;
    }
}
