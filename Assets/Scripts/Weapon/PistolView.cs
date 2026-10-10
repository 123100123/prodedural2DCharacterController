using UnityEngine;

// What the sidearm shows right now, for the debug tools.
public readonly struct SidearmReadout {
    public SidearmReadout(bool inHand, Vector2 muzzle) {
        InHand = inHand;
        Muzzle = muzzle;
    }

    public bool InHand { get; }
    public Vector2 Muzzle { get; }
}

// The pistol and its holster as objects in the rig, built at runtime (nothing is authored in the scene):
//   bone_abs (pelvis)  -> Holster -> HolsterSprite
//   bone_forearmF      -> WeaponPivot            (at the end of the forearm, where the hand is)
//   Pistol             -> Muzzle, EjectionPort   (a child of Holster or of WeaponPivot, whichever carries it)
// The rig is mirrored by flipping the root's x scale, so everything under the bones mirrors with it; the pistol is
// aimed and re-parented in world space with that in mind. Posed in Pose, which runs after the arms are posed.
public sealed class PistolView {
    public const string HolsterName = "Holster";
    public const string PivotName = "WeaponPivot";
    public const string PistolName = "Pistol";
    // Sorting orders on the Default layer: holstered it ties with the chest and is told apart by depth (it is nearer the
    // camera than the chest and behind the front thigh, order 6); drawn it is over the front forearm (9) and head (10).
    public const int HolsteredOrder = 5;
    public const int DrawnOrder = 11;
    // How much nearer the camera (z) the holster and the holstered pistol are than the pelvis; the pouch is in front.
    private const float HolsterDepth = 0.02f;
    private const float PistolDepth = 0.01f;
    private const string LitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string FallbackShader = "Sprites/Default";

    private readonly CharacterRig rig;
    private readonly PistolHandlingSettings settings;
    private readonly Transform holster;
    private readonly Transform holsterSprite;
    private readonly Transform weaponPivot;
    private readonly Transform pistol;
    private readonly SpriteRenderer pistolRenderer;
    private readonly Material material;

    private bool inHand;
    private bool hasAim;
    private Vector2 aimDirection;
    private float recoilKick;
    private float swapAge = float.MaxValue;
    private (Vector3 position, float angle) swapFrom;

    public PistolView(CharacterRig rig, PistolHandlingSettings settings) {
        this.rig = rig;
        this.settings = settings;
        Shader shader = Shader.Find(LitShader) ?? Shader.Find(FallbackShader);
        material = new Material(shader) { name = "PistolMaterial" };

        holster = NewChild(HolsterName, rig.PostureRoot);
        holsterSprite = NewChild("HolsterSprite", holster);
        AddSprite(holsterSprite, PistolTextures.HolsterSprite(), HolsteredOrder);
        weaponPivot = NewChild(PivotName, rig.FrontArm.Forearm);
        pistol = NewChild(PistolName, holster);
        pistolRenderer = AddSprite(pistol, PistolTextures.PistolSprite(), HolsteredOrder);
        Muzzle = NewChild("Muzzle", pistol);
        Muzzle.localPosition = PistolTextures.BarrelTip();
        EjectionPort = NewChild("EjectionPort", pistol);
        EjectionPort.localPosition = PistolTextures.EjectionPort();

        ApplyLayout();
        pistol.localPosition = new Vector3(0f, 0f, PistolDepth);
    }

    public Transform Holster => holster;
    public Transform WeaponPivot => weaponPivot;
    public Transform Pistol => pistol;
    // The barrel's tip: shots, the muzzle flash, smoke and sparks start here.
    public Transform Muzzle { get; }
    // Where spent casings leave the pistol.
    public Transform EjectionPort { get; }
    // What carries the pistol now: the Holster or the WeaponPivot.
    public Transform Parent => pistol.parent;
    public bool IsInHand => inHand;
    public SidearmReadout Readout => new SidearmReadout(inHand, Muzzle.position);

    // The direction last given to SetBarrelDirection: where the pistol is aimed, before the recoil twist.
    public Vector2 AimDirection => aimDirection;

