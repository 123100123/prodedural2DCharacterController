using UnityEngine;

// A fixed set of line renderers reused for the bullet tracers: a shot lights one up, it fades and switches itself off.
// Nothing is allocated while shooting; when every line is busy the oldest one is taken over.
public sealed class TracerPool {
    private const int Size = 12;
    private const int SortingOrder = 100;
    // The muzzle end of the line is dimmer than the far end, so it reads as a streak flying away from the gun.
    private const float MuzzleEndBrightness = 0.3f;

    private readonly GameObject root;
    private readonly Material material;
    private readonly LineRenderer[] lines = new LineRenderer[Size];
    private readonly float[] ages = new float[Size];
    private readonly float[] durations = new float[Size];
    private readonly Color[] colors = new Color[Size];
    private readonly bool[] active = new bool[Size];
    private int next;

    public TracerPool() {
        root = new GameObject("Tracers");
        material = new Material(Shader.Find("Sprites/Default")) { name = "TracerMaterial" };
        for (int index = 0; index < Size; index++) {
            lines[index] = CreateLine(index);
        }
    }

    public GameObject Root => root;

    // How many tracers are visible right now.
    public int ActiveCount {
        get {
            int count = 0;
            for (int index = 0; index < Size; index++) {
                count += active[index] ? 1 : 0;
            }
            return count;
        }
    }

    public void Spawn((Vector2 from, Vector2 to) segment, WeaponSettings settings) {
        int index = next;
        next = (next + 1) % Size;
        LineRenderer line = lines[index];
        line.SetPosition(0, segment.from);
        line.SetPosition(1, segment.to);
        line.startWidth = settings.tracerWidth;
        line.endWidth = settings.tracerWidth * 0.5f;
        ages[index] = 0f;
        durations[index] = settings.tracerDuration;
        colors[index] = settings.tracerColor;
        active[index] = true;
        line.enabled = true;
        Fade(index);
    }

    public void Tick(float deltaTime) {
        for (int index = 0; index < Size; index++) {
            if (!active[index]) {
                continue;
            }
            ages[index] += deltaTime;
            if (ages[index] >= durations[index]) {
                active[index] = false;
                lines[index].enabled = false;
                continue;
            }
            Fade(index);
        }
    }

    public void Dispose() {
        Discard(root);
        Discard(material);
    }

    // Edit-mode tests build the pool outside play mode, where Destroy is not allowed.
    private static void Discard(Object unwanted) {
        if (unwanted == null) {
            return;
        }
        if (Application.isPlaying) {
            Object.Destroy(unwanted);
        }
        else {
            Object.DestroyImmediate(unwanted);
        }
    }

    private void Fade(int index) {
        Color color = colors[index];
        color.a *= TracerFade.Alpha(ages[index], durations[index]);
        Color muzzleEnd = color;
        muzzleEnd.a *= MuzzleEndBrightness;
        lines[index].startColor = muzzleEnd;
        lines[index].endColor = color;
    }

    private LineRenderer CreateLine(int index) {
        GameObject tracer = new GameObject("Tracer" + index);
        tracer.transform.SetParent(root.transform, false);
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = EnvironmentLayers.Level;
        line.sortingOrder = SortingOrder;
        line.enabled = false;
        return line;
    }
}
