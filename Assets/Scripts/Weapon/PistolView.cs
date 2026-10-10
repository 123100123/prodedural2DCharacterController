using UnityEngine;

// The pistol and its holster as objects in the rig, built at runtime (nothing is authored in the scene):
//   bone_thighF (front thigh) -> Holster -> HolsterSprite   (a drop-leg holster on the outside of the front thigh)
//   bone_forearmF             -> WeaponPivot            (at the end of the forearm, where the hand is)
//   Pistol                    -> Muzzle, EjectionPort   (a child of Holster or of WeaponPivot, whichever carries it)
// The holster swings with the front leg; the hand reaches for it where it is each frame.
// The rig is mirrored by flipping the root's x scale, so everything under the bones mirrors with it; the pistol is
// aimed and re-parented in world space with that in mind. Posed in Pose, which runs after the arms are posed.
public sealed class PistolView {
    public const string HolsterName = "Holster";
    public const string PivotName = "WeaponPivot";
    public const string PistolName = "Pistol";
    // Sorting orders on the Default layer: holstered it is over the front thigh (6) and ties with the front calf (ankleF,
    // 7), in front of it by depth (nearer the camera), and under the front arm (8, 9); drawn it is over the front forearm
    // (9) and head (10).
    public const int HolsteredOrder = 7;
    public const int DrawnOrder = 11;
    // How much nearer the camera (z) the holster and the holstered pistol are than the thigh; the pouch is in front.
    private const float HolsterDepth = 0.02f;
    private const float PistolDepth = 0.01f;
    // The seat share below which the held pistol has left the holster's mouth and is drawn over the arm.
    private const float OutOfThePouch = 0.5f;
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

        holster = NewChild(HolsterName, rig.Stride.FrontLeg.Hip);
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

    // Where the holstered pistol's grip sits in the world, and the way its barrel points there: what the hand reaches
    // for on the draw and brings the pistol back to on the holster.
    public Vector2 HolsterGrip => holster.TransformPoint(new Vector3(0f, 0f, PistolDepth));
    public Vector2 HolsteredDirection => ((Vector2)holster.TransformVector(Vector3.right)).normalized;

    // How far the pistol held in the hand is pulled onto the holstered grip (1 at the grip and the insert, 0 clear of the
    // holster); set every frame by the holder.
    public float SeatShare { get; set; }

    // The direction last given to SetBarrelDirection: where the pistol is aimed, before the recoil twist.
    public Vector2 AimDirection => aimDirection;

    // Where the barrel points in the world, mirror included.
    public Vector2 BarrelDirection => ((Vector2)Muzzle.TransformVector(Vector3.right)).normalized;

    // The direction the pistol is held in while in hand (the aim, or the turn toward it on the draw), and how far the last
    // shot has twisted it up. Set every frame it is in hand.
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
        // Held at the holster's mouth, the pistol is seated on its grip there (the hand may still be arriving), so the
        // insert and the grip happen where the pistol already is.
        if (inHand && SeatShare > 0f) {
            pistol.position = Vector3.Lerp(pistol.position, holster.TransformPoint(new Vector3(0f, 0f, PistolDepth)), Mathf.Clamp01(SeatShare));
        }
        // Still in the pouch it stays behind the leather's order (and under the gripping hand); clear of it, over the arm.
        pistolRenderer.sortingOrder = inHand && SeatShare < OutOfThePouch ? DrawnOrder : HolsteredOrder;
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
