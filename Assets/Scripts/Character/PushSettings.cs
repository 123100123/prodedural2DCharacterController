using System;
using UnityEngine;

// Pushing crates: how fast and how hard the character pushes a dynamic body it walks into, how close it keeps to the
// crate's face, and the leaning two-handed pose. A class with initializers, so a scene saved before a field existed keeps
// its default.
[Serializable]
public class PushSettings {
    [Tooltip("Off: crates are only obstacles to the character (shots still knock them about).")]
    public bool enabled = true;

    [Header("Force")]
    [Tooltip("Speed (u/s) the character pushes a crate at: slower than a walk.")]
    public float pushSpeed = 2.2f;
    [Tooltip("The most force (N) the push puts into a crate. A crate whose friction needs more than this stays put.")]
    public float maxForce = 200f;
    [Tooltip("How far (u/s) the crate may run ahead of the character, and the character catch up with the crate, while " +
             "they speed up together.")]
    public float speedSlack = 0.3f;

    [Header("Contact")]
    [Tooltip("Gap (u) the push keeps between where the body is heading and the crate's face; the push takes hold within " +
             "twice this.")]
    public float contactSkin = 0.08f;
    [Tooltip("The push lets go when the crate's face is this far (u) ahead of where the body is heading.")]
    public float releaseDistance = 0.35f;

    [Header("Pose")]
    [Tooltip("Degrees the torso leans into the push.")]
    public float lean = 12f;
    [Tooltip("The hands press on the crate's face this far (u) above the shoulder (negative is below), kept on the face.")]
    public float handHeight = -0.45f;
    [Tooltip("Seconds the pose takes to blend in and out.")]
    public float blendTime = 0.15f;

    public PushSettings Clone() => (PushSettings)MemberwiseClone();

    // A copy made field by field into this instance, which the push holds by reference.
    public void CopyFrom(PushSettings source) {
        enabled = source.enabled;
        pushSpeed = source.pushSpeed;
        maxForce = source.maxForce;
        speedSlack = source.speedSlack;
        contactSkin = source.contactSkin;
        releaseDistance = source.releaseDistance;
        lean = source.lean;
        handHeight = source.handHeight;
        blendTime = source.blendTime;
    }
}
