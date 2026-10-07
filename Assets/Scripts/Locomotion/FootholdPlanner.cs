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
    // The planted foot must stay within this distance of the hip until it may lift (0: not checked): where the hip will be
    // horizontally then, and how far the pelvis may sink below its predicted height to keep it.
    public float SupportReach;
    public float SupportHipX;
    public float SupportSink;
    // On a slope: the ground under LandingHipX, and how high the hip rides above the ground under it. Without it the
    // hip settles at the mean height of the feet plus HipHeight (flat ground).
    public bool HasLandingGround;
    public float LandingGroundY;
    public float HipAboveGround;
}

// Picks the cheapest reachable foothold: closest to the desired spot, least height change, never out of leg reach, and
// preferring footholds that keep the planted foot within reach until it may lift.
public static class FootholdPlanner {
    private const float ForwardTieBreak = 0.01f;

    // A foothold that would hold the planted foot beyond its reach is passed over for one that does not; if none does,
    // the nearest foothold is still taken (the planted foot is then released at full extension), since no step at all
    // would leave the body to fall past its feet.
    public static bool TryChooseFoothold(
        FootholdRequest request,
        (IReadOnlyList<Vector2> candidates, FootholdLimits limits) search,
        out Vector2 foothold) {
        return TryChooseFoothold((request, true), search, out foothold) || TryChooseFoothold((request, false), search, out foothold);
    }

    private static bool TryChooseFoothold(
        (FootholdRequest request, bool keepSupport) choice,
        (IReadOnlyList<Vector2> candidates, FootholdLimits limits) search,
        out Vector2 foothold) {
        FootholdRequest request = choice.request;
        float bestScore = float.PositiveInfinity;
        foothold = default;

        foreach (Vector2 candidate in search.candidates) {
            if (!IsAcceptable(candidate, (request, search.limits)) || (choice.keepSupport && !KeepsSupportInReach(candidate, request))) {
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

    // Until the planted foot may lift, the hip is at SupportHipX at the height the step settles it to (PredictSettledHip),
    // lowered by as much as the pelvis may sink: the planted foot must be within SupportReach of it (SlopeReach.Sink).
    public static bool KeepsSupportInReach(Vector2 candidate, FootholdRequest request) {
        if (request.SupportReach <= 0f) {
            return true;
        }
        Vector2 hip = PredictSettledHip(candidate, request);
        float hipAboveFoot = hip.y - request.PlantedFoot.y;
        float sink = SlopeReach.Sink((request.SupportHipX - request.PlantedFoot.x, 0f), (hipAboveFoot, request.SupportReach));
        return sink <= request.SupportSink;
    }

    private static bool IsLongEnoughStep(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        return Mathf.Abs(candidate.x - context.request.PlantedFoot.x) >= context.limits.minStepLength;
    }

    // At touchdown the hip is at the gait's predicted x, at the mean foot height plus the current hip height; on a slope,
    // at its height above the ground under it (the body rides the ground under the pelvis there, not the feet).
    private static Vector2 PredictSettledHip(Vector2 candidate, FootholdRequest request) {
        float hipY = request.HasLandingGround
            ? request.LandingGroundY + request.HipAboveGround
            : (request.PlantedFoot.y + candidate.y) / 2f + request.HipHeight;
        return new Vector2(request.LandingHipX, hipY);
    }

    private static float ScoreFoothold(Vector2 candidate, (FootholdRequest request, FootholdLimits limits) context) {
        float sideways = candidate.x - context.request.DesiredX;
        float heightChange = Mathf.Abs(candidate.y - context.request.PlantedFoot.y);
        float forwardBonus = ForwardTieBreak * context.request.ForwardSign * sideways;

        return Mathf.Abs(sideways) + context.limits.heightWeight * heightChange - forwardBonus;
    }
}
