using System.Collections.Generic;
using UnityEngine;

// Checks the character's hierarchy against what the procedural code relies on, so a broken rig is reported when the
// character starts (and while editing) instead of showing up as a strange pose: the locomotion lean turns the posture
// root (the pelvis bone, the skeleton's root under the kinematic body), which must carry the chest, must not be the
// kinematic body itself, and must not carry the world-space IK targets.
public static class RigValidation {
    public static List<string> Problems((Transform kinematicBody, Transform postureRoot, Transform chest, IReadOnlyList<Transform> worldSpaceTargets) rig) {
        List<string> problems = new List<string>();
        if (rig.postureRoot == null) {
            problems.Add("postureRoot is not assigned: the locomotion lean has nothing to turn. Assign the pelvis bone.");
            return problems;
        }
        if (rig.postureRoot == rig.chest) {
            problems.Add("postureRoot is the chest itself: the lean would bend at the waist and fight the chest's own motion. Assign the pelvis bone.");
        }
        if (rig.postureRoot == rig.kinematicBody) {
            problems.Add("postureRoot is the kinematic body: leaning would tilt the body pivot. Assign the pelvis bone under it.");
        }
        if (rig.chest != null && !rig.chest.IsChildOf(rig.postureRoot)) {
            problems.Add($"the chest ({rig.chest.name}) is not under postureRoot ({rig.postureRoot.name}): the lean would not carry the upper body.");
        }
        if (rig.kinematicBody != null && !rig.postureRoot.IsChildOf(rig.kinematicBody)) {
            problems.Add($"postureRoot ({rig.postureRoot.name}) is not under the kinematic body ({rig.kinematicBody.name}): the skeleton would not travel with the body.");
        }
        foreach (Transform target in rig.worldSpaceTargets) {
            if (target != null && target.IsChildOf(rig.postureRoot)) {
                problems.Add($"{target.name} is under postureRoot and would lean with it: IK targets must stay in world space.");
            }
        }
        return problems;
    }
}
