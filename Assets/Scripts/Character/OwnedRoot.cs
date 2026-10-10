using UnityEngine;

// Marks a scene object that a CharacterDriver built at runtime (the tracers, effects and audio roots, and the holster,
// weapon pivot and pistol hanging under the rig's bones) and which driver owns it. The mark is serialized, so after a
// script reload in play mode, which drops the collaborators but keeps their scene objects, the driver can still find
// and destroy what its previous composition built, wherever in the hierarchy it hangs.
public sealed class OwnedRoot : MonoBehaviour {
    [SerializeField] private Transform owner;
    // Set when it is being destroyed (Destroy takes effect at the end of the frame), so a second composition in the same
    // frame neither counts it nor destroys it twice.
    private bool destroying;

    // Marks the object once (a second mark only changes the owner).
    public static void Mark(GameObject root, Transform owner) {
        OwnedRoot mark = root.GetComponent<OwnedRoot>();
        (mark != null ? mark : root.AddComponent<OwnedRoot>()).owner = owner;
    }

    // How many marked objects the owner has: for tests and tools.
    public static int CountOwnedBy(Transform owner) {
        int count = 0;
        foreach (OwnedRoot root in FindObjectsByType<OwnedRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (root.owner == owner && !root.destroying) {
                count++;
            }
        }
        return count;
    }

    public static void DestroyOwnedBy(Transform owner) {
        foreach (OwnedRoot root in FindObjectsByType<OwnedRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (root.owner == owner && !root.destroying) {
                root.destroying = true;
                Destroy(root.gameObject);
            }
        }
    }
}
