using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sets up the world around the character in the open scene: the background sorting layer, a plain dot-grid background
// (generated art, one lit sprite behind the level), shadow casters on the level's blocks, lamps and lit materials on the
// level's sprites. Everything is
// idempotent, and SetUp() refuses to touch a scene with unsaved changes so a scripted save never carries edits
// that were not made here. Run it from Tools > Procedural Character > Environment, or through the MCP.
public static class EnvironmentSetup {
    private const string ArtFolder = "Assets/Art/Background";
    internal const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    private const string BackgroundRoot = "Background";
    private const float PixelsPerUnit = 32f;
    private const string GridSprite = "bg_grid";
    // One tile of the grid is five cells of a pixels-per-unit-sized cell (one world unit each); the background is a
    // whole number of tiles, larger than any view, and follows the camera in tile steps.
    private const int GridCellPixels = 32;
    private const int GridCellsPerTile = 5;
    private static readonly Vector2 GridSize = new Vector2(120f, 80f);
    private const float GridDepth = 10f;
    private const string MarkerSpritePrefix = "icon";
    private const string PrefabFolder = "Assets/Prefabs";
    internal const string LampPrefabPath = PrefabFolder + "/Lamp.prefab";
    private const string LampComponentType = "LampLight, ProceduralCharacter.Lighting";
    private const string ShadowCasterType = "UnityEngine.Rendering.Universal.ShadowCaster2D, Unity.RenderPipelines.Universal.2D.Runtime";

    [MenuItem("Tools/Procedural Character/Environment/Set Up Environment")]
    public static void SetUpFromMenu() => Debug.Log(SetUp());

