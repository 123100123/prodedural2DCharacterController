using UnityEngine;

// A target to shoot at: needs only a collider and a sprite. A hit flashes the sprite and sets it wobbling on a spring;
// a dynamic body is knocked back by the weapon's impulse (the weapon applies that, not the target). At no health left it
// vanishes, and comes back with full health after a delay.
[DisallowMultipleComponent]
public class ShootingTarget : MonoBehaviour, IShotReceiver {
    [SerializeField] private float maxHealth = 3f;
    [Tooltip("Colour the sprite flashes to when it is hit; it fades back to its own colour.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.4f, 0.3f, 1f);
    [SerializeField] private float flashDuration = 0.12f;
    [Tooltip("Speed (degrees per second) a hit sets the target turning, away from the side it was hit on.")]
    [SerializeField] private float wobbleKick = 240f;
    [SerializeField] private SpringSettings wobbleSpring = new SpringSettings(4f, 0.18f, 0f);
    [Tooltip("Seconds a destroyed target stays gone.")]
    [SerializeField] private float respawnDelay = 3f;

    private readonly SecondOrderDynamics wobble = new SecondOrderDynamics(0f);
    private SpriteRenderer sprite;
    private Collider2D[] colliders;
    private Rigidbody2D body;
    private Color restColor;
    private Vector3 restPosition;
    private float restAngle;
    private float flashAge = float.PositiveInfinity;
    private float respawnAt;
    private bool isWobbling;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDestroyed { get; private set; }
    // Bullets that struck it since it was created, destroyed or not.
    public int HitCount { get; private set; }

    private bool WobblesByTransform => body == null || body.bodyType != RigidbodyType2D.Dynamic;

    private void Awake() {
        sprite = GetComponent<SpriteRenderer>();
        colliders = GetComponents<Collider2D>();
        body = GetComponent<Rigidbody2D>();
        restColor = sprite != null ? sprite.color : Color.white;
        restPosition = transform.position;
        restAngle = transform.eulerAngles.z;
        Health = maxHealth;
    }

    public void OnShot(ShotHit hit) {
        if (IsDestroyed) {
            return;
        }
        HitCount++;
        Health -= hit.Damage;
        flashAge = 0f;
        if (WobblesByTransform) {
            float side = Vector3.Cross((Vector3)(hit.Point - (Vector2)transform.position), hit.Direction).z;
            wobble.AddVelocity(wobbleKick * (side >= 0f ? 1f : -1f));
            isWobbling = true;
        }
        if (Health <= 0f) {
            Break();
        }
    }

    private void Update() {
        if (IsDestroyed) {
            if (Time.time >= respawnAt) {
                Respawn();
            }
            return;
        }
        UpdateFlash();
        UpdateWobble();
    }

    private void UpdateFlash() {
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

    private void UpdateWobble() {
        if (!isWobbling) {
            return;
        }
        float angle = wobble.Update((0f, Time.deltaTime), wobbleSpring);
        bool isSettled = Mathf.Abs(angle) < 0.05f && Mathf.Abs(wobble.Velocity) < 0.5f;
        if (isSettled) {
            wobble.Reset(0f);
            angle = 0f;
            isWobbling = false;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, restAngle + angle);
    }

    private void Break() {
        IsDestroyed = true;
        respawnAt = Time.time + respawnDelay;
        SetPresent(false);
    }

    private void Respawn() {
        IsDestroyed = false;
        Health = maxHealth;
        flashAge = float.PositiveInfinity;
        isWobbling = false;
        wobble.Reset(0f);
        if (sprite != null) {
            sprite.color = restColor;
        }
        transform.SetPositionAndRotation(restPosition, Quaternion.Euler(0f, 0f, restAngle));
        if (body != null) {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        SetPresent(true);
    }

    // Hidden and untouchable, or back.
    private void SetPresent(bool isPresent) {
        if (sprite != null) {
            sprite.enabled = isPresent;
        }
        foreach (Collider2D collider in colliders) {
            collider.enabled = isPresent;
        }
        if (body != null) {
            body.simulated = isPresent;
        }
    }
}
