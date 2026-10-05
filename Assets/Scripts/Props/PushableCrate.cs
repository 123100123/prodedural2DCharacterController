using UnityEngine;

// Authoring helper for a crate the character can push: sets up its Rigidbody2D (dynamic, mass, gravity, rotation held
// upright or not, interpolated) and its BoxCollider2D. Optional: the character pushes any dynamic Rigidbody2D; this only
// saves setting one up by hand. A bullet already shoves it (the weapon applies the impulse to any dynamic body); as a
// shot receiver it also flashes its sprite.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PushableCrate : MonoBehaviour, IShotReceiver {
    [Tooltip("Collider size in units.")]
    [SerializeField] private Vector2 size = new Vector2(2.4f, 2.4f);
    [Tooltip("Mass in kg. The push's strength against the crate's friction (which grows with its mass) decides how fast it moves.")]
    [SerializeField] private float mass = 10f;
    [Tooltip("The character falls at about 3 g; a crate at 2.5 drops off a ledge with a similar weight.")]
    [SerializeField] private float gravityScale = 2.5f;
    [Tooltip("Keeps the crate upright, so it slides instead of tumbling.")]
    [SerializeField] private bool freezeRotation = true;
    [Tooltip("Colour the sprite flashes to when a bullet strikes it; it fades back to its own colour.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.6f, 1f);
    [SerializeField] private float flashDuration = 0.1f;

    private Rigidbody2D body;
    private BoxCollider2D box;
    private SpriteRenderer sprite;
    private Color restColor;
    private float flashAge = float.PositiveInfinity;

    public Rigidbody2D Body => body;
    // Bullets that struck it since it was created.
    public int HitCount { get; private set; }

    private void Awake() {
        sprite = GetComponent<SpriteRenderer>();
        restColor = sprite != null ? sprite.color : Color.white;
        Configure();
    }

    // Re-applies the settings with a new size and mass (e.g. a crate built in code).
    public void Configure((Vector2 size, float mass) crate) {
        size = crate.size;
        mass = crate.mass;
        Configure();
    }

    public void OnShot(ShotHit hit) {
        HitCount++;
        flashAge = 0f;
    }

    private void Configure() {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = Mathf.Max(0.01f, mass);
        body.gravityScale = gravityScale;
        body.constraints = freezeRotation ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.None;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.StartAwake;
        box.size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        box.offset = Vector2.zero;
    }

    private void Update() {
        if (sprite == null || float.IsPositiveInfinity(flashAge)) {
            return;
        }
        flashAge += Time.deltaTime;
        float share = flashDuration <= 0f ? 1f : Mathf.Clamp01(flashAge / flashDuration);
        sprite.color = Color.Lerp(flashColor, restColor, share);
        if (share >= 1f) {
            flashAge = float.PositiveInfinity;
        }
    }
}