    // Where the barrel points in the world, mirror included.
    public Vector2 BarrelDirection => ((Vector2)Muzzle.TransformVector(Vector3.right)).normalized;

    // The direction the pistol is held in while in hand (the aim), and how far the last shot has twisted it up.
    public void SetBarrelDirection(Vector2 worldDirection, float recoil = 0f) {
        if (worldDirection.sqrMagnitude <= 0f) {
            return;
        }
        aimDirection = worldDirection.normalized;
        recoilKick = recoil;
        hasAim = true;
    }

    public void AttachToHolster() => Reparent(holster, false);

    public void AttachToHand() => Reparent(weaponPivot, true);

    // Moves the pistol to a new parent without moving it: its place and the way it points are kept in the world, which
    // takes more than SetParent when the parent chain is mirrored, so the new local pose is worked out from them.
    private void Reparent(Transform parent, bool hand) {
        if (pistol.parent == parent) {
            return;
        }
        Vector3 worldPosition = pistol.position;
        Vector2 forward = pistol.TransformVector(Vector3.right);
        pistol.SetParent(parent, true);
        pistol.localPosition = parent.InverseTransformPoint(worldPosition);
        pistol.localRotation = Quaternion.Euler(0f, 0f, LocalAngleOf(parent, forward));
        pistol.localScale = Vector3.one * settings.pistolScale;
        swapFrom = (pistol.localPosition, LocalAngleOf(parent, forward));
        swapAge = 0f;
        inHand = hand;
        hasAim = false;
        pistolRenderer.sortingOrder = hand ? DrawnOrder : HolsteredOrder;
    }

    // Lays out the fixed parts from the settings and then places the pistol: eased from where it was when it changed
    // parent, and in hand pointing exactly along the aim (twisted by the recoil). Called every frame after the arms.
    public void Pose(float deltaTime) {
        ApplyLayout();
        // The frame it changed parent it stays where it was; the move starts the frame after.
        float blend = settings.attachEaseTime <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, swapAge / settings.attachEaseTime);
        swapAge += deltaTime;
        Vector3 target = new Vector3(0f, 0f, inHand ? 0f : PistolDepth);
        float targetAngle = inHand ? LocalAngleOf(pistol.parent, HeldDirection()) : 0f;
        pistol.localPosition = Vector3.Lerp(swapFrom.position, target, blend);
        pistol.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(swapFrom.angle, targetAngle, blend));
    }

    public void Dispose() {
        Discard(holster.gameObject);
        Discard(weaponPivot.gameObject);
        Discard(pistol.gameObject);
        Discard(material);
    }

    // The aim, twisted up by the recoil (up is anticlockwise facing right and clockwise facing left).
    private Vector2 HeldDirection() {
        if (!hasAim) {
            return weaponPivot.TransformVector(Vector3.right);
        }
        float twist = recoilKick * settings.recoilTwist * rig.Facing.Sign;
        return Rotate(aimDirection, twist);
    }

    private void ApplyLayout() {
        holster.localPosition = new Vector3(settings.holsterOffset.x, settings.holsterOffset.y, -HolsterDepth);
        holster.localRotation = Quaternion.Euler(0f, 0f, settings.holsterAngle);
        holsterSprite.localScale = Vector3.one * settings.pistolScale;
        weaponPivot.localPosition = new Vector3(rig.FrontArm.ForearmLength + settings.gripOffset.x, settings.gripOffset.y, 0f);
        pistol.localScale = Vector3.one * settings.pistolScale;
    }

    // The local angle (degrees) a vector pointing along the parent's world-space direction has in the parent's space,
    // scale and mirror included.
    private static float LocalAngleOf(Transform parent, Vector2 worldDirection) {
        Vector3 local = parent.InverseTransformVector(worldDirection);
        return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees) {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private static Transform NewChild(string name, Transform parent) {
        GameObject made = new GameObject(name);
        made.transform.SetParent(parent, false);
        return made.transform;
    }

    private SpriteRenderer AddSprite(Transform host, Sprite sprite, int order) {
        SpriteRenderer renderer = host.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = order;
        return renderer;
    }

    // Edit-mode tests build the view outside play mode, where Destroy is not allowed.
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
}
