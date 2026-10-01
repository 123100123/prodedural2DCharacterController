using System;
using UnityEngine;

// Vertical window around the probe origin in which ground counts as reachable, plus the sideways search extent.
[Serializable]
public struct FootholdLimits {
    [Tooltip("Highest accepted surface above the probe origin.")]
    public float maxRise;
    [Tooltip("Lowest accepted surface below the probe origin. Anything deeper (e.g. the floor under a gap) is ignored.")]
    public float maxDrop;
    [Tooltip("How far sideways to look for a ledge when nothing is reachable directly below.")]
    public float searchRadius;
    public float searchStep;
    public LayerMask groundMask;

    public static FootholdLimits Default => new FootholdLimits {
        maxRise = 1.0f,
        maxDrop = 3.2f,
        searchRadius = 0.8f,
        searchStep = 0.05f,
        groundMask = Physics2D.DefaultRaycastLayers,
    };
}

// Finds reachable ground for a foot: directly below the probe, otherwise the nearest ledge, searching forward first.
public static class FootholdProbe {
    public static bool TryFindFoothold(
        (float probeX, float originY, float forwardSign) request,
        FootholdLimits limits,
        out Vector2 foothold) {
        if (TryFindGroundBelow((request.probeX, request.originY), limits, out foothold)) {
            return true;
        }
        if (TrySearchSideways((request.probeX, request.originY, request.forwardSign), limits, out foothold)) {
            return true;
        }
        return TrySearchSideways((request.probeX, request.originY, -request.forwardSign), limits, out foothold);
    }

    private static bool TrySearchSideways(
        (float probeX, float originY, float direction) search,
        FootholdLimits limits,
        out Vector2 foothold) {
        int stepCount = Mathf.FloorToInt(limits.searchRadius / limits.searchStep);

        for (int step = 1; step <= stepCount; step++) {
            float x = search.probeX + search.direction * step * limits.searchStep;
            if (TryFindGroundBelow((x, search.originY), limits, out foothold)) {
                return true;
            }
        }

        foothold = default;
        return false;
    }

    // A ray that starts inside a collider (distance 0) is rejected: that surface is overhead, not underfoot.
    private static bool TryFindGroundBelow((float x, float originY) probe, FootholdLimits limits, out Vector2 foothold) {
        Vector2 rayOrigin = new Vector2(probe.x, probe.originY + limits.maxRise);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, limits.maxRise + limits.maxDrop, limits.groundMask);

        bool isReachable = hit.collider != null && hit.distance > 0f;
        foothold = isReachable ? hit.point : default;
        return isReachable;
    }
}
