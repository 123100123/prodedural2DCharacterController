using UnityEngine;

// A grabbable ledge: the top corner of a surface's near edge and the side the character climbs toward.
public readonly struct Ledge {
    public Ledge(Vector2 corner, int climbDirection) {
        Corner = corner;
        ClimbDirection = climbDirection;
    }

    public Vector2 Corner { get; }
    public int ClimbDirection { get; }
}
