using UnityEngine;

// The character's procedural effects: dust, smoke, sparks and shell casings, all made in code. It listens to the
// character's events (gameplay never calls it) and emits into a few particle systems built once, so nothing is
// instantiated while playing. Dust and smoke are lit sprites, so they take the dusk lights; sparks are unlit.
public sealed class CharacterEffects {
    private const string LitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string UnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string FallbackShader = "Sprites/Default";

    private const int DustOrder = 20;
    private const int SmokeOrder = 21;
    private const int SparkOrder = 22;
    private const int SplinterOrder = 23;
    private const int MaxFootPuffs = 3;
    private const int MaxLandingPuffs = 14;
    private const int MuzzleSmokePuffs = 2;
    private const int MuzzleSparks = 3;
    private const int ImpactSparks = 6;
    private const int WallKickPuffs = 6;
    private const int LedgeGritPuffs = 2;
    private const int DropThroughPuffs = 3;
    // Seconds between the puffs scraped off the wall while sliding down it.
    private const float SlideDustInterval = 0.09f;
    // Seconds between the puffs a running slide kicks up, at the run speed and when nearly stopped.
    private static readonly (float fast, float slow) GroundSlideDustInterval = (0.035f, 0.12f);
    // Seconds between the puffs a pushed crate scuffs up at its base, at the push speed and when barely moving.
    private static readonly (float fast, float slow) PushDustInterval = (0.08f, 0.2f);
    // A pushed crate slower than this (u/s) raises no dust; at this many u/s it raises the most.
    private static readonly (float min, float full) PushDustSpeed = (0.3f, 2.2f);
    private const float CasingBackOffset = 0.35f;
    private const float CasingSpin = 720f;

    private static readonly Color DustColor = new Color(0.8f, 0.74f, 0.64f, 0.55f);
    private static readonly Color SmokeColor = new Color(0.75f, 0.75f, 0.78f, 0.4f);
    private static readonly Color SparkColor = new Color(1f, 0.82f, 0.45f, 1f);
    // The browns of splintered wood: a light and a dark, a splinter takes a mix of the two.
    private static readonly Color SplinterLight = new Color(0.86f, 0.62f, 0.36f, 1f);
    private static readonly Color SplinterDark = new Color(0.5f, 0.3f, 0.15f, 1f);
    private const float SplinterSpin = 900f;

    private readonly EffectsSettings settings;
    private readonly CharacterRig rig;
    private readonly CharacterEvents events;
    private readonly GameObject root;
    private readonly Texture2D puffTexture;
    private readonly Texture2D sparkTexture;
    private readonly Texture2D splinterTexture;
    private readonly Material splinterMaterial;
    private readonly ParticleEmitter splinters;
    private readonly Material litMaterial;
    private readonly Material smokeMaterial;
    private readonly Material sparkMaterial;
    private readonly Material casingMaterial;
    private readonly ParticleEmitter dust;
    private readonly ParticleEmitter smoke;
    private readonly ParticleEmitter sparks;
    // The wall being slid down (its face and side, +1 right of the body), 0 when not sliding; and the time to the next puff.
    private (int side, float faceX) slidingWall;
    private float slideDustDue;
    // The running slide's direction (+1 right, -1 left), 0 when not sliding; and the time to its next puff.
    private int groundSlideDirection;
    private float groundSlideDustDue;
    // The crate being pushed and which way (+1 right, -1 left), null when not pushing; the time to its next puff; and
    // which bottom corner (leading or trailing) puffs next.
    private (Collider2D crate, int direction) pushed;
    private float pushDustDue;
    private bool pushDustLeads;

    public CharacterEffects(CharacterRig rig, EffectsSettings settings) {
        this.rig = rig;
        this.settings = settings;
        events = rig.Events;
        root = new GameObject("Effects");
        puffTexture = EffectTextures.SoftPuff();
        sparkTexture = EffectTextures.Spark();
        splinterTexture = EffectTextures.Splinter();
        splinterMaterial = CreateMaterial("SplinterMaterial", splinterTexture, UnlitShader);
        litMaterial = CreateMaterial("DustMaterial", puffTexture, LitShader);
        smokeMaterial = CreateMaterial("SmokeMaterial", puffTexture, LitShader);
        sparkMaterial = CreateMaterial("SparkMaterial", sparkTexture, UnlitShader);
        casingMaterial = CreateMaterial("CasingMaterial", null, LitShader);

        dust = new ParticleEmitter("Dust", root.transform, new ParticleEmitter.Style(litMaterial, DustOrder, 160).WithMotion((-0.05f, 2.5f, 2.2f)).WithFadeIn(0.1f));
        smoke = new ParticleEmitter("Smoke", root.transform, new ParticleEmitter.Style(smokeMaterial, SmokeOrder, 48).WithMotion((-0.15f, 1.5f, 3f)).WithFadeIn(0.1f));
        sparks = new ParticleEmitter("Sparks", root.transform, new ParticleEmitter.Style(sparkMaterial, SparkOrder, 96).WithMotion((0.9f, 1.2f, 0.4f)).WithStretch(0.06f));
        splinters = new ParticleEmitter("Splinters", root.transform, new ParticleEmitter.Style(splinterMaterial, SplinterOrder, 96).WithMotion((1.4f, 0.35f, 0.7f)));
        Casings = new CasingPool(root.transform, casingMaterial);
        Popups = new PointsPopups(root.transform, settings.targets);
        Subscribe();
    }

