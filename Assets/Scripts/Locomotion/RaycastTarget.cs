using UnityEngine;

// Snaps the desired target's height onto the ground directly below it.
public class RaycastTarget : MonoBehaviour {
    private const float RayStartHeightAboveBody = 5f;
    private const float RayLength = 12f;

    [SerializeField] private Transform desiredTarget;

    private void Update() {
        desiredTarget.position = new Vector2(desiredTarget.position.x, GroundHeightBelowTarget());
    }

    // Falls back to this transform's height when no ground is found.
    private float GroundHeightBelowTarget() {
        Vector2 rayOrigin = new Vector2(desiredTarget.position.x, transform.position.y + RayStartHeightAboveBody);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, RayLength);

        return hit.collider != null ? hit.point.y : transform.position.y;
    }
}
