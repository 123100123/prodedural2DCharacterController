using System.Collections.Generic;
using UnityEngine;

// What a stepping leg needs to know to judge a foothold.
public struct FootholdRequest {
    public float DesiredX;
    // Where the hip will be horizontally when the foot touches down.
    public float LandingHipX;
    public Vector2 PlantedFoot;
    public float HipHeight;
    public float LegLength;
    public float ForwardSign;
}

// Picks the cheapest reachable foothold: closest to the desired spot, least height change, never out of leg reach.
public static class FootholdPlanner {
    private const float ForwardTieBreak = 0.01f;

    public static bool TryChooseFoothold(
        FootholdRequest request,
        (IReadOnlyList<Vector2> candidates, FootholdLimits limits) search,
        out Vector2 foothold) {
        float bestScore = float.PositiveInfinity;
        foothold = default;

        foreach (Vector2 candidate in search.candidates) {
            if (!IsAcceptable(candidate, (request, search.limits))) {
                continue;
            }

            float score = ScoreFoothold(candidate, (request, search.limits));
            if (score < bestScore) {
                bestScore = score;
                foothold = candidate;
            }
        }
        return !float.IsPositiveInfinity(bestScore);
    }

    public static bool IsAcceptable(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        return IsWithinStepWindow(candidate, context)
            && IsWithinLegReach(candidate, context)
            && IsLongEnoughStep(candidate, context);
    }

    private static bool IsWithinStepWindow(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        float rise = candidate.y - context.request.PlantedFoot.y;
        return rise <= context.limits.maxStepUp && rise >= -context.limits.maxStepDown;
    }

    private static bool IsWithinLegReach(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        float reach = Vector2.Distance(PredictSettledHip(candidate, context.request), candidate);
        return reach <= context.request.LegLength * context.limits.maxReachStretch;
    }

    private static bool IsLongEnoughStep(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        return Mathf.Abs(candidate.x - context.request.PlantedFoot.x) >= context.limits.minStepLength;
    }

    // At touchdown the hip is at the gait's predicted x, at the mean foot height plus the current hip height.
    private static Vector2 PredictSettledHip(Vector2 candidate, FootholdRequest request) {
        float hipY = (request.PlantedFoot.y + candidate.y) / 2f + request.HipHeight;
        return new Vector2(request.LandingHipX, hipY);
    }

    private static float ScoreFoothold(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        float sideways = candidate.x - context.request.DesiredX;
        float heightChange = Mathf.Abs(candidate.y - context.request.PlantedFoot.y);
        float forwardBonus = ForwardTieBreak * context.request.ForwardSign * sideways;

        return Mathf.Abs(sideways) + context.limits.heightWeight * heightChange - forwardBonus;
    }
}