    public CasingPool Casings { get; }
    // The points that float up from a target that scored.
    public PointsPopups Popups { get; }
    public GameObject Root => root;
    // Live particles of each kind, for tests and tools.
    public int DustCount => dust.Count;
    public int SmokeCount => smoke.Count;
    public int SparkCount => sparks.Count;
    public int SplinterCount => splinters.Count;

    public void Tick(float deltaTime) {
        Casings.Tick(deltaTime);
        Popups.Tick(deltaTime);
        EmitSlideDust(deltaTime);
        EmitGroundSlideDust(deltaTime);
        EmitPushDust(deltaTime);
    }

    public void Dispose() {
        Unsubscribe();
        Casings.Dispose();
        Popups.Dispose();
        Discard(root);
        Discard(litMaterial);
        Discard(smokeMaterial);
        Discard(sparkMaterial);
        Discard(splinterMaterial);
        Discard(casingMaterial);
        Discard(puffTexture);
        Discard(sparkTexture);
        Discard(splinterTexture);
    }

    private void Subscribe() {
        events.FootPlanted += OnFootPlanted;
        events.Landed += OnLanded;
        events.Jumped += OnJumped;
        events.Rolled += OnRolled;
        events.WallJumped += OnWallJumped;
        events.WallSlideStarted += OnWallSlideStarted;
        events.WallSlideStopped += OnWallSlideStopped;
        events.SlideStarted += OnSlideStarted;
        events.SlideStopped += OnSlideStopped;
        events.LedgeDropped += OnLedgeDropped;
        events.DroppedThroughPlatform += OnDroppedThroughPlatform;
        events.PushStarted += OnPushStarted;
        events.PushStopped += OnPushStopped;
        events.Fired += OnFired;
        events.ShotHit += OnShotHit;
        events.TargetScored += OnTargetScored;
    }

    private void Unsubscribe() {
        events.FootPlanted -= OnFootPlanted;
        events.Landed -= OnLanded;
        events.Jumped -= OnJumped;
        events.Rolled -= OnRolled;
        events.WallJumped -= OnWallJumped;
        events.WallSlideStarted -= OnWallSlideStarted;
        events.WallSlideStopped -= OnWallSlideStopped;
        events.SlideStarted -= OnSlideStarted;
        events.SlideStopped -= OnSlideStopped;
        events.LedgeDropped -= OnLedgeDropped;
        events.DroppedThroughPlatform -= OnDroppedThroughPlatform;
        events.PushStarted -= OnPushStarted;
        events.PushStopped -= OnPushStopped;
        events.Fired -= OnFired;
        events.ShotHit -= OnShotHit;
        events.TargetScored -= OnTargetScored;
    }

    // ---- Dust -----------------------------------------------------------------------------------------------

    // A small puff kicked back and up from the planted foot.
    private void OnFootPlanted((Vector2 position, float impactSpeed) plant) {
        if (!settings.enabled) {
            return;
        }
        float share = EffectMath.ImpactShare((plant.impactSpeed, settings.footDustMinImpact, settings.footDustFullImpact));
        int count = EffectMath.PuffCount((share, settings.dustScale), MaxFootPuffs);
        float size = EffectMath.PuffSize(share, Mathf.Sqrt(settings.dustScale));
        float behind = -rig.Facing.Sign;
        for (int puff = 0; puff < count; puff++) {
            Vector2 velocity = new Vector2(behind * Random.Range(0.3f, 1f) * (0.6f + share), Random.Range(0.2f, 0.7f));
            dust.Emit(plant.position + Vector2.up * 0.05f, velocity, (size * Random.Range(0.7f, 1.1f), DustLife()), DustColor);
        }
    }

