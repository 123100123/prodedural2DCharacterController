using UnityEngine;

// The secondary motion of a long fall: the arms flail, the feet drift and the torso pitches, in step with nothing but
// the length and speed of the fall. It is built from a few sine waves of unrelated, slowly wandering speeds whose phases
// come from a seed, so it is smooth (never a random value per frame), different for each fall, deterministic for a given
// seed, and never repeats a loop. The strength rises continuously with the time spent falling (nothing below the onset,
// so a hop or a normal jump is untouched), with the fall speed, and is cut as the landing nears or while aiming.
public sealed class FallSecondaryMotion {
    private const float TwoPi = Mathf.PI * 2f;
    private const float GoldenRatio = 1.618034f;

    // What the secondary motion adds to the flight pose this frame.
    public readonly struct Output {
        public Output(float strength, (float front, float back) arms, (float front, float back) elbows, (Vector2 lead, Vector2 trail) feet, float pitch) {
            Strength = strength;
            ArmFront = arms.front;
            ArmBack = arms.back;
            ElbowFront = elbows.front;
            ElbowBack = elbows.back;
            LeadFoot = feet.lead;
            TrailFoot = feet.trail;
            ForwardPitch = pitch;
        }

        public float Strength { get; }
        // Degrees added to the arms' raise.
        public float ArmFront { get; }
        public float ArmBack { get; }
        // Added to the elbow bend of each arm.
        public float ElbowFront { get; }
        public float ElbowBack { get; }
        // Added to the feet's pose offsets.
        public Vector2 LeadFoot { get; }
        public Vector2 TrailFoot { get; }
        // Degrees added to the posture's forward lean.
        public float ForwardPitch { get; }
    }

    public readonly struct Input {
        public Input((float timeFalling, float verticalSpeed, float forwardAcceleration) fall, (float landingPrep, float aimSuppression) damping, float deltaTime) {
            TimeFalling = fall.timeFalling;
            VerticalSpeed = fall.verticalSpeed;
            ForwardAcceleration = fall.forwardAcceleration;
            LandingPrep = damping.landingPrep;
            AimSuppression = damping.aimSuppression;
            DeltaTime = deltaTime;
        }

        public float TimeFalling { get; }
        public float VerticalSpeed { get; }
        // Acceleration along the facing direction (positive speeding up forward).
        public float ForwardAcceleration { get; }
        // 0 far from the ground, 1 at touchdown.
        public float LandingPrep { get; }
        // 1 while aiming, which needs the arms.
        public float AimSuppression { get; }
        public float DeltaTime { get; }
    }

    private readonly FallSecondarySettings settings;
    private float time;
    // Where each wave is in its cycle, and the starting phases that make this fall differ from the last.
    private float armFront;
    private float armBack;
    private float legLead;
    private float legTrail;
    private (float a, float b, float c, float d, float e, float f) phases;

    public FallSecondaryMotion(FallSecondarySettings settings) {
        this.settings = settings;
    }

    // Starts a new fall: its seed decides the phases of the waves.
    public void Begin(int seed) {
        time = 0f;
        phases = (Hash01(seed, 1) * TwoPi, Hash01(seed, 2) * TwoPi, Hash01(seed, 3) * TwoPi, Hash01(seed, 4) * TwoPi, Hash01(seed, 5) * TwoPi, Hash01(seed, 6) * TwoPi);
        armFront = phases.a;
        armBack = phases.b + Mathf.PI * 0.85f;
        legLead = phases.c;
        legTrail = phases.d;
    }

    // 0 before the onset, rising to 1 by the full time, and with the fall speed: the strength before damping.
    public static float Strength(float timeFalling, float verticalSpeed, FallSecondarySettings settings) {
        float timeGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.onsetTime, Mathf.Max(settings.onsetTime + 0.01f, settings.fullTime), timeFalling));
        float speedGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.speedBand.x, Mathf.Max(settings.speedBand.x + 0.01f, settings.speedBand.y), -verticalSpeed));
        return timeGate * Mathf.Lerp(settings.slowFallStrength, 1f, speedGate);
    }

    public Output Tick(Input input) {
        time += input.DeltaTime;
        float speedGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.speedBand.x, Mathf.Max(settings.speedBand.x + 0.01f, settings.speedBand.y), -input.VerticalSpeed));
        float strength = Strength(input.TimeFalling, input.VerticalSpeed, settings)
                         * (1f - settings.landingDamping * Mathf.Clamp01(input.LandingPrep))
                         * (1f - Mathf.Clamp01(input.AimSuppression));

        float baseRate = Mathf.Lerp(settings.flailRate.x, settings.flailRate.y, strength);
        armFront += Advance(baseRate, phases.e, input.DeltaTime);
        armBack += Advance(baseRate * settings.backArmRate, phases.f, input.DeltaTime);
        legLead += Advance(baseRate * 0.8f, phases.c, input.DeltaTime);
        legTrail += Advance(baseRate * 0.67f, phases.d, input.DeltaTime);

        float reactive = strength * (settings.armLiftWithSpeed * speedGate - Mathf.Clamp(settings.armLagPerAcceleration * input.ForwardAcceleration, -settings.maxArmLag, settings.maxArmLag));
        float front = settings.armAmplitude * strength * Wave(armFront, phases.a) + reactive;
        float back = settings.armAmplitude * strength * Wave(armBack, phases.b) + reactive;
        float elbowFront = settings.elbowFlail * strength * (0.5f + 0.5f * Mathf.Sin(1.37f * armFront + phases.d));
        float elbowBack = settings.elbowFlail * strength * (0.5f + 0.5f * Mathf.Sin(1.37f * armBack + phases.c));

        Vector2 lead = new Vector2(settings.legLoosen.x * Mathf.Sin(legLead), settings.legLoosen.y * Mathf.Sin(GoldenRatio * legLead + phases.e)) * strength;
        Vector2 trail = new Vector2(settings.legLoosen.x * Mathf.Sin(legTrail), settings.legLoosen.y * Mathf.Sin(GoldenRatio * legTrail + phases.f)) * strength;
        return new Output(strength, (front, back), (elbowFront, elbowBack), (lead, trail), settings.forwardPitch * strength);
    }

    // How far a wave advances this frame: its rate wanders slowly about the base rate so it never settles into a rhythm.
    private float Advance(float baseRate, float seedPhase, float deltaTime) {
        float wander = 1f + settings.rateWobble * Mathf.Sin(TwoPi * settings.wobbleRate * time + seedPhase);
        return TwoPi * baseRate * wander * deltaTime;
    }

    // The main wave with a faster second one on top: an arc that is not a plain sine.
    private float Wave(float phase, float seedPhase) {
        return (1f - settings.secondWave) * Mathf.Sin(phase) + settings.secondWave * Mathf.Sin(GoldenRatio * phase + seedPhase);
    }

    // A deterministic number in [0, 1) from a seed and a salt.
    public static float Hash01(int seed, int salt) {
        unchecked {
            uint hash = (uint)(seed * 73856093) ^ (uint)(salt * 19349663);
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFFu) / (float)0x1000000;
        }
    }
}
