using UnityEngine;

// Decides which leg carries the body, steadily: the foot that planted most recently takes the weight, and it keeps it
// until it lifts while the other foot is down. In a running flight phase neither foot is down and the last support is
// kept, so the pelvis carries on from it. Each foot remembers where the body was when it planted, so the stance
// progress comes from how far the body has since travelled over it.
public sealed class SupportLegResolver {
    private struct Plant {
        public bool IsPlanted;
        public float FootX;
        public float AnchorAtPlant;
        public float Direction;
        public float StanceTravel;
    }

    private Plant front;
    private Plant back;
    private SupportStance.Leg support = SupportStance.Leg.None;
    private float anchorX;

    public SupportStance Current => StanceOf(support);

    // Called once per gait tick, after the feet moved. expectedStanceTravel: how far the body travels over a planted foot
    // at the current speed (the gait's stance length).
    public void Track((bool front, bool back) planted, (float frontX, float backX) feet, (float anchorX, float direction, float expectedStanceTravel) motion) {
        anchorX = motion.anchorX;
        bool frontLanded = UpdatePlant(ref front, (planted.front, feet.frontX), motion);
        bool backLanded = UpdatePlant(ref back, (planted.back, feet.backX), motion);
        support = ChooseSupport((frontLanded, backLanded));
    }

    // Standing still, nothing carries the body forward over a foot.
    public void Reset() {
        front.IsPlanted = false;
        back.IsPlanted = false;
        support = SupportStance.Leg.None;
    }

    // Returns true on the tick the foot lands.
    private static bool UpdatePlant(ref Plant plant, (bool isPlanted, float footX) foot, (float anchorX, float direction, float expectedStanceTravel) motion) {
        bool landed = foot.isPlanted && !plant.IsPlanted;
        if (landed) {
            plant.FootX = foot.footX;
            plant.AnchorAtPlant = motion.anchorX;
            plant.Direction = motion.direction;
            plant.StanceTravel = Mathf.Max(0.01f, motion.expectedStanceTravel);
        }
        plant.IsPlanted = foot.isPlanted;
        return landed;
    }

    private SupportStance.Leg ChooseSupport((bool front, bool back) landed) {
        if (landed.front != landed.back) {
            return landed.front ? SupportStance.Leg.Front : SupportStance.Leg.Back;
        }
        bool supportStillDown = support == SupportStance.Leg.Front ? front.IsPlanted : support == SupportStance.Leg.Back && back.IsPlanted;
        if (supportStillDown) {
            return support;
        }
        if (front.IsPlanted != back.IsPlanted) {
            return front.IsPlanted ? SupportStance.Leg.Front : SupportStance.Leg.Back;
        }
        return support;
    }

    private SupportStance StanceOf(SupportStance.Leg leg) {
        if (leg == SupportStance.Leg.None) {
            return SupportStance.Unsupported;
        }
        Plant plant = leg == SupportStance.Leg.Front ? front : back;
        float progress = Mathf.Max(0f, (anchorX - plant.AnchorAtPlant) * plant.Direction / plant.StanceTravel);
        return new SupportStance((leg, plant.IsPlanted), (plant.FootX, plant.Direction, plant.StanceTravel, progress));
    }
}
