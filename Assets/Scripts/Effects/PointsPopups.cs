using System.Collections.Generic;
using UnityEngine;

// The points that float up from a target when a shot scores: a fixed pool of world-space text (the legacy TextMesh with
// the built-in font, since the project has no text package), each drawn twice (a dark copy behind for legibility), unlit
// on a high sorting order so it reads whatever the dusk lighting does. A popup rises with an ease-out and fades over its
// lifetime, then goes back to the pool; when all are busy the oldest is taken over. The strings are made once per value,
// so a hit allocates nothing.
public sealed class PointsPopups {
    public const int Size = 8;
    private const int TextOrder = 40;
    private const int FontSize = 64;
    private const float ShadowOffset = 0.035f;
    private static readonly Color ShadowColor = new Color(0.08f, 0.04f, 0.02f, 1f);

    private readonly GameObject root;
    private readonly TargetEffectsSettings settings;
    private readonly Font font;
    private readonly GameObject[] hosts = new GameObject[Size];
    private readonly TextMesh[] texts = new TextMesh[Size];
    private readonly TextMesh[] shadows = new TextMesh[Size];
    private readonly bool[] active = new bool[Size];
    private readonly float[] ages = new float[Size];
    private readonly Vector2[] origins = new Vector2[Size];
    private readonly Color[] colors = new Color[Size];
    private readonly Dictionary<int, string> labels = new Dictionary<int, string>();

    public PointsPopups(Transform parent, TargetEffectsSettings settings) {
        this.settings = settings;
        root = new GameObject("Points Popups");
        root.transform.SetParent(parent, false);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int index = 0; index < Size; index++) {
            Create(index);
        }
    }

    // How many popups are out of the pool right now.
    public int ActiveCount {
        get {
            int count = 0;
            for (int index = 0; index < Size; index++) {
                count += active[index] ? 1 : 0;
            }
            return count;
        }
    }

    public bool IsActive(int index) => active[index];
    public string TextOf(int index) => texts[index].text;
    public Vector2 PositionOf(int index) => hosts[index].transform.position;
    public float AlphaOf(int index) => texts[index].color.a;
    public float SizeOf(int index) => texts[index].characterSize;
    public Color ColorOf(int index) => colors[index];

    // Starts a popup for points at a position: the plain colour and size for a hit, the bigger and brighter for the hit
    // that broke the target.
    public void Show(int points, Vector2 position, bool broke) {
        int index = CasingPool.PickSlot(active, ages);
        TextMesh text = texts[index];
        TextMesh shadow = shadows[index];
        string label = Label(points);
        float size = broke ? settings.breakTextSize : settings.hitTextSize;
        text.text = label;
        shadow.text = label;
        text.characterSize = size;
        shadow.characterSize = size;
        colors[index] = broke ? settings.breakTextColor : settings.hitTextColor;
        origins[index] = position + Vector2.up * settings.popupLift;
        ages[index] = 0f;
        active[index] = true;
        hosts[index].SetActive(true);
        Apply(index);
    }

    public void Tick(float deltaTime) {
        for (int index = 0; index < Size; index++) {
            if (!active[index]) {
                continue;
            }
            ages[index] += deltaTime;
            if (ages[index] >= settings.popupLifetime) {
                active[index] = false;
                hosts[index].SetActive(false);
                continue;
            }
            Apply(index);
        }
    }

    public void Dispose() {
        if (root == null) {
            return;
        }
        if (Application.isPlaying) {
            Object.Destroy(root);
        }
        else {
            Object.DestroyImmediate(root);
        }
    }

    private void Apply(int index) {
        float rise = EffectMath.PopupRise(ages[index], settings.popupLifetime, settings.popupRise);
        float alpha = EffectMath.PopupAlpha(ages[index], settings.popupLifetime);
        hosts[index].transform.position = new Vector3(origins[index].x, origins[index].y + rise, 0f);
        Color color = colors[index];
        color.a = alpha;
        texts[index].color = color;
        Color shade = ShadowColor;
        shade.a = alpha;
        shadows[index].color = shade;
    }

    // "+10" for 10, kept so the same string is reused.
    private string Label(int points) {
        if (!labels.TryGetValue(points, out string label)) {
            label = "+" + points.ToString();
            labels[points] = label;
        }
        return label;
    }

    private void Create(int index) {
        GameObject host = new GameObject("Popup " + index);
        host.transform.SetParent(root.transform, false);
        shadows[index] = Text(host.transform, "Shadow", new Vector3(ShadowOffset, -ShadowOffset, 0f), TextOrder - 1);
        texts[index] = Text(host.transform, "Text", Vector3.zero, TextOrder);
        hosts[index] = host;
        host.SetActive(false);
    }

    private TextMesh Text(Transform parent, string name, Vector3 offset, int order) {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = offset;
        TextMesh text = part.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = FontSize;
        text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.richText = false;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.sortingLayerName = EnvironmentLayers.Level;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return text;
    }
}
