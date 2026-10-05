using System;
using System.Collections.Generic;
using UnityEngine;

// Tunables for choosing where a stepping foot may land, measured from the planted foot.
[Serializable]
public struct FootholdLimits {
    [Tooltip("Highest accepted foothold above the planted foot.")]
    public float maxStepUp;
    [Tooltip("Lowest accepted foothold below the planted foot. Anything deeper (e.g. the floor under a gap) is ignored.")]
    public float maxStepDown;
    [Tooltip("Accepted hip-to-foothold distance as a multiple of leg length, after the body settles.")]
    public float maxReachStretch;
    [Tooltip("How far sideways from the desired spot to look for ground.")]
    public float searchRadius;
    public float searchStep;
    [Tooltip("Cost of one unit of height change relative to one unit of sideways shift.")]
    public float heightWeight;
    [Tooltip("Steps shorter than this are skipped instead of shuffling in place.")]
    public float minStepLength;
    public LayerMask groundMask;

    public static FootholdLimits Default => new FootholdLimits {
        maxStepUp = 1.0f,
        maxStepDown = 1.3f,
        maxReachStretch = 1.2f,
        searchRadius = 0.8f,
        searchStep = 0.05f,
        heightWeight = 0.5f,
        minStepLength = 0.15f,
        groundMask = Physics2D.DefaultRaycastLayers,
    };
}

// Physics queries for footholds: samples ground tops inside the step window around a desired x
// that the foot can swing to without passing through terrain.
public static class FootholdProbe {
    private const float RayHeadroom = 0.05f;
    private const float SwingClearance = 0.1f;

    // Samples run from the desired spot back to the swinging foot (so a shorter step onto an obstacle is found)
    // and searchRadius beyond it (so a gap's far edge is found). Nearest samples come first.
    public static void CollectCandidates((float desiredX, float stanceY, Vector2 swingFrom) query, FootholdLimits limits, List<Vector2> candidates) {
        CollectCandidates(query, (limits, OneWaySupport.At(query.stanceY + OneWayFilter.StandingTolerance)), candidates);
    }

    // support: the one-way platforms that may hold the feet (those at or below the planted foot: a step never climbs onto
    // one, and a foot never lands on one it is below).
    public static void CollectCandidates((float desiredX, float stanceY, Vector2 swingFrom) query, (FootholdLimits limits, OneWaySupport support) search, List<Vector2> candidates) {
        FootholdLimits limits = search.limits;
        candidates.Clear();
        float towardSwingSign = Mathf.Sign(query.swingFrom.x - query.desiredX);
        int towardSwingCount = Mathf.FloorToInt(Mathf.Abs(query.swingFrom.x - query.desiredX) / limits.searchStep);
        int beyondCount = Mathf.FloorToInt(limits.searchRadius / limits.searchStep);

        for (int sample = 0; sample <= Mathf.Max(towardSwingCount, beyondCount); sample++) {
            float offset = sample * limits.searchStep;
            if (sample <= towardSwingCount) {
                AddReachableCandidate((query.desiredX + towardSwingSign * offset, query.stanceY, query.swingFrom), search, candidates);
            }
            if (sample > 0 && sample <= beyondCount) {
                AddReachableCandidate((query.desiredX - towardSwingSign * offset, query.stanceY, query.swingFrom), search, candidates);
            }
        }
    }

    private static void AddReachableCandidate((float x, float stanceY, Vector2 swingFrom) column, (FootholdLimits limits, OneWaySupport support) search, List<Vector2> candidates) {
        if (TryFindGroundInStepWindow((column.x, column.stanceY), search, out Vector2 groundPoint)
            && IsSwingPathClear((column.swingFrom, groundPoint), search.limits.groundMask)) {
            candidates.Add(groundPoint);
        }
    }

    // A swing is blocked when terrain rises above both ends between them, e.g. a stair block in front of the floor beyond it.
    public static bool IsSwingPathClear((Vector2 from, Vector2 to) swing, LayerMask groundMask) {
        float swingHeight = Mathf.Max(swing.from.y, swing.to.y) + SwingClearance;
        Vector2 start = new Vector2(swing.from.x, swingHeight);
        Vector2 end = new Vector2(swing.to.x, swingHeight);

        return !OneWayFilter.Linecast((start, end), groundMask, OneWaySupport.None);
    }

    // Any one-way platform below the ray's start holds it.
    public static bool TryFindGroundBelow((Vector2 origin, float maxDistance) ray, LayerMask groundMask, out Vector2 groundPoint) {
        return TryFindGroundBelow(ray, (groundMask, OneWaySupport.At(ray.origin.y)), out groundPoint);
    }

    // ground.support: the one-way platforms that hold the feet this ray stands for (see OneWaySupport).
    public static bool TryFindGroundBelow((Vector2 origin, float maxDistance) ray, (LayerMask mask, OneWaySupport support) ground, out Vector2 groundPoint) {
        bool isGroundHit = TryFindGroundHitBelow(ray, ground, out RaycastHit2D hit);
        groundPoint = isGroundHit ? hit.point : default;
        return isGroundHit;
    }

    // The ground hit itself, for what the ground is (e.g. a one-way platform to drop through).
    public static bool TryFindGroundHitBelow((Vector2 origin, float maxDistance) ray, (LayerMask mask, OneWaySupport support) ground, out RaycastHit2D hit) {
        hit = OneWayFilter.Raycast((ray.origin, Vector2.down, ray.maxDistance), ground.mask, ground.support);
        return hit.collider != null && hit.distance > 0f;
    }

    // A ray that starts inside a collider is rejected: that column is blocked above the step window.
    private static bool TryFindGroundInStepWindow((float x, float stanceY) column, (FootholdLimits limits, OneWaySupport support) search, out Vector2 groundPoint) {
        FootholdLimits limits = search.limits;
        Vector2 origin = new Vector2(column.x, column.stanceY + limits.maxStepUp + RayHeadroom);
        float windowHeight = limits.maxStepUp + limits.maxStepDown + 2f * RayHeadroom;

        return TryFindGroundBelow((origin, windowHeight), (limits.groundMask, search.support), out groundPoint);
    }
}
