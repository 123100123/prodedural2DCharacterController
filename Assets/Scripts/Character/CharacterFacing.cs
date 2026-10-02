using System.Collections.Generic;
using UnityEngine;

// Turns the character by mirroring the rig root in place: bones and IK chains mirror,
// while world-space targets (feet, stride target, body pivot) keep their positions.
public sealed class CharacterFacing {
    private readonly Transform root;
    private readonly int authoredSign;
    private readonly List<Vector3> childPositions = new List<Vector3>();

    public CharacterFacing((Transform root, int authoredSign) rig) {
        root = rig.root;
        authoredSign = rig.authoredSign;
    }

    // Facing of the unmirrored rig as authored: +1 right, -1 left.
    public int AuthoredSign => authoredSign;

    // +1 when facing world right, -1 when facing world left.
    public int Sign => authoredSign * (root.localScale.x < 0f ? -1 : 1);

    public void FaceToward(float horizontalDirection) {
        if (Mathf.Approximately(horizontalDirection, 0f) || (horizontalDirection > 0f ? 1 : -1) == Sign) {
            return;
        }
        MirrorInPlace();
    }

    private void MirrorInPlace() {
        RecordChildPositions();

        Vector3 scale = root.localScale;
        scale.x = -scale.x;
        root.localScale = scale;

        RestoreChildPositions();
    }

    private void RecordChildPositions() {
        childPositions.Clear();
        foreach (Transform child in root) {
            childPositions.Add(child.position);
        }
    }

    private void RestoreChildPositions() {
        int index = 0;
        foreach (Transform child in root) {
            child.position = childPositions[index++];
        }
    }
}
