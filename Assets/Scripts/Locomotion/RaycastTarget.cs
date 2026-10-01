using UnityEngine;

// Keeps the desired target on reachable ground below this transform, holding the last valid foothold when none is found.
// Runs after StrideController places targets and before LegStepper follows them.
[DefaultExecutionOrder(10)]
public class RaycastTarget : MonoBehaviour {
    [SerializeField] private Transform desiredTarget;
    [Tooltip("Move the target sideways onto the found ledge. Disable for input-driven targets, which only take its height.")]
    [SerializeField] private bool snapToFoundLedge = true;
    [SerializeField] private FootholdLimits limits = FootholdLimits.Default;

    private Vector2 lastValidFoothold;

    private void Awake() {
        lastValidFoothold = desiredTarget.position;
    }

    private void Update() {
        Vector2 foothold = FindReachableFoothold();
        desiredTarget.position = snapToFoundLedge ? foothold : new Vector2(desiredTarget.position.x, foothold.y);
    }

    private Vector2 FindReachableFoothold() {
        float probeX = desiredTarget.position.x;
        float forwardSign = Mathf.Sign(probeX - transform.position.x);

        if (FootholdProbe.TryFindFoothold((probeX, transform.position.y, forwardSign), limits, out Vector2 foothold)) {
            lastValidFoothold = foothold;
        }
        return lastValidFoothold;
    }
}
