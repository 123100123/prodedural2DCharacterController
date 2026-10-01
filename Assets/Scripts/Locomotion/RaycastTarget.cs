using UnityEngine;

// Legacy debug marker: drops the target onto the ground below it. Locomotion no longer reads these markers
// (footholds are planned by FootholdPlanner), so the component and its marker objects can be removed from the scene.
public class RaycastTarget : MonoBehaviour {
    private const float RayStartHeight = 1f;
    private const float RayLength = 6f;

    [SerializeField] private Transform desiredTarget;

    private void LateUpdate() {
        Vector2 origin = new Vector2(desiredTarget.position.x, transform.position.y + RayStartHeight);
        if (FootholdProbe.TryFindGroundBelow((origin, RayLength), Physics2D.DefaultRaycastLayers, out Vector2 ground)) {
            desiredTarget.position = new Vector2(desiredTarget.position.x, ground.y);
        }
    }
}