    // A spray along the ground to both sides, wider and heavier the harder the landing.
    private void OnLanded((Vector2 position, float impactSpeed) landing) {
        if (!settings.enabled) {
            return;
        }
        float share = EffectMath.ImpactShare((landing.impactSpeed, settings.landingDustMinImpact, settings.landingDustFullImpact));
        int count = EffectMath.PuffCount((share, settings.dustScale), MaxLandingPuffs);
        float size = EffectMath.PuffSize(share, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            float side = puff % 2 == 0 ? 1f : -1f;
            Vector2 velocity = new Vector2(side * Random.Range(1.2f, 3.5f) * (0.5f + share), Random.Range(0.1f, 0.8f));
            dust.Emit(landing.position + new Vector2(side * Random.Range(0f, 0.3f), 0.05f), velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    // A few puffs at the feet as they push off.
    private void OnJumped(Vector2 launchVelocity) {
        if (!settings.enabled) {
            return;
        }
        Vector2 feet = Feet();
        int count = EffectMath.ScaledCount(settings.jumpDustCount, settings.dustScale);
        float size = EffectMath.PuffSize(0.3f, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            float side = puff % 2 == 0 ? 1f : -1f;
            Vector2 velocity = new Vector2(side * Random.Range(0.6f, 2f) - launchVelocity.x * 0.15f, Random.Range(0.1f, 0.5f));
            dust.Emit(feet + new Vector2(side * Random.Range(0f, 0.2f), 0.05f), velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    // A burst where the hands meet the ground in a roll, thrown back against the direction of travel.
    private void OnRolled((Vector2 position, int direction) roll) {
        if (!settings.enabled) {
            return;
        }
        int count = EffectMath.ScaledCount(settings.rollDustCount, settings.dustScale);
        float size = EffectMath.PuffSize(0.5f, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            Vector2 velocity = new Vector2(-roll.direction * Random.Range(0.5f, 2.5f), Random.Range(0.2f, 1f));
            dust.Emit(roll.position + Vector2.up * 0.05f, velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    // A burst off the wall where the feet kicked it, thrown back against the jump.
    private void OnWallJumped((Vector2 position, int direction) kick) {
        if (!settings.enabled) {
            return;
        }
        int count = EffectMath.ScaledCount(WallKickPuffs, settings.dustScale);
        float size = EffectMath.PuffSize(0.4f, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            Vector2 velocity = new Vector2(kick.direction * Random.Range(0.3f, 1.2f), Random.Range(-0.8f, 0.8f));
            dust.Emit(kick.position + new Vector2(0f, Random.Range(-0.2f, 0.4f)), velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    // A little grit knocked off the corner the hands let go of.
    private void OnLedgeDropped((Vector2 corner, int climbDirection) drop) {
        if (!settings.enabled) {
            return;
        }
        int count = EffectMath.ScaledCount(LedgeGritPuffs, settings.dustScale);
        float size = EffectMath.PuffSize(0.15f, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            Vector2 velocity = new Vector2(-drop.climbDirection * Random.Range(0.1f, 0.5f), Random.Range(-0.6f, 0f));
            dust.Emit(drop.corner + new Vector2(-drop.climbDirection * 0.05f, 0.02f), velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    // A few puffs off the platform the feet slip through.
    private void OnDroppedThroughPlatform(Vector2 feet) {
        if (!settings.enabled) {
            return;
        }
        int count = EffectMath.ScaledCount(DropThroughPuffs, settings.dustScale);
        float size = EffectMath.PuffSize(0.2f, Mathf.Sqrt(settings.dustScale));
        for (int puff = 0; puff < count; puff++) {
            float side = puff % 2 == 0 ? 1f : -1f;
            Vector2 velocity = new Vector2(side * Random.Range(0.4f, 1.2f), Random.Range(0f, 0.3f));
            dust.Emit(feet + new Vector2(side * Random.Range(0f, 0.25f), 0.05f), velocity, (size * Random.Range(0.8f, 1.2f), DustLife()), DustColor);
        }
    }

    private void OnWallSlideStarted((Vector2 contact, int wallSide) slide) {
        slidingWall = (slide.wallSide, slide.contact.x);
        slideDustDue = 0f;
    }

    private void OnWallSlideStopped() => slidingWall = (0, 0f);

    // While sliding, a small puff now and then scraped off the wall at the feet, drifting up and out from it.
    private void EmitSlideDust(float deltaTime) {
        if (slidingWall.side == 0 || !settings.enabled || settings.dustScale <= 0f) {
            return;
        }
        slideDustDue -= deltaTime;
        if (slideDustDue > 0f) {
            return;
        }
        slideDustDue = SlideDustInterval;
        float feetY = Mathf.Min(rig.Stride.FrontLeg.FootTarget.y, rig.Stride.BackLeg.FootTarget.y);
        Vector2 velocity = new Vector2(-slidingWall.side * Random.Range(0.2f, 0.7f), Random.Range(0.6f, 1.4f));
        float size = EffectMath.PuffSize(0.25f, Mathf.Sqrt(settings.dustScale));
        dust.Emit(new Vector2(slidingWall.faceX, feetY + Random.Range(0f, 0.3f)), velocity, (size * Random.Range(0.7f, 1.1f), DustLife() * 0.6f), DustColor);
    }

    private void OnSlideStarted((Vector2 position, int direction) slide) {
        groundSlideDirection = slide.direction;
        groundSlideDustDue = 0f;
    }

    private void OnSlideStopped() => groundSlideDirection = 0;

    // While sliding along the ground, a trail of puffs where the lead heel scrapes it, thrown back and up: more often and
    // bigger the faster the slide.
    private void EmitGroundSlideDust(float deltaTime) {
        if (groundSlideDirection == 0 || !settings.enabled || settings.dustScale <= 0f) {
            return;
        }
        float share = Mathf.Clamp01(Mathf.Abs(rig.Motor.Velocity.x) / Mathf.Max(0.01f, rig.Stride.Run.speed));
        groundSlideDustDue -= deltaTime;
        if (groundSlideDustDue > 0f) {
            return;
        }
        groundSlideDustDue = Mathf.Lerp(GroundSlideDustInterval.slow, GroundSlideDustInterval.fast, share);

        Vector2 front = rig.Stride.FrontLeg.FootTarget;
        Vector2 back = rig.Stride.BackLeg.FootTarget;
        float heelX = (front.x - back.x) * groundSlideDirection >= 0f ? front.x : back.x;
        Vector2 heel = new Vector2(heelX, Mathf.Min(front.y, back.y) + 0.05f);
        Vector2 velocity = new Vector2(-groundSlideDirection * Random.Range(0.4f, 1.6f) * (0.4f + share), Random.Range(0.3f, 1f) * (0.5f + share));
        float size = EffectMath.PuffSize(share, Mathf.Sqrt(settings.dustScale));
        dust.Emit(heel, velocity, (size * Random.Range(0.7f, 1.1f), DustLife() * 0.8f), DustColor);
    }

    private void OnPushStarted((Collider2D crate, int direction) push) {
        pushed = push;
        pushDustDue = 0f;
    }

    private void OnPushStopped() => pushed = (null, 0);

    // While a pushed crate slides, a little dust now and then at its base, by turns at the leading and the trailing bottom
    // corner, drifting back from the push: more often and bigger the faster it slides.
    private void EmitPushDust(float deltaTime) {
        if (pushed.crate == null || !settings.enabled || settings.dustScale <= 0f) {
            return;
        }
        Rigidbody2D body = pushed.crate.attachedRigidbody;
        float speed = body != null ? Mathf.Abs(body.linearVelocity.x) : 0f;
        if (speed < PushDustSpeed.min) {
            return;
        }
        pushDustDue -= deltaTime;
        if (pushDustDue > 0f) {
            return;
        }
        float share = Mathf.InverseLerp(PushDustSpeed.min, PushDustSpeed.full, speed);
        pushDustDue = Mathf.Lerp(PushDustInterval.slow, PushDustInterval.fast, share);
        pushDustLeads = !pushDustLeads;

        Bounds bounds = pushed.crate.bounds;
        float leadingX = pushed.direction > 0 ? bounds.max.x : bounds.min.x;
        float trailingX = pushed.direction > 0 ? bounds.min.x : bounds.max.x;
        Vector2 corner = new Vector2(pushDustLeads ? leadingX : trailingX, bounds.min.y + 0.05f);
        Vector2 velocity = new Vector2(-pushed.direction * Random.Range(0.2f, 0.8f) * (0.5f + share), Random.Range(0.15f, 0.6f));
        float size = EffectMath.PuffSize(0.2f + 0.3f * share, Mathf.Sqrt(settings.dustScale));
        dust.Emit(corner, velocity, (size * Random.Range(0.7f, 1.1f), DustLife() * 0.7f), DustColor);
    }

    // ---- Gun ------------------------------------------------------------------------------------------------

    private void OnFired((Vector2 muzzle, Vector2 direction) shot) {
        if (!settings.enabled) {
            return;
        }
        Vector2 direction = shot.direction.normalized;
        int puffs = EffectMath.ScaledCount(MuzzleSmokePuffs, settings.smokeScale);
        for (int puff = 0; puff < puffs; puff++) {
            Vector2 velocity = direction * Random.Range(0.6f, 1.6f) + Vector2.up * Random.Range(0.2f, 0.6f);
            smoke.Emit(shot.muzzle, velocity, (Random.Range(0.12f, 0.22f) * Mathf.Sqrt(settings.smokeScale), Random.Range(0.6f, 1f)), SmokeColor);
        }
        int count = EffectMath.ScaledCount(MuzzleSparks, settings.sparkScale);
        for (int spark = 0; spark < count; spark++) {
            Vector2 velocity = Rotate(direction, Random.Range(-14f, 14f)) * Random.Range(6f, 12f);
            sparks.Emit(shot.muzzle, velocity, (Random.Range(0.05f, 0.09f), Random.Range(0.1f, 0.22f)), SparkColor);
        }
        EjectCasing(shot);
    }

    // The bullet's sparks fly off along the mirrored heading, spread around it, plus a small puff of dust.
    private void OnShotHit(ShotHit hit) {
        if (!settings.enabled) {
            return;
        }
        Vector2 glance = Vector2.Reflect(hit.Direction, hit.Normal);
        int count = EffectMath.ScaledCount(ImpactSparks, settings.sparkScale);
        for (int spark = 0; spark < count; spark++) {
            Vector2 velocity = Rotate(glance, Random.Range(-40f, 40f)) * Random.Range(3f, 9f);
            sparks.Emit(hit.Point, velocity, (Random.Range(0.05f, 0.1f), Random.Range(0.12f, 0.3f)), SparkColor);
        }
        int puffs = EffectMath.ScaledCount(1f, settings.dustScale);
        for (int puff = 0; puff < puffs; puff++) {
            dust.Emit(hit.Point, hit.Normal * Random.Range(0.3f, 0.9f), (Random.Range(0.2f, 0.32f) * Mathf.Sqrt(settings.dustScale), DustLife() * 0.7f), DustColor);
        }
    }

    // A target paid out: its points float up from where it says, and a few splinters fly off the board (a spray of them
    // when the hit broke it), brown and tumbling, thrown up and out.
    private void OnTargetScored((int points, Vector2 position, bool broke) score) {
        if (!settings.enabled) {
            return;
        }
        Popups.Show(score.points, score.position, score.broke);
        int count = EffectMath.ScaledCount(score.broke ? settings.targets.breakSplinters : settings.targets.hitSplinters, 1f);
        for (int splinter = 0; splinter < count; splinter++) {
            float angle = Random.Range(0f, 360f);
            Vector2 velocity = Rotate(Vector2.up, Random.Range(-70f, 70f)) * Random.Range(2.5f, score.broke ? 8f : 5f);
            Color color = Color.Lerp(SplinterDark, SplinterLight, Random.value);
            splinters.EmitSpinning(score.position + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f)), velocity, (Random.Range(0.35f, 0.6f), Random.Range(0.7f, 1.2f)), (color, angle, Random.Range(-SplinterSpin, SplinterSpin)));
        }
    }

    // Thrown up and back from the ejection port, a little behind the muzzle, spinning.
    private void EjectCasing((Vector2 muzzle, Vector2 direction) shot) {
        if (settings.casingLifetime <= 0f) {
            return;
        }
        Vector2 direction = shot.direction.normalized;
        float speed = settings.casingEjectSpeed * Random.Range(0.75f, 1.15f);
        Vector2 velocity = EffectMath.UpwardPerpendicular(direction) * speed - direction * (speed * 0.4f);
        Casings.Eject((shot.muzzle - direction * CasingBackOffset, velocity, Random.Range(-CasingSpin, CasingSpin)), (settings.casingLifetime, settings.casingFadeTime));
    }

    // ---- Helpers --------------------------------------------------------------------------------------------

    // Where the feet stand: under the body, on the height of the planted feet.
    private Vector2 Feet() => new Vector2(rig.Body.position.x, rig.Stride.StanceHeight);

    private float DustLife() => settings.dustLifetime * Random.Range(0.7f, 1.1f);

    private static Vector2 Rotate(Vector2 vector, float degrees) {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private static Material CreateMaterial(string name, Texture texture, string shaderName) {
        Shader shader = Shader.Find(shaderName) ?? Shader.Find(FallbackShader);
        Material material = new Material(shader) { name = name };
        if (texture != null) {
            material.mainTexture = texture;
        }
        return material;
    }

    // Edit-mode tests build the effects outside play mode, where Destroy is not allowed.
    private static void Discard(Object unwanted) {
        if (unwanted == null) {
            return;
        }
        if (Application.isPlaying) {
            Object.Destroy(unwanted);
        }
        else {
            Object.DestroyImmediate(unwanted);
        }
    }
}
