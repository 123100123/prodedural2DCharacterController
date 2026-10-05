using System.Collections.Generic;
using UnityEngine;

// Makes the colliders on its object one-way platforms: the character lands on them from above and passes through them
// from below and from the sides (and drops through with down + jump). The character moves by queries, not physics, so
// its motor, foot probes, landing and ledge searches ask OneWayFilter about every hit on these colliders. Physics bodies
// (shell casings, crates) get the same rule from a PlatformEffector2D added at runtime.
[DisallowMultipleComponent]
public sealed class OneWayPlatform : MonoBehaviour {
    // Surface arc of the effector: contacts within this many degrees of straight up collide.
    private const float EffectorSurfaceArc = 160f;

    private static readonly HashSet<Collider2D> active = new HashSet<Collider2D>();

    [Tooltip("Also let physics bodies (casings, crates) pass up through it, through a PlatformEffector2D added at runtime.")]
    [SerializeField] private bool oneWayForPhysicsBodies = true;

    private readonly List<Collider2D> colliders = new List<Collider2D>();

    // Whether any one-way platform is enabled: the queries skip their filtering entirely when none is.
    public static bool AnyActive => active.Count > 0;

    public static bool IsOneWay(Collider2D collider) => collider != null && active.Count > 0 && active.Contains(collider);

    // Clears the registry when play starts without a domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();

    private void OnEnable() {
        GetComponents(colliders);
        foreach (Collider2D platformCollider in colliders) {
            active.Add(platformCollider);
        }
        if (oneWayForPhysicsBodies && Application.isPlaying) {
            ConfigureEffector();
        }
    }

    private void OnDisable() {
        foreach (Collider2D platformCollider in colliders) {
            active.Remove(platformCollider);
        }
        colliders.Clear();
    }

    private void ConfigureEffector() {
        if (!TryGetComponent(out PlatformEffector2D effector)) {
            effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = EffectorSurfaceArc;
        }
        foreach (Collider2D platformCollider in colliders) {
            platformCollider.usedByEffector = true;
        }
    }
}
