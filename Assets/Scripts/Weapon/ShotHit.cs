using UnityEngine;

// One bullet striking a collider: where, along which direction, and what it carries.
public readonly struct ShotHit {
    public ShotHit(Collider2D collider, (Vector2 point, Vector2 normal, Vector2 direction) geometry, (float damage, float impulse) force) {
        Collider = collider;
        Point = geometry.point;
        Normal = geometry.normal;
        Direction = geometry.direction;
        Damage = force.damage;
        Impulse = force.impulse;
    }

    public Collider2D Collider { get; }
    public Vector2 Point { get; }
    // The surface normal at the point, facing the shooter.
    public Vector2 Normal { get; }
    // The unit direction the bullet travelled.
    public Vector2 Direction { get; }
    public float Damage { get; }
    // The momentum the bullet gives a dynamic body, applied at the point by the weapon.
    public float Impulse { get; }
}
