using System.Collections.Generic;
using UnityEngine;

// Colliders the character moves through as if they were not there, while shots and physics bodies still meet them: a
// flying target hung over the walking path. The character moves by queries, not physics, so its motor, foot probes, wall
// and ledge searches and push probe ask OneWayFilter about every hit, and OneWayFilter skips what is registered here. A
// collider is registered by its owner while enabled (HoverTarget); a hit is looked up in a set, never by a component
// search. With nothing registered every query is the plain physics query it replaced.
public static class CharacterPassThrough {
    private static readonly HashSet<Collider2D> registered = new HashSet<Collider2D>();

    // Whether anything is registered: the queries skip their filtering entirely when nothing is (and no one-way platform is).
    public static bool AnyActive => registered.Count > 0;

    public static void Register(Collider2D collider) {
        if (collider != null) {
            registered.Add(collider);
        }
    }

    public static void Unregister(Collider2D collider) {
        if (collider != null) {
            registered.Remove(collider);
        }
    }

    public static bool Contains(Collider2D collider) => collider != null && registered.Count > 0 && registered.Contains(collider);

    // Clears the registry when play starts without a domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => registered.Clear();
}
