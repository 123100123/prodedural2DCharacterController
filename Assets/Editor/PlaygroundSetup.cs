using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds a showcase area at the right end of the level in the open scene, to try the moves on: a pit to wall-jump out of,
// a low ceiling to slide under, a ledge to hang from, one-way platforms to drop through, two crates to push, three
// targets to shoot and a lamp. Everything is idempotent (one "Playground" root whose children are replaced on a rebuild,
// undo-registered) and Build() refuses a scene with unsaved changes, like EnvironmentSetup, so a scripted save never
// carries edits that were not made here. Run it from Tools > Procedural Character > Build Playground, or through the MCP.
//
// The area starts at the right edge of the level's furthest block and continues at that block's top height, so it is
// reached by climbing onto that block. Measurements below are relative to that start: x along the floor, y above its top.
public static class PlaygroundSetup {
    public const string RootName = "Playground";
    private const string SpritePath = "Assets/Art/Sprites/groundTile.jpg";
    private const string UndoName = "Build Playground";

    // The blocks of the level are the sprite stretched over a box collider, so every playground block is a scaled tile.
    private static readonly Color BlockTint = Color.white;
    private static readonly Color PlatformTint = new Color(0.62f, 0.78f, 1f, 1f);
    private static readonly Color LightCrateTint = new Color(1f, 0.8f, 0.5f, 1f);
    private static readonly Color HeavyCrateTint = new Color(0.62f, 0.45f, 0.3f, 1f);
    private static readonly Color TargetTint = new Color(1f, 0.45f, 0.35f, 1f);

    // Layout (x along the area, y above its floor).
    // A slab, not a mass: the lit background shows under it (the level's own sun-shadow scenarios probe the space below the raised block next to it).
    private const float FloorThickness = 1.2f;
    private const float PitFloorThickness = 6f;
    private const float CrateSize = 2.4f;
    private const float LightCrateMass = 3f;
    private const float HeavyCrateMass = 14f;
    private const float TargetSize = 1.4f;
    private const float CeilingStart = 44f;
    private const float CeilingWidth = 12f;
    // The same clearance the slide scenario tests use: too low to stand under, enough to slide or crouch under.
    private const float CeilingClearance = 4.1f;
    private const float CeilingThickness = 3f;
    private const float LedgeStart = 60f;
    private const float LedgeWidth = 8f;
    private const float LedgeHeight = 4.2f;
    private const float PlatformThickness = 0.4f;
    private const float PlatformWidth = 5f;
    private const float PitLeft = 94f;
    private const float PitWallWidth = 6f;
    private const float PitGap = 2.8f;
    private const float PitDepth = 10.5f;
    private const float PitRight = 110f;
    // The far wall towers over the floor the pit is entered from, out of any ledge grab and too high to bridge with a stride: walking off the edge falls in.
    private const float PitRightRise = 6f;

    [MenuItem("Tools/Procedural Character/Build Playground")]
    public static void BuildFromMenu() => Debug.Log(Build());

    // Returns what it did, or why it did not.
    public static string Build() {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty) {
            return $"The scene '{scene.name}' has unsaved changes. Save it (or revert it) first, so this setup does not save work it did not make.";
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentSetup.LitMaterialPath);
        if (sprite == null || lit == null) {
            return "Playground: the level tile sprite or the URP lit sprite material was not found.";
        }

        GameObject root = FindRoot(scene);
        if (!TryFindStart(scene, root, out Bounds reference)) {
            return "Playground: the scene has no level block to extend.";
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        if (root == null) {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            SceneManager.MoveGameObjectToScene(root, scene);
        } else {
            for (int index = root.transform.childCount - 1; index >= 0; index--) {
                Undo.DestroyObjectImmediate(root.transform.GetChild(index).gameObject);
            }
        }
        root.transform.position = Vector3.zero;

        Builder builder = new Builder(root.transform, sprite, lit, new Vector2(reference.max.x, reference.max.y), reference.center.z);
        builder.Floor();
        builder.Crates();
        builder.Targets();
        builder.SlideCeiling();
        builder.HangLedge();
        builder.OneWayPlatforms();
        builder.WallJumpPit();
        string lamp = builder.Lamp();

        string casters = EnvironmentSetup.AddShadowCasters(scene);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Playground: {root.transform.childCount} objects from x {builder.Start.x:F1} to {builder.Start.x + PitRight:F1} " +
               $"at floor height {builder.Start.y:F2}.\n{lamp}\n{casters}";
    }

    private static GameObject FindRoot(Scene scene) {
        foreach (GameObject candidate in scene.GetRootGameObjects()) {
            if (candidate.name == RootName) {
                return candidate;
            }
        }
        return null;
    }

    // The solid block of the level (outside the playground, the background and the character) reaching furthest right.
    private static bool TryFindStart(Scene scene, GameObject playground, out Bounds furthest) {
        furthest = default;
        bool found = false;
        foreach (GameObject top in scene.GetRootGameObjects()) {
            if (top == playground || top.name == "Background") {
                continue;
            }
            foreach (BoxCollider2D box in top.GetComponentsInChildren<BoxCollider2D>(false)) {
                bool isBlock = !box.isTrigger && box.GetComponent<SpriteRenderer>() != null && box.GetComponent<Rigidbody2D>() == null
                    && box.GetComponentInParent<CharacterDriver>() == null;
                if (isBlock && (!found || box.bounds.max.x > furthest.max.x)) {
                    furthest = box.bounds;
                    found = true;
                }
            }
        }
        return found;
    }

    // Creates the playground's objects at positions relative to the start of the area.
    private sealed class Builder {
        private readonly Transform root;
        private readonly Sprite sprite;
        private readonly Material lit;
        private readonly float depth;

