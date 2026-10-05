using UnityEngine;

// Marks a scene object that a CharacterDriver built at runtime (the tracers, effects and audio roots) and which driver owns
// it. The mark is serialized, so after a script reload in play mode, which drops the collaborators but keeps their scene
// objects, the driver can still find and destroy the roots of its previous composition.
public sealed class OwnedRoot : MonoBehaviour {
    [SerializeField] private Transform owner;

    public static void Mark(GameObject root, Transform owner) => root.AddComponent<OwnedRoot>().owner = owner;

    public static void DestroyOwnedBy(Transform owner) {
        foreach (OwnedRoot root in FindObjectsByType<OwnedRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (root.owner == owner) {
                Destroy(root.gameObject);
            }
        }
    }
}
