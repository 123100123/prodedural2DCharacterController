using UnityEngine;

// A fixed set of shell casings reused for every shot: a tiny generated sprite with a Rigidbody2D that is thrown out of
// the pistol, tumbles, settles on the ground, fades and goes back to the pool. Nothing is instantiated while shooting;
// when every casing is busy the oldest one is taken over.
//
// Casings live on the Ignore Raycast layer (2). Every gameplay mask (the shot's hit mask, the body's solid mask, the
// foothold ground mask and the ledge mask) is Physics2D.DefaultRaycastLayers, which leaves that layer out, so a casing
// is never hit by the hitscan, never counts as ground for the feet and never blocks the hull; it still collides with the
// level, which is on Default. The character has no physical collider (its hull is a cast), so a casing cannot push it,
// and casings exclude their own layer so they do not pile up against each other.
public sealed class CasingPool {
    public const int Size = 16;
    private const int IgnoreRaycastLayer = 2;
    private const int SortingOrder = 19;
    private const float Bounciness = 0.25f;
    private const float Friction = 1f;
    private const float GravityScale = 1f;
    // Air and rolling resistance, so a casing comes to rest on the ground instead of skating on.
    private const float LinearDamping = 0.6f;
    private const float AngularDamping = 3f;

    private readonly GameObject root;
    private readonly Sprite sprite;
    private readonly Material material;
    private readonly PhysicsMaterial2D physicsMaterial;
    private readonly Rigidbody2D[] bodies = new Rigidbody2D[Size];
    private readonly SpriteRenderer[] renderers = new SpriteRenderer[Size];
    private readonly float[] ages = new float[Size];
    private readonly (float lifetime, float fade)[] lives = new (float, float)[Size];
    private readonly bool[] active = new bool[Size];
    private readonly bool[] touched = new bool[Size];
    // Time.fixedTime when each casing was thrown: until a physics step has run after that, its contacts are the old ones.
    private readonly float[] thrownAtFixedTime = new float[Size];
    private readonly Collider2D[] colliders = new Collider2D[Size];

    public CasingPool(Transform parent, Material material) {
        root = new GameObject("Casings");
        root.transform.SetParent(parent, false);
        this.material = material;
        sprite = EffectTextures.CasingSprite();
        physicsMaterial = new PhysicsMaterial2D("Casing") { bounciness = Bounciness, friction = Friction };
        for (int index = 0; index < Size; index++) {
            CreateCasing(index);
        }
    }

    // How many casings are out of the pool right now.
    public int ActiveCount {
        get {
            int count = 0;
            for (int index = 0; index < Size; index++) {
                count += active[index] ? 1 : 0;
            }
            return count;
        }
    }

    // A casing touched something (the ground, mostly) for the first time since it was thrown, and where.
    public event System.Action<Vector2> FirstContact;

    public bool IsActive(int index) => active[index];

    public Rigidbody2D BodyOf(int index) => bodies[index];

    public void Eject((Vector2 position, Vector2 velocity, float spin) throwing, (float lifetime, float fadeTime) life) {
        int index = PickSlot(active, ages);
        Rigidbody2D body = bodies[index];
        // Placed through the transform before the body simulates again: a body that is not simulated ignores its position.
        body.transform.SetPositionAndRotation(throwing.position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        body.gameObject.SetActive(true);
        body.simulated = true;
        body.linearVelocity = throwing.velocity;
        body.angularVelocity = throwing.spin;
        ages[index] = 0f;
        lives[index] = life;
        active[index] = true;
        touched[index] = false;
        thrownAtFixedTime[index] = Time.fixedTime;
        renderers[index].color = Color.white;
    }

    public void Tick(float deltaTime) {
        for (int index = 0; index < Size; index++) {
            if (!active[index]) {
                continue;
            }
            ages[index] += deltaTime;
            if (!touched[index] && PhysicsHasSteppedSince((thrownAtFixedTime[index], Time.fixedTime)) && colliders[index].IsTouchingLayers(Physics2D.DefaultRaycastLayers)) {
                touched[index] = true;
                FirstContact?.Invoke(bodies[index].position);
            }
            float alpha = EffectMath.CasingAlpha(ages[index], lives[index].lifetime, lives[index].fade);
            if (alpha <= 0f) {
                Release(index);
                continue;
            }
            renderers[index].color = new Color(1f, 1f, 1f, alpha);
        }
    }

    // Takes every casing out of play at once.
    public void Clear() {
        for (int index = 0; index < Size; index++) {
            if (active[index]) {
                Release(index);
            }
        }
    }

    // Moving a casing's transform only reaches the physics world at the next step, so a casing taken over from the ground
    // still reports its old contact until fixed time has advanced past the moment it was thrown.
    public static bool PhysicsHasSteppedSince((float thrownAtFixedTime, float fixedTimeNow) clock) => clock.fixedTimeNow > clock.thrownAtFixedTime;

    // The slot to reuse: the first free one, otherwise the one that has lain longest.
    public static int PickSlot(bool[] active, float[] ages) {
        int oldest = 0;
        for (int index = 0; index < active.Length; index++) {
            if (!active[index]) {
                return index;
            }
            if (ages[index] > ages[oldest]) {
                oldest = index;
            }
        }
        return oldest;
    }

    public void Dispose() {
        Discard(root);
        Discard(sprite != null ? sprite.texture : null);
        Discard(sprite);
        Discard(physicsMaterial);
    }

    private void Release(int index) {
        active[index] = false;
        bodies[index].simulated = false;
        bodies[index].gameObject.SetActive(false);
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

    private void CreateCasing(int index) {
        GameObject casing = new GameObject("Casing" + index) { layer = IgnoreRaycastLayer };
        casing.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = casing.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = EnvironmentLayers.Level;
        renderer.sortingOrder = SortingOrder;

        Rigidbody2D body = casing.AddComponent<Rigidbody2D>();
        body.gravityScale = GravityScale;
        body.linearDamping = LinearDamping;
        body.angularDamping = AngularDamping;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sharedMaterial = physicsMaterial;
        body.simulated = false;

        // A capsule, as a cartridge case is a little cylinder (and it is not mistaken for a level block, which is a box).
        CapsuleCollider2D collider = casing.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Horizontal;
        collider.size = sprite.bounds.size;
        collider.excludeLayers = 1 << IgnoreRaycastLayer;

        bodies[index] = body;
        colliders[index] = collider;
        renderers[index] = renderer;
        casing.SetActive(false);
    }
}