        public Vector2 Start { get; }

        public Builder(Transform root, Sprite sprite, Material lit, Vector2 start, float depth) {
            this.root = root;
            this.sprite = sprite;
            this.lit = lit;
            Start = start;
            this.depth = depth;
        }

        private float SpriteUnits => sprite.bounds.size.x;

        // A tile stretched over the box from x0 to x1 and y0 to y1 (relative), with a collider the size of the box.
        private GameObject Block(string name, (float x0, float x1) xs, (float y0, float y1) ys, Color tint) {
            GameObject block = Create(name, new Vector3(Start.x + (xs.x0 + xs.x1) / 2f, Start.y + (ys.y0 + ys.y1) / 2f, depth));
            block.transform.localScale = new Vector3((xs.x1 - xs.x0) / SpriteUnits, (ys.y1 - ys.y0) / SpriteUnits, 1f);
            Dress(block, tint, 0);
            block.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
            return block;
        }

        private void Dress(GameObject target, Color tint, int order) {
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sharedMaterial = lit;
            renderer.sortingLayerName = EnvironmentLayers.Level;
            renderer.sortingOrder = order;
        }

        private GameObject Create(string name, Vector3 position) {
            GameObject made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, UndoName);
            made.transform.SetParent(root, false);
            made.transform.position = position;
            return made;
        }

        // The floor the whole area stands on, up to the pit.
        public void Floor() => Block("Floor", (0f, PitLeft), (-FloorThickness, 0f), BlockTint);

        // A light and a heavy crate side by side on the runway, with the whole floor ahead of them to push along.
        public void Crates() {
            Crate("Crate Light", 5.2f, LightCrateMass, LightCrateTint);
            Crate("Crate Heavy", 12.2f, HeavyCrateMass, HeavyCrateTint);
        }

        private void Crate(string name, float centerX, float mass, Color tint) {
            GameObject crate = Create(name, new Vector3(Start.x + centerX, Start.y + CrateSize / 2f, 0f));
            float scale = CrateSize / SpriteUnits;
            crate.transform.localScale = new Vector3(scale, scale, 1f);
            Dress(crate, tint, 1);
            crate.AddComponent<BoxCollider2D>();
            crate.AddComponent<Rigidbody2D>();
            PushableCrate pushable = crate.AddComponent<PushableCrate>();
            // The collider is in the crate's scaled space, so it takes the sprite's size, not the crate's.
            pushable.Configure((sprite.bounds.size, mass));
            EditorUtility.SetDirty(pushable);
        }

        // Three targets hung at different heights and distances from a shooter standing on the floor, all above head height
        // (a standing character is about 4.3 tall) so they never block the way.
        public void Targets() {
            Target("Target Near Low", 26f, 5.6f);
            Target("Target Mid High", 33f, 8.4f);
            Target("Target Far Mid", 40f, 6.8f);
        }

        private void Target(string name, float x, float height) {
            GameObject target = Create(name, new Vector3(Start.x + x, Start.y + height, 0f));
            float scale = TargetSize / SpriteUnits;
            target.transform.localScale = new Vector3(scale, scale, 1f);
            Dress(target, TargetTint, 1);
            target.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
            target.AddComponent<ShootingTarget>();
        }

        // A block hanging over the floor: a standing character cannot pass, a slide (or crouch walk) can.
        public void SlideCeiling() {
            Block("Slide Ceiling", (CeilingStart, CeilingStart + CeilingWidth), (CeilingClearance, CeilingClearance + CeilingThickness), BlockTint);
        }

        // A block taller than a jump with a wide top: grab its edge, hang, then climb or let go.
        public void HangLedge() {
            Block("Hang Ledge", (LedgeStart, LedgeStart + LedgeWidth), (0f, LedgeHeight), BlockTint);
        }

        // A staircase of thin one-way platforms over the solid floor: jump up through them, drop down with down + jump.
        public void OneWayPlatforms() {
            OneWay("One Way Low", 71f, 2f);
            OneWay("One Way Mid", 77f, 3.8f);
            OneWay("One Way High", 83f, 5.6f);
        }

        private void OneWay(string name, float x, float top) {
            Block(name, (x, x + PlatformWidth), (top - PlatformThickness, top), PlatformTint).AddComponent<OneWayPlatform>();
        }

        // A pit with sheer walls just wider than the character, entered by walking off the left edge: wall-jump up between them
        // and climb out over the left edge (the far wall is a tower, the end of the area).
        public void WallJumpPit() {
            float gapStart = PitLeft + PitWallWidth;
            float gapEnd = gapStart + PitGap;
            Block("Pit Left Wall", (PitLeft, gapStart), (-PitDepth - PitFloorThickness, 0f), BlockTint);
            Block("Pit Right Wall", (gapEnd, PitRight), (-PitDepth - PitFloorThickness, PitRightRise), BlockTint);
            Block("Pit Bottom", (gapStart, gapEnd), (-PitDepth - PitFloorThickness, -PitDepth), BlockTint);
        }

        // One lamp from the shared prefab over the middle of the area, so the playground is lit at dusk.
        public string Lamp() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentSetup.LampPrefabPath);
            if (prefab == null) {
                return "Playground lamp: the lamp prefab does not exist; run Set Up Environment first.";
            }
            GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            Undo.RegisterCreatedObjectUndo(lamp, UndoName);
            lamp.name = "Playground Lamp";
            lamp.transform.position = new Vector3(Start.x + 36f, Start.y + 2.6f, 0f);
            return "Playground lamp: placed.";
        }
    }
}
