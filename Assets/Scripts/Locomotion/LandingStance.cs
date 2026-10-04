using UnityEngine;

// Where the feet go when the character lands. Standing, they take the stance: either side of the body. Moving, they take
// where a gait at that speed has them at a contact: the leading foot lands ahead of the body and the other trails far
// behind, so a running landing is the first step of the stride and the gait carries on from it.
public static class LandingStance {
    public readonly struct Columns {
        public Columns(float lead, float trail, bool isMoving) {
            Lead = lead;
            Trail = trail;
            IsMoving = isMoving;
        }

        // Where the foot that lands first, and the other foot, stand (world x).
        public float Lead { get; }
        public float Trail { get; }
        // The feet take a moving gait's positions rather than the standing stance.
        public bool IsMoving { get; }
    }

    // centerX: where the body pivot is; facingSign: the way the character faces; speedX: its horizontal speed.
    public static Columns Of((float centerX, int facingSign, float speedX) landing, (float stanceHalfWidth, GaitSet gaits, float movingShare) stance) {
        float walkSpeed = stance.gaits.Walk.speed;
        bool isMoving = Mathf.Abs(landing.speedX) >= stance.movingShare * walkSpeed;
        if (!isMoving) {
            return new Columns(landing.centerX + landing.facingSign * stance.stanceHalfWidth, landing.centerX - landing.facingSign * stance.stanceHalfWidth, false);
        }

        float direction = Mathf.Sign(landing.speedX);
        GaitProfile gait = stance.gaits.SettledAt(landing.speedX);
        float lead = GaitTiming.ContactLead(landing.speedX, gait);
        float trail = GaitTiming.ToeOffLag(landing.speedX, gait);
        return new Columns(landing.centerX + direction * lead, landing.centerX - direction * trail, true);
    }
}
