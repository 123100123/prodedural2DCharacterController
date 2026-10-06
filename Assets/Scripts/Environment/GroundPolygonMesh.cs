using System.Collections.Generic;
using UnityEngine;

// The visible face of a piece of ground shaped by a PolygonCollider2D (a hill): a mesh filling the collider's outline,
// with a texture stretched over its bounds as the level's blocks stretch their tile, drawn by the MeshRenderer (the lit sprite material on the level sorting layer,
// like the level's blocks). Built in code from the collider whenever it is enabled, in the Editor too, so the scene stores
// only the collider and these settings; the mesh is never saved.
[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D), typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class GroundPolygonMesh : MonoBehaviour {
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private const string FallbackShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";

    [Tooltip("The tile drawn over the face (the level's ground tile); none draws it plain.")]
    [SerializeField] private Texture2D texture;
    [SerializeField] private Color tint = Color.white;

    private Mesh mesh;
    private MaterialPropertyBlock properties;

    public Mesh Mesh => mesh;

    public void Configure((Texture2D texture, Color tint) look) {
        texture = look.texture;
        tint = look.tint;
        Rebuild();
    }

    private void OnEnable() => Rebuild();

    private void OnDestroy() {
        if (mesh != null) {
            if (Application.isPlaying) {
                Destroy(mesh);
            } else {
                DestroyImmediate(mesh);
            }
        }
    }

    // Fills the collider's first path with triangles, the texture stretched across its bounds.
    public void Rebuild() {
        Vector2[] outline = GetComponent<PolygonCollider2D>().GetPath(0);
        if (outline == null || outline.Length < 3) {
            return;
        }
        if (mesh == null) {
            mesh = new Mesh { name = name + " Face", hideFlags = HideFlags.DontSave };
        }
        Vector3[] vertices = new Vector3[outline.Length];
        Vector2[] uv = new Vector2[outline.Length];
        Color[] colors = new Color[outline.Length];
        Rect bounds = BoundsOf(outline);
        for (int index = 0; index < outline.Length; index++) {
            vertices[index] = outline[index];
            uv[index] = Rect.PointToNormalized(bounds, outline[index]);
            colors[index] = tint;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.triangles = Triangulate(outline);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer.sharedMaterial == null) {
            Shader shader = Shader.Find(FallbackShader);
            if (shader != null) {
                meshRenderer.sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
        }
        properties = properties ?? new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(properties);
        properties.SetTexture(MainTexId, texture != null ? texture : Texture2D.whiteTexture);
        meshRenderer.SetPropertyBlock(properties);
    }

    private static Rect BoundsOf(Vector2[] outline) {
        Vector2 min = outline[0];
        Vector2 max = outline[0];
        foreach (Vector2 point in outline) {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // Ear clipping: the triangles of a simple polygon given in either winding.
    public static int[] Triangulate(IReadOnlyList<Vector2> polygon) {
        List<int> remaining = new List<int>();
        for (int index = 0; index < polygon.Count; index++) {
            remaining.Add(index);
        }
        float winding = SignedArea(polygon) >= 0f ? 1f : -1f;
        List<int> triangles = new List<int>();
        int guard = polygon.Count * polygon.Count;
        while (remaining.Count > 3 && guard-- > 0) {
            for (int corner = 0; corner < remaining.Count; corner++) {
                int previous = remaining[(corner + remaining.Count - 1) % remaining.Count];
                int current = remaining[corner];
                int next = remaining[(corner + 1) % remaining.Count];
                if (IsEar((previous, current, next), (polygon, remaining, winding))) {
                    triangles.AddRange(new[] { previous, current, next });
                    remaining.RemoveAt(corner);
                    break;
                }
            }
        }
        if (remaining.Count == 3) {
            triangles.AddRange(remaining);
        }
        return triangles.ToArray();
    }

    private static bool IsEar((int previous, int current, int next) corner, (IReadOnlyList<Vector2> polygon, List<int> remaining, float winding) shape) {
        Vector2 a = shape.polygon[corner.previous];
        Vector2 b = shape.polygon[corner.current];
        Vector2 c = shape.polygon[corner.next];
        if (Cross(b - a, c - b) * shape.winding <= 0f) {
            return false;
        }
        foreach (int other in shape.remaining) {
            if (other != corner.previous && other != corner.current && other != corner.next && IsInside(shape.polygon[other], (a, b, c))) {
                return false;
            }
        }
        return true;
    }

    private static bool IsInside(Vector2 point, (Vector2 a, Vector2 b, Vector2 c) triangle) {
        float ab = Cross(triangle.b - triangle.a, point - triangle.a);
        float bc = Cross(triangle.c - triangle.b, point - triangle.b);
        float ca = Cross(triangle.a - triangle.c, point - triangle.c);
        return (ab > 0f && bc > 0f && ca > 0f) || (ab < 0f && bc < 0f && ca < 0f);
    }

    private static float SignedArea(IReadOnlyList<Vector2> polygon) {
        float area = 0f;
        for (int index = 0; index < polygon.Count; index++) {
            area += Cross(polygon[index], polygon[(index + 1) % polygon.Count]);
        }
        return area / 2f;
    }

    private static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;
}
