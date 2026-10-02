using System;
using UnityEngine;

// Moves the body by velocity while a capsule hull (hips to head) slides along solid geometry.
[Serializable]
public class KinematicBodyMotor {
    private const float SkinWidth = 0.02f;

    [Tooltip("Hull size: covers the torso and head, not the legs (feet handle ground contact).")]
    [SerializeField] private Vector2 hullSize = new Vector2(0.8f, 2.45f);
    [Tooltip("Hull centre relative to the body pivot.")]
    [SerializeField] private Vector2 hullOffset = new Vector2(0f, 0.93f);
    [SerializeField] private float gravity = 30f;
    [SerializeField] private LayerMask solidMask = Physics2D.DefaultRaycastLayers;

    private readonly RaycastHit2D[] hullHits = new RaycastHit2D[8];
    private Transform body;

    public Vector2 Velocity { get; set; }
    public float Gravity => gravity;
    public LayerMask SolidMask => solidMask;
    public float HullHalfWidth => hullSize.x / 2f;

    public void Bind(Transform movedBody) => body = movedBody;

    public void ApplyGravity(float deltaTime) => Velocity += Vector2.down * (gravity * deltaTime);

    // Moves horizontally then vertically; a blocked axis loses its velocity.
    public void MoveWithCollisions(float deltaTime) {
        Vector2 delta = Velocity * deltaTime;
        float movedX = MoveAlongAxis(Vector2.right * Mathf.Sign(delta.x), Mathf.Abs(delta.x));
        float movedY = MoveAlongAxis(Vector2.up * Mathf.Sign(delta.y), Mathf.Abs(delta.y));

        Velocity = new Vector2(
            Mathf.Approximately(movedX, Mathf.Abs(delta.x)) ? Velocity.x : 0f,
            Mathf.Approximately(movedY, Mathf.Abs(delta.y)) ? Velocity.y : 0f);
    }

    // How far the hull can travel sideways before touching geometry, up to maxDistance.
    public float FreeDistance(float direction, float maxDistance) {
        RaycastHit2D hit = CastHull((Vector2.right * Mathf.Sign(direction), maxDistance + SkinWidth));
        return hit.collider == null ? maxDistance : Mathf.Max(0f, hit.distance - SkinWidth);
    }

    private float MoveAlongAxis(Vector2 direction, float distance) {
        if (distance <= 0f) {
            return 0f;
        }

        float allowed = FreeTravel((direction, distance));
        body.position += (Vector3)(direction * allowed);
        return allowed;
    }

    private float FreeTravel((Vector2 direction, float distance) motion) {
        RaycastHit2D hit = CastHull((motion.direction, motion.distance + SkinWidth));
        return hit.collider == null ? motion.distance : Mathf.Max(0f, hit.distance - SkinWidth);
    }

    // Geometry the hull already overlaps (distance 0) does not block, so the hull can move out instead of sticking,
    // and it must not hide real obstacles further along the cast.
    private RaycastHit2D CastHull((Vector2 direction, float distance) cast) {
        Vector2 center = (Vector2)body.position + hullOffset;
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask };
        int hitCount = Physics2D.CapsuleCast(center, hullSize, CapsuleDirection2D.Vertical, 0f, cast.direction, filter, hullHits, cast.distance);

        RaycastHit2D nearest = default;
        for (int index = 0; index < hitCount; index++) {
            RaycastHit2D hit = hullHits[index];
            if (hit.distance > 0f && (nearest.collider == null || hit.distance < nearest.distance)) {
                nearest = hit;
            }
        }
        return nearest;
    }
}
