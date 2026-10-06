using UnityEngine;

// How far the grounded body and its feet may travel sideways before geometry stops them.
public static class WallSpans {
    // The body may travel as far as its hull is free, and no further than a pushable crate in front of its legs (one the
    // hull, hips to head, would pass over); feet may reach the face itself, a hull half-width further. A slope the
    // character can walk up is ground, not a wall: the hull passes over it.
    public static ((float min, float max) feet, (float min, float max) body) Within(CharacterRig rig) {
        float bodyX = rig.Body.position.x;
        float lookAhead = rig.Stride.WallProbeDistance;

        (float min, float max) body = (bodyX - FreeDistance(rig, (-1, lookAhead)), bodyX + FreeDistance(rig, (1, lookAhead)));
        float hullHalfWidth = rig.Motor.HullHalfWidth;
        return ((body.min - hullHalfWidth, body.max + hullHalfWidth), body);
    }

    private static float FreeDistance(CharacterRig rig, (int direction, float lookAhead) side) {
        float hull = rig.Motor.FreeDistance(side.direction, (side.lookAhead, rig.Stride.Slope.maxSlopeAngle));
        return PushProbe.TryFind(rig, (side.direction, hull), out PushProbe.Hit crate) ? Mathf.Min(hull, crate.FreeDistance) : hull;
    }
}
