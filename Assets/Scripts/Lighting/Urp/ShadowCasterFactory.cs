using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Gives a box collider's sprite a 2D shadow caster the size of the collider. The caster's shape is not settable from
// code through the package's public API, so it is written the way the Editor writes it: into the caster's serialized
// fields while it is still inactive, so it builds its shadow mesh from them when it wakes. If a package update renames
// those fields the level simply casts no shadows (a warning says so once) and everything else keeps working.
public static class ShadowCasterFactory {
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const int ShapeEditorSource = 1;

    private static bool hasWarned;

    public static bool TryAdd(BoxCollider2D box) {
        GameObject host = new GameObject("ShadowCaster");
        host.SetActive(false);
        host.transform.SetParent(box.transform, false);
        ShadowCaster2D caster = host.AddComponent<ShadowCaster2D>();
        caster.selfShadows = false;

        if (!TryWriteShape(caster, ShapeOf(box))) {
            UnityEngine.Object.Destroy(host);
            if (!hasWarned) {
                hasWarned = true;
                Debug.LogWarning("The 2D shadow caster of this render pipeline version keeps its shape somewhere else: the level will not cast shadows.");
            }
            return false;
        }
        host.SetActive(true);
        return true;
    }

    // The collider's rectangle in its own transform's space, counter-clockwise.
    private static Vector3[] ShapeOf(BoxCollider2D box) {
        Vector2 half = box.size / 2f;
        Vector2 center = box.offset;
        return new[] {
            new Vector3(center.x - half.x, center.y - half.y, 0f),
            new Vector3(center.x + half.x, center.y - half.y, 0f),
            new Vector3(center.x + half.x, center.y + half.y, 0f),
            new Vector3(center.x - half.x, center.y + half.y, 0f),
        };
    }

    private static bool TryWriteShape(ShadowCaster2D caster, Vector3[] path) {
        try {
            Type type = typeof(ShadowCaster2D);
            FieldInfo shape = type.GetField("m_ShapePath", Private);
            FieldInfo hash = type.GetField("m_ShapePathHash", Private);
            FieldInfo source = type.GetField("m_ShadowCastingSource", Private);
            if (shape == null || hash == null || source == null) {
                return false;
            }
            shape.SetValue(caster, path);
            hash.SetValue(caster, 1 + path.Length * 7919 + (int)(path[2].x * 1000f) + (int)(path[2].y * 100000f));
            source.SetValue(caster, Enum.ToObject(source.FieldType, ShapeEditorSource));
            return true;
        }
        catch (Exception) {
            return false;
        }
    }
}
