using UnityEngine;

// Pure gait geometry: how high the pelvis can be while a planted foot still reaches the ground.
// With the feet spread the pelvis must drop (contact, the low point); over a single straight support leg it rises
// (passing, the high point) - the inverted-pendulum bob of a weighted walk.
public static class GaitPose {
    public static float HighestPelvisY((Vector2 foot, float pelvisX) support, (Vector2 hipOffset, float reach) leg) {
        float hipX = support.pelvisX + leg.hipOffset.x;
        float horizontal = Mathf.Min(Mathf.Abs(hipX - support.foot.x), leg.reach);
        float hipAboveFoot = Mathf.Sqrt(leg.reach * leg.reach - horizontal * horizontal);
        return support.foot.y + hipAboveFoot - leg.hipOffset.y;
    }
}
