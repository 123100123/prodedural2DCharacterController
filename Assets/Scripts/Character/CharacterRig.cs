using UnityEngine;

// The character's movable parts as seen by the full-body states.
public sealed class CharacterRig {
    public CharacterRig((StrideController stride, CharacterFacing facing, KinematicBodyMotor motor) parts) {
        Stride = parts.stride;
        Facing = parts.facing;
        Motor = parts.motor;
    }

    public StrideController Stride { get; }
    public CharacterFacing Facing { get; }
    public KinematicBodyMotor Motor { get; }
    public Transform Body => Stride.transform;
}
