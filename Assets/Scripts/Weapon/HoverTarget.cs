using UnityEngine;

public enum HoverPhase {
    // Flying its route; it can be hit.
    Hovering,
    // Back after a break: rising to its place with the rotor spinning up, not yet hittable.
    Rising,
    // Broken: the rotor runs down while the board drops and tumbles.
    Falling,
    // Out of sight until the respawn delay is over.
    Gone,
}

// A target to shoot at that flies: a square wooden board with dots on it and a helicopter rotor on a mast above, hovering
// along a route round its anchor (the object's scene position), bobbing and leaning into its motion. The picture is built
// in code (HoverTextures). Each hit flashes and wobbles the board and earns points for the popup; after enough hits the
// rotor runs down, the board drops and tumbles, and after a delay it rises back in. It only reacts: the weapon asks it
// for the points (IScoreSource) and the character's audio and effects show them, so it knows nothing of the character.
//
// A kinematic body moved at the physics rate, so the hitscan sees where it is drawn, and a box collider the size of the
// board (the rotor is only a picture). The character passes through it (CharacterPassThrough), but hang it above jump
// height anyway so it never looks as if it should block the walking path.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class HoverTarget : MonoBehaviour, IShotReceiver, IScoreSource {
    private const string LitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string FallbackShader = "Sprites/Default";
    private const int BoardOrder = 1;
    private const int MastOrder = 0;
    private const int BladeOrder = 2;
    private const int HubOrder = 3;
    // The rotor, as shares of the board's side: the mast's height and a blade's reach.
    private const float MastShare = 0.27f;
    private const float BladeShare = 0.78f;
    private const float TiltEase = 7f;
    private const float FallDrift = 0.5f;
    private const float FallPop = 2.2f;
    private const int GroundHits = 4;

    private static Material litMaterial;

    [SerializeField] private HoverTargetSettings settings = new HoverTargetSettings();

    private readonly SecondOrderDynamics wobble = new SecondOrderDynamics(0f);
    private readonly SpriteRenderer[] blades = new SpriteRenderer[2];
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[GroundHits];
    private Rigidbody2D body;
    private BoxCollider2D boardCollider;
    private GameObject visual;
    private SpriteRenderer boardRenderer;
    private SpriteRenderer mastRenderer;
    private SpriteRenderer hubRenderer;
    private Vector2 anchor;
    private Vector2 position;
    private Vector2 velocity;
    private float pathTime;
    private float riseAge;
    private float fallAge;
    private float respawnAt;
    private float flashAge = float.PositiveInfinity;
    private float spin = 1f;
    private float spinTurns;
    private float tilt;
    private float tumble;
    private float tumbleDirection = 1f;
    private float visualAlpha = 1f;
    private bool isWobbling;
    private bool hasPendingScore;
    private ScoreAward pendingScore;

    public HoverTargetSettings Settings => settings;
    public HoverPhase Phase { get; private set; }
    public bool IsBroken => Phase == HoverPhase.Falling || Phase == HoverPhase.Gone;
    // Where the board's middle is now, and the anchor of its route.
    public Vector2 Position => position;
    public Vector2 Anchor => anchor;
    // Hits that struck it (broken or not) and hits that count towards the next break.
    public int HitCount { get; private set; }
    public int HitsTaken { get; private set; }
    public int BreakCount { get; private set; }
    // How fast the rotor is turning, 0 (stopped) to 1 (full speed).
    public float Spin => spin;
    // The lowest the underside of the board gets on its route, for keeping the walking path clear.
    public float LowestPoint => anchor.y - HoverMath.Descent(settings) - settings.boardSize / 2f;

    private void Awake() {
        anchor = transform.position;
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        boardCollider = GetComponent<BoxCollider2D>();
        boardCollider.size = Vector2.one * settings.boardSize;
        boardCollider.offset = Vector2.zero;
        BuildVisual();
        position = Route(0f);
        Teleport(position);
        ApplyVisual();
    }

    // The character walks, jumps and climbs through the board (CharacterPassThrough); shots and physics bodies still meet it.
    private void OnEnable() => CharacterPassThrough.Register(boardCollider != null ? boardCollider : GetComponent<BoxCollider2D>());

    private void OnDisable() => CharacterPassThrough.Unregister(boardCollider != null ? boardCollider : GetComponent<BoxCollider2D>());

    private void OnDestroy() {
        if (visual != null) {
            Destroy(visual);
        }
    }

    public void OnShot(ShotHit hit) {
        if (Phase != HoverPhase.Hovering) {
            return;
        }
        HitCount++;
        HitsTaken++;
        flashAge = 0f;
        float side = Vector3.Cross((Vector3)(hit.Point - position), hit.Direction).z >= 0f ? 1f : -1f;
        wobble.AddVelocity(settings.wobbleKick * side);
        isWobbling = true;

        bool broke = HitsTaken >= settings.hitsToBreak;
        pendingScore = new ScoreAward(HoverMath.Points(settings, broke), position, broke);
        hasPendingScore = true;
        if (broke) {
            Break(hit.Direction, side);
        }
    }

    // Replaces the settings (for code that builds a target; the Awake that ran on adding the component used the defaults).
    public void Configure(HoverTargetSettings replacement) => settings = replacement;

    public bool TryTakeScore(out ScoreAward award) {
        award = pendingScore;
        bool had = hasPendingScore;
        hasPendingScore = false;
        return had;
    }

    private void FixedUpdate() {
        float deltaTime = Time.fixedDeltaTime;
        switch (Phase) {
            case HoverPhase.Hovering:
                pathTime += deltaTime;
                MoveTo(Route(pathTime), deltaTime);
                break;
            case HoverPhase.Rising:
                Rise(deltaTime);
                break;
            case HoverPhase.Falling:
                Fall(deltaTime);
                break;
            default:
                if (Time.time >= respawnAt) {
                    Respawn();
                }
                break;
        }
    }

    private void Update() {
        float deltaTime = Time.deltaTime;
        bool isFlying = Phase == HoverPhase.Hovering || Phase == HoverPhase.Rising;
        float runDown = Mathf.Max(0.01f, settings.rotorRunDown);
        spin = Mathf.MoveTowards(spin, isFlying ? 1f : 0f, deltaTime / runDown);
        spinTurns += settings.rotorSpeed * spin * deltaTime;
        if (!float.IsPositiveInfinity(flashAge)) {
            flashAge += deltaTime;
        }

        float wobbleAngle = 0f;
        if (isWobbling) {
            wobbleAngle = wobble.Update((0f, deltaTime), settings.wobbleSpring);
            if (Mathf.Abs(wobbleAngle) < 0.05f && Mathf.Abs(wobble.Velocity) < 0.5f) {
                wobble.Reset(0f);
                wobbleAngle = 0f;
                isWobbling = false;
            }
        }
        float leanTarget = Phase == HoverPhase.Hovering ? HoverMath.Tilt(settings, velocity.x) : 0f;
        tilt = Mathf.Lerp(tilt, leanTarget, 1f - Mathf.Exp(-TiltEase * deltaTime));
        if (Phase == HoverPhase.Falling) {
            tumble += tumbleDirection * settings.tumbleSpeed * deltaTime;
        }
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, tilt + wobbleAngle + tumble);
        ApplyVisual();
    }

    // ---- Route, break and return ----------------------------------------------------------------------------

    private Vector2 Route(float time) => anchor + HoverMath.PathOffset(settings, time) + Vector2.up * HoverMath.Bob(settings, time);

    private void MoveTo(Vector2 target, float deltaTime) {
        velocity = deltaTime > 0f ? (target - position) / deltaTime : Vector2.zero;
        position = target;
        body.MovePosition(target);
    }

    private void Teleport(Vector2 target) {
        position = target;
        transform.position = target;
        body.position = target;
    }

    private void Rise(float deltaTime) {
        pathTime += deltaTime;
        riseAge += deltaTime;
        float share = Mathf.Clamp01(riseAge / Mathf.Max(0.01f, settings.riseTime));
        float eased = 1f - (1f - share) * (1f - share);
        visualAlpha = Mathf.Clamp01(share * 2f);
        MoveTo(Route(pathTime) - Vector2.up * (settings.riseDistance * (1f - eased)), deltaTime);
        if (share >= 1f) {
            Phase = HoverPhase.Hovering;
            visualAlpha = 1f;
            boardCollider.enabled = true;
        }
    }

    private void Break(Vector2 shotDirection, float side) {
        Phase = HoverPhase.Falling;
        BreakCount++;
        fallAge = 0f;
        tumbleDirection = -side;
        velocity = velocity * FallDrift + shotDirection * 1.5f + Vector2.up * FallPop;
        boardCollider.enabled = false;
        respawnAt = Time.time + settings.respawnDelay;
    }

    // Gravity and a tumble, until it meets the ground (the level, not a body) or has fallen long enough.
    private void Fall(float deltaTime) {
        fallAge += deltaTime;
        velocity.y -= settings.fallGravity * deltaTime;
        Vector2 next = position + velocity * deltaTime;
        float half = settings.boardSize / 2f;
        bool landed = false;
        if (velocity.y < 0f) {
            ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers, useTriggers = false };
            int count = Physics2D.Raycast(position, Vector2.down, filter, groundHits, half - velocity.y * deltaTime);
            for (int index = 0; index < count; index++) {
                if (groundHits[index].collider.attachedRigidbody == null) {
                    next.y = groundHits[index].point.y + half;
                    landed = true;
                    break;
                }
            }
        }
        position = next;
        body.MovePosition(next);
        if (landed || fallAge >= settings.maxFallTime) {
            Phase = HoverPhase.Gone;
            visual.SetActive(false);
        }
    }

    // Back at the start of its route (below it), invisible at first, rising in with the rotor spinning up.
    private void Respawn() {
        Phase = HoverPhase.Rising;
        HitsTaken = 0;
        pathTime = 0f;
        riseAge = 0f;
        spin = 0f;
        tilt = 0f;
        tumble = 0f;
        velocity = Vector2.zero;
        visualAlpha = 0f;
        flashAge = float.PositiveInfinity;
        wobble.Reset(0f);
        isWobbling = false;
        Teleport(Route(0f) - Vector2.up * settings.riseDistance);
        visual.transform.localRotation = Quaternion.identity;
        visual.SetActive(true);
        ApplyVisual();
    }

    // ---- Picture --------------------------------------------------------------------------------------------

    private void BuildVisual() {
        visual = new GameObject("Hover Visual");
        visual.transform.SetParent(transform, false);
        float side = settings.boardSize;
        boardRenderer = Part("Board", HoverTextures.Board(), (new Vector3(0f, 0f, 0f), new Vector3(side, side, 1f)), BoardOrder);
        Vector3 mastBase = new Vector3(0f, side * 0.5f, 0f);
        mastRenderer = Part("Mast", HoverTextures.Mast(), (mastBase, new Vector3(side * 0.6f, side * MastShare / (HoverTextures.MastHeight / HoverTextures.PixelsPerUnit), 1f)), MastOrder);
        Vector3 hubAt = mastBase + new Vector3(0f, side * MastShare, 0f);
        hubRenderer = Part("Hub", HoverTextures.Hub(), (hubAt, new Vector3(side * 0.7f, side * 0.7f, 1f)), HubOrder);
        for (int blade = 0; blade < blades.Length; blade++) {
            blades[blade] = Part("Blade " + blade, HoverTextures.Blade(), (hubAt, new Vector3(side * BladeShare, side * 0.8f, 1f)), BladeOrder);
        }
    }

    private SpriteRenderer Part(string name, Sprite sprite, (Vector3 position, Vector3 scale) placement, int order) {
        GameObject part = new GameObject(name);
        part.transform.SetParent(visual.transform, false);
        part.transform.localPosition = placement.position;
        part.transform.localScale = placement.scale;
        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = LitMaterial();
        renderer.sortingLayerName = EnvironmentLayers.Level;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static Material LitMaterial() {
        if (litMaterial == null) {
            Shader shader = Shader.Find(LitShader) ?? Shader.Find(FallbackShader);
            litMaterial = new Material(shader) { name = "HoverTargetMaterial" };
        }
        return litMaterial;
    }

    // The colours and the blades' swing for this frame: the board flashes back to its own colour, the whole target fades
    // in while rising, and the blades smear while the rotor is fast.
    private void ApplyVisual() {
        float share = float.IsPositiveInfinity(flashAge) || settings.flashDuration <= 0f ? 1f : Mathf.Clamp01(flashAge / settings.flashDuration);
        if (share >= 1f) {
            flashAge = float.PositiveInfinity;
        }
        Color board = Color.Lerp(settings.flashColor, Color.white, share);
        board.a = visualAlpha;
        boardRenderer.color = board;
        Color plain = new Color(1f, 1f, 1f, visualAlpha);
        mastRenderer.color = plain;
        hubRenderer.color = plain;
        for (int blade = 0; blade < blades.Length; blade++) {
            float scale = HoverMath.BladeScale(spinTurns, blade);
            Vector3 size = blades[blade].transform.localScale;
            size.x = settings.boardSize * BladeShare * scale;
            blades[blade].transform.localScale = size;
            blades[blade].color = new Color(1f, 1f, 1f, HoverMath.BladeAlpha(scale, spin) * visualAlpha);
        }
    }
}
