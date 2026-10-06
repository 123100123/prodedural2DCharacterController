using UnityEngine;

// The one rule every character query applies to one-way platforms, and the physics queries that apply it. A hit on a
// one-way platform blocks only a query moving down onto its top from feet at or above that top (OneWaySupport); it never
// blocks a query moving up or sideways, or one that starts inside it. Hits on every other collider block as before, so
// with no one-way platform and no pass-through collider (CharacterPassThrough) in the scene each query is the plain physics query it replaced.
public static class OneWayFilter {
    // A surface counts as a platform's top when its normal points at least this much up.
    public const float MinTopNormalY = 0.5f;
    // Feet standing on a top sit within this of it (floating-point and step settling), so the top still holds them.
    public const float StandingTolerance = 0.05f;
    private const int MaxHits = 8;

    private static readonly RaycastHit2D[] hits = new RaycastHit2D[MaxHits];
    private static readonly Collider2D[] overlaps = new Collider2D[MaxHits];

    // The rule itself. platform: whether the hit collider is one-way, and whether the query is dropping through it.
    // hit: how far along the query it was (0 when the query starts inside it), its normal's y and its height.
    // query: the vertical direction of the query and the level of the feet it stands for.
    public static bool Blocks((bool isOneWay, bool isIgnored) platform, (float distance, float normalY, float pointY) hit, (float directionY, float supportLevel) query) {
        if (!platform.isOneWay) {
            return true;
        }
        if (platform.isIgnored || hit.distance <= 0f || query.directionY >= 0f || hit.normalY < MinTopNormalY) {
            return false;
        }
        return hit.pointY <= query.supportLevel;
    }

    public static bool Blocks(RaycastHit2D hit, Vector2 direction, OneWaySupport support) {
        if (CharacterPassThrough.Contains(hit.collider)) {
            return false;
        }
        bool isOneWay = OneWayPlatform.IsOneWay(hit.collider);
        return Blocks((isOneWay, isOneWay && support.Ignores(hit.collider)), (hit.distance, hit.normal.y, hit.point.y), (direction.y, support.Level));
    }

    // The nearest hit along the ray that blocks it; no collider when nothing does. A solid collider the ray starts inside
    // blocks at distance 0, as a plain raycast reports it.
    public static RaycastHit2D Raycast((Vector2 origin, Vector2 direction, float distance) ray, LayerMask mask, OneWaySupport support) {
        if (!AnyFiltering) {
            return Physics2D.Raycast(ray.origin, ray.direction, ray.distance, mask);
        }
        int count = Physics2D.Raycast(ray.origin, ray.direction, QueryFilter(mask), hits, ray.distance);
        return NearestBlocking((count, ray.direction), support);
    }

    // Whether anything that blocks lies on the segment (one-way platforms never block a sideways or upward segment).
    public static bool Linecast((Vector2 start, Vector2 end) segment, LayerMask mask, OneWaySupport support) {
        if (!AnyFiltering) {
            return Physics2D.Linecast(segment.start, segment.end, mask).collider != null;
        }
        int count = Physics2D.Linecast(segment.start, segment.end, QueryFilter(mask), hits);
        return NearestBlocking((count, segment.end - segment.start), support).collider != null;
    }

    // Whether a collider other than a one-way platform or a pass-through collider covers the point.
    public static bool OverlapsSolidPoint(Vector2 point, LayerMask mask) {
        if (!AnyFiltering) {
            return Physics2D.OverlapPoint(point, mask) != null;
        }
        return AnySolid(Physics2D.OverlapPoint(point, QueryFilter(mask), overlaps));
    }

    // Whether a collider other than a one-way platform or a pass-through collider overlaps the box.
    public static bool OverlapsSolidBox((Vector2 center, Vector2 size) box, LayerMask mask) {
        if (!AnyFiltering) {
            return Physics2D.OverlapBox(box.center, box.size, 0f, mask) != null;
        }
        return AnySolid(Physics2D.OverlapBox(box.center, box.size, 0f, QueryFilter(mask), overlaps));
    }

    // Whether any of the first count colliders is neither a one-way platform nor a pass-through collider.
    public static bool AnySolid(Collider2D[] found, int count) {
        for (int index = 0; index < count; index++) {
            if (!IsSkipped(found[index])) {
                return true;
            }
        }
        return false;
    }

    // Whether the collider never stops a hull or a push: a one-way platform (the feet decide about its top) or a collider the
    // character passes through.
    public static bool IsSkipped(Collider2D collider) => OneWayPlatform.IsOneWay(collider) || CharacterPassThrough.Contains(collider);

    private static bool AnyFiltering => OneWayPlatform.AnyActive || CharacterPassThrough.AnyActive;

    private static bool AnySolid(int count) => AnySolid(overlaps, count);

    // The same colliders the plain queries find: the layer mask, and triggers as the physics settings say.
    private static ContactFilter2D QueryFilter(LayerMask mask) {
        return new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = Physics2D.queriesHitTriggers };
    }

    private static RaycastHit2D NearestBlocking((int count, Vector2 direction) found, OneWaySupport support) {
        RaycastHit2D nearest = default;
        for (int index = 0; index < found.count; index++) {
            RaycastHit2D hit = hits[index];
            if (Blocks(hit, found.direction, support) && (nearest.collider == null || hit.distance < nearest.distance)) {
                nearest = hit;
            }
        }
        return nearest;
    }
}