    // Returns what it did, or why it did not.
    public static string SetUp() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty) {
            return $"The scene '{scene.name}' has unsaved changes. Save it (or revert it) first, so this setup does not save work it did not make.";
        }

        string layers = EnsureSortingLayers();
        string art = GenerateArt();
        string background = BuildBackground(scene);
        string lit = MakeLevelSpritesLit(scene);
        string casters = AddShadowCasters(scene);
        string lamps = BuildLamps(scene);
        string shadows = UrpLightingSetup.UseCrispShadows();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return string.Join("\n", layers, art, background, lit, casters, lamps, shadows);
    }

    // ---- Sorting layers -------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Create Sorting Layers")]
    public static void CreateSortingLayersFromMenu() => Debug.Log(EnsureSortingLayers());

    // Inserts the background layer before Default and removes the layers an earlier background used.
    public static string EnsureSortingLayers() {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
        int removed = 0;
        foreach (string obsolete in EnvironmentLayers.Obsolete) {
            int at = IndexOfLayer(layers, obsolete);
            if (at >= 0) {
                layers.DeleteArrayElementAtIndex(at);
                removed++;
            }
        }

        int added = 0;
        foreach (string name in EnvironmentLayers.BackToFront) {
            if (IndexOfLayer(layers, name) >= 0) {
                continue;
            }
            int before = IndexOfLayer(layers, EnvironmentLayers.Level);
            int at = before >= 0 ? before : layers.arraySize;
            layers.InsertArrayElementAtIndex(at);
            SerializedProperty layer = layers.GetArrayElementAtIndex(at);
            layer.FindPropertyRelative("name").stringValue = name;
            layer.FindPropertyRelative("uniqueID").uintValue = StableId(name);
            layer.FindPropertyRelative("locked").boolValue = false;
            added++;
        }
        tagManager.ApplyModifiedProperties();
        return $"Sorting layers: {added} added, {removed} obsolete removed ({string.Join(", ", EnvironmentLayers.BackToFront)}).";
    }

    private static int IndexOfLayer(SerializedProperty layers, string name) {
        for (int index = 0; index < layers.arraySize; index++) {
            if (layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == name) {
                return index;
            }
        }
        return -1;
    }

    // A fixed id per layer name, so running the setup on another machine gives the same ids.
    private static uint StableId(string name) {
        unchecked {
            uint hash = 2166136261u;
            foreach (char character in name) {
                hash = (hash ^ character) * 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }
    }

    // ---- Placeholder art ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Generate Background Art")]
    public static void GenerateArtFromMenu() => Debug.Log(GenerateArt());

    // The dot-grid tile. Replace the PNG with real art any time; the background only references the sprite.
    public static string GenerateArt() {
        Directory.CreateDirectory(ArtFolder);
        WriteTexture(GridSprite, PlaceholderArt.DotGrid(GridCellPixels, GridCellsPerTile));
        AssetDatabase.Refresh();
        ConfigureImporter($"{ArtFolder}/{GridSprite}.png");
        return "Background art: generated " + GridSprite + " in " + ArtFolder + ".";
    }

    private static void WriteTexture(string name, Texture2D texture) {
        File.WriteAllBytes($"{ArtFolder}/{name}.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    // A sprite that tiles: Full Rect mesh, repeating, uncompressed.
    private static void ConfigureImporter(string path) {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // ---- Background rig -------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Build Background")]
    public static void BuildBackgroundFromMenu() => Debug.Log(BuildBackground(SceneManager.GetActiveScene()));

    // A root with the one background sprite: tiled, lit, on the background sorting layer, following the camera in whole
    // tiles. Anything else under the root (an earlier background's layers) is removed.
    public static string BuildBackground(Scene scene) {
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        if (lit == null) {
            return "Background: the URP lit sprite material was not found; set up the Universal Render Pipeline first.";
        }

        GameObject root = FindRoot(scene, BackgroundRoot);
        if (root == null) {
            root = new GameObject(BackgroundRoot);
        }
        if (root.scene != scene) {
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        RemoveOtherChildren(root.transform, "BackgroundGrid");

        Transform existing = root.transform.Find("BackgroundGrid");
        GameObject host = existing != null ? existing.gameObject : new GameObject("BackgroundGrid");
        host.transform.SetParent(root.transform, false);
        host.transform.position = new Vector3(0f, 0f, GridDepth);

        SpriteRenderer renderer = EnsureComponent<SpriteRenderer>(host);
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{GridSprite}.png");
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = GridSize;
        renderer.sortingLayerName = EnvironmentLayers.Background;
        renderer.sortingOrder = 0;
        renderer.sharedMaterial = lit;
        EnsureComponent<GridBackground>(host);
        EditorUtility.SetDirty(host);
        return $"Background: one dot-grid sprite ({GridSize.x} x {GridSize.y} units) under '{BackgroundRoot}'.";
    }

    private static void RemoveOtherChildren(Transform root, string keep) {
        for (int index = root.childCount - 1; index >= 0; index--) {
            Transform child = root.GetChild(index);
            if (child.name != keep) {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    // The component, added if the object has none (Unity objects cannot use ??: a destroyed one is not null).
    private static T EnsureComponent<T>(GameObject host) where T : Component {
        T existing = host.GetComponent<T>();
        return existing != null ? existing : host.AddComponent<T>();
    }

    private static GameObject FindRoot(Scene scene, string name) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == name) {
                return root;
            }
        }
        return null;
    }

    // ---- Lamps ----------------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Build Lamps")]
    public static void BuildLampsFromMenu() => Debug.Log(BuildLamps(SceneManager.GetActiveScene()));

    // The lamp prefab (a lantern sprite and a LampLight, which makes its point light and takes the shared Lamps settings)
    // and two lamps placed in the scene: one by the start and one by the raised block. Existing lamps are left where
    // they are, so moving them or adding more is up to you.
    public static string BuildLamps(Scene scene) {
        Type lampType = Type.GetType(LampComponentType);
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        if (lampType == null || lit == null) {
            return "Lamps: the Universal Render Pipeline lighting is not set up.";
        }

        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);
        WriteTexture("lantern", PlaceholderArt.Lantern(48, 96));
        AssetDatabase.Refresh();
        ConfigureImporter($"{ArtFolder}/lantern.png");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath);
        if (prefab == null) {
            GameObject made = new GameObject("Lamp");
            SpriteRenderer renderer = made.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/lantern.png");
            renderer.sharedMaterial = lit;
            renderer.sortingLayerName = EnvironmentLayers.Level;
            renderer.sortingOrder = -5;
            made.AddComponent(lampType);
            prefab = PrefabUtility.SaveAsPrefabAsset(made, LampPrefabPath);
            UnityEngine.Object.DestroyImmediate(made);
        }

        int placed = 0;
        placed += PlaceLamp(scene, prefab, "Lamp A", new Vector2(-11.5f, -2.2f)) ? 1 : 0;
        placed += PlaceLamp(scene, prefab, "Lamp B", new Vector2(-0.4f, -0.6f)) ? 1 : 0;
        return $"Lamps: prefab at {LampPrefabPath}, {placed} lamps placed.";
    }

    private static bool PlaceLamp(Scene scene, GameObject prefab, string name, Vector2 position) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == name) {
                return false;
            }
        }
        GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        lamp.name = name;
        lamp.transform.position = new Vector3(position.x, position.y, 0f);
        return true;
    }

    // ---- Shadow casters -------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Add Shadow Casters To Level Blocks")]
    public static void AddShadowCastersFromMenu() => Debug.Log(AddShadowCasters(SceneManager.GetActiveScene()));

    // Every solid block of the level (a box collider on a sprite, outside the character, the targets and the background) gets a
    // ShadowCaster2D that shadows the layers in EnvironmentLayers.ShadowedByBlocks. Added in the Editor, the caster takes
    // the sprite's outline as its shape, and the choice is saved in the scene (a caster added while playing would not
    // survive a build). Blocks that already have one only have their target layers corrected.
    public static string AddShadowCasters(Scene scene) {
        Type casterType = Type.GetType(ShadowCasterType);
        if (casterType == null) {
            return "Shadow casters: the Universal Render Pipeline is not installed.";
        }

        int[] targets = Array.ConvertAll(EnvironmentLayers.ShadowedByBlocks, SortingLayer.NameToID);
        int added = 0;
        int total = 0;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == BackgroundRoot) {
                continue;
            }
            foreach (BoxCollider2D box in root.GetComponentsInChildren<BoxCollider2D>(true)) {
                // A shooting target is not a block: it vanishes when shot and a caster would go on shadowing where it was.
                bool isLevelBlock = !box.isTrigger && box.GetComponent<SpriteRenderer>() != null && box.GetComponentInParent<CharacterDriver>() == null
                    && box.GetComponent<ShootingTarget>() == null;
                if (!isLevelBlock) {
                    continue;
                }
                Component caster = box.GetComponent(casterType);
                if (caster == null) {
                    caster = box.gameObject.AddComponent(casterType);
                    added++;
                }
                SetTargetLayers(caster, targets);
                total++;
            }
        }
        return $"Shadow casters: {total} level blocks cast onto {string.Join(", ", EnvironmentLayers.ShadowedByBlocks)} ({added} added).";
    }

    private static void SetTargetLayers(Component caster, int[] layerIds) {
        SerializedObject serialized = new SerializedObject(caster);
        SerializedProperty layers = serialized.FindProperty("m_ApplyToSortingLayers");
        layers.arraySize = layerIds.Length;
        for (int index = 0; index < layerIds.Length; index++) {
            layers.GetArrayElementAtIndex(index).intValue = layerIds[index];
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(caster);
    }

    // ---- Lit level sprites ----------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Make Level Sprites Lit")]
    public static void MakeLevelSpritesLitFromMenu() => Debug.Log(MakeLevelSpritesLit(SceneManager.GetActiveScene()));

    // Gives every sprite of the scene the lit material, so it takes the 2D lights and shadows in the Scene view too (the
    // debug marker sprites stay unlit). The sprites on the default material only.
    public static string MakeLevelSpritesLit(Scene scene) {
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        if (lit == null) {
            return "Lit sprites: the URP lit sprite material was not found.";
        }

        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) {
                bool isDefault = renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Sprites/Default";
                bool isMarker = renderer.sprite != null && renderer.sprite.name.StartsWith(MarkerSpritePrefix, StringComparison.Ordinal);
                if (isDefault && !isMarker) {
                    renderer.sharedMaterial = lit;
                    EditorUtility.SetDirty(renderer);
                    changed++;
                }
            }
        }
        return $"Lit sprites: {changed} sprite renderers given the lit material.";
    }
}
