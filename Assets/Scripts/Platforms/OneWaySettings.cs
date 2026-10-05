using System;
using UnityEngine;

// One-way platforms: dropping through them and landing on them. A class with initializers, so a scene saved before a
// field existed keeps its default.
[Serializable]
public class OneWaySettings {
    [Tooltip("Seconds a platform dropped through (down + jump) is ignored; by then the feet are well below its top.")]
    public float dropThroughTime = 0.25f;
    [Tooltip("A falling character still lands on a one-way platform when its standing feet are this far below the top: " +
             "a jump that only just clears the platform catches it.")]
    public float landingCatch = 0.2f;

    public OneWaySettings Clone() => (OneWaySettings)MemberwiseClone();

    // A copy made field by field into this instance, which the passage holds by reference.
    public void CopyFrom(OneWaySettings source) {
        dropThroughTime = source.dropThroughTime;
        landingCatch = source.landingCatch;
    }
}
