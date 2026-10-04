using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sets up the world around the character in the open scene: the background sorting layers, a placeholder parallax
// background (generated art, three layers behind the level), and lit materials on the level's sprites. Everything is
// idempotent, and SetUp() refuses to touch a scene with unsaved changes so a scripted save never carries edits
// that were not made here. Run it from Tools > Procedural Character > Environment, or through the MCP.
public static class EnvironmentSetup {
    private const string ArtFolder = "Assets/Art/Background";
    private const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    private const string BackgroundRoot = "Background";
    private const float PixelsPerUnit = 32f;
    private const string MarkerSpritePrefix = "icon";

    private readonly struct LayerSpec {
        public LayerSpec(string name, string sprite, string sortingLayer, (Vector2 size, Vector2 center, float z) placement, Vector2 parallax) {
            Name = name;
            Sprite = sprite;
            SortingLayer = sortingLayer;
            Size = placement.size;
            Center = placement.center;
            Z = placement.z;
            Parallax = parallax;
        }

        public string Name { get; }
        public string Sprite { get; }
        public string SortingLayer { get; }
        public Vector2 Size { get; }
        public Vector2 Center { get; }
        public float Z { get; }
        public Vector2 Parallax { get; }
    }

    // Far mountains, middle ruins and the near wall, wide enough for the camera's travel plus each layer's own lag. The
    // wall reaches well below the floor and rises a few units above it (its crenellated top is the edge), so the ruins
    // and mountains show above it.
    private static readonly LayerSpec[] Layers = {
        new LayerSpec("BackgroundFar", "bg_far", EnvironmentLayers.Far, (new Vector2(200f, 16f), new Vector2(0f, 2f), 10f), new Vector2(0.25f, 0.4f)),
        new LayerSpec("BackgroundMid", "bg_mid", EnvironmentLayers.Mid, (new Vector2(160f, 16f), new Vector2(0f, 1f), 6f), new Vector2(0.6f, 0.7f)),
        new LayerSpec("BackgroundWall", "bg_wall", EnvironmentLayers.Wall, (new Vector2(160f, 32f), new Vector2(2f, -16f), 3f), new Vector2(0.9f, 0.9f)),
    };

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
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return string.Join("\n", layers, art, background, lit);
    }

    // ---- Sorting layers -------------------------------------------------------------------------------------------

    [MenuItem("Tools/Procedural Character/Environment/Create Sorting Layers")]
    public static void CreateSortingLayersFromMenu() => Debug.Log(EnsureSortingLayers());

    // Inserts the background layers before Default, back to front, keeping any layer that exists.
    public static string EnsureSortingLayers() {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
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
        return added > 0 ? $"Sorting layers: added {added} ({string.Join(", ", EnvironmentLayers.BackToFront)})." : "Sorting layers: already set.";
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

    // Three seamless, seeded textures: mountains, ruins and a brick wall. Replace the PNGs with real art any time; the
    // layers only reference the sprites.
    public static string GenerateArt() {
        Directory.CreateDirectory(ArtFolder);
        WriteTexture("bg_far", PlaceholderArt.Mountains(1024, 512));
        WriteTexture("bg_mid", PlaceholderArt.Ruins(512, 512));
        WriteTexture("bg_wall", PlaceholderArt.BrickWall(256, 1024));
        AssetDatabase.Refresh();
        foreach (LayerSpec layer in Layers) {
            ConfigureImporter($"{ArtFolder}/{layer.Sprite}.png");
        }
        return "Background art: generated bg_far, bg_mid and bg_wall in " + ArtFolder + ".";
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

    // A root with the three layers, each tiled, lit, on its sorting layer and moved by its parallax. Rebuilds the layers
    // from the specification each time, so the numbers above are the single place they are set.
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
        foreach (LayerSpec spec in Layers) {
            BuildLayer(root.transform, spec, lit);
        }
        return $"Background: {Layers.Length} parallax layers under '{BackgroundRoot}'.";
    }

    private static void BuildLayer(Transform root, LayerSpec spec, Material lit) {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{spec.Sprite}.png");
        Transform layer = root.Find(spec.Name);
        GameObject host = layer != null ? layer.gameObject : new GameObject(spec.Name);
        host.transform.SetParent(root, false);
        host.transform.position = new Vector3(spec.Center.x, spec.Center.y, spec.Z);

        SpriteRenderer renderer = EnsureComponent<SpriteRenderer>(host);
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = spec.Size;
        renderer.sortingLayerName = spec.SortingLayer;
        renderer.sortingOrder = 0;
        renderer.sharedMaterial = lit;

        ParallaxLayer parallax = EnsureComponent<ParallaxLayer>(host);
        parallax.Factor = spec.Parallax;
        EditorUtility.SetDirty(host);
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
