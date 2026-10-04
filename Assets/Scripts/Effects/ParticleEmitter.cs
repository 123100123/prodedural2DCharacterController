using UnityEngine;

// One ParticleSystem configured in code that never plays a burst of its own: every particle is emitted by hand at a
// position with a velocity, size, lifetime and colour. Particles come from the system's own fixed buffer, so emitting
// allocates nothing and the oldest particle is recycled when it is full.
public sealed class ParticleEmitter {
    // How a kind of particle looks and moves; the values the emitter's modules are configured with once.
    public readonly struct Style {
        public Style(Material material, int sortingOrder, int maxParticles) {
            Material = material;
            SortingOrder = sortingOrder;
            MaxParticles = maxParticles;
            Gravity = 0f;
            Drag = 0f;
            Growth = 1f;
            Stretch = 0f;
            FadeIn = 0f;
        }

        private Style(Style source, (float gravity, float drag, float growth, float stretch, float fadeIn) motion) {
            Material = source.Material;
            SortingOrder = source.SortingOrder;
            MaxParticles = source.MaxParticles;
            (Gravity, Drag, Growth, Stretch, FadeIn) = motion;
        }

        public Material Material { get; }
        public int SortingOrder { get; }
        public int MaxParticles { get; }
        // Gravity multiplier (1 is the world's gravity); dust floats (a little negative) and sparks fall.
        public float Gravity { get; }
        // Share of the velocity lost per second.
        public float Drag { get; }
        // Size at the end of life as a multiple of the start size: puffs billow, sparks shrink.
        public float Growth { get; }
        // Streak length per unit of speed; 0 draws a plain round particle.
        public float Stretch { get; }
        // Share of the life spent fading in; the rest fades out.
        public float FadeIn { get; }

        public Style WithMotion((float gravity, float drag, float growth) motion) => new Style(this, (motion.gravity, motion.drag, motion.growth, Stretch, FadeIn));

        public Style WithStretch(float stretch) => new Style(this, (Gravity, Drag, Growth, stretch, FadeIn));

        public Style WithFadeIn(float fadeIn) => new Style(this, (Gravity, Drag, Growth, Stretch, fadeIn));
    }

    private readonly ParticleSystem system;
    private ParticleSystem.EmitParams emit;

    public ParticleEmitter(string name, Transform parent, Style style) {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent, false);
        system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Configure(style);
        ConfigureRenderer(host.GetComponent<ParticleSystemRenderer>(), style);
        system.Play();
    }

    // Live particles right now.
    public int Count => system.particleCount;

    public void Emit(Vector2 position, Vector2 velocity, (float size, float lifetime) shape, Color color) {
        emit.position = position;
        emit.velocity = velocity;
        emit.startSize = shape.size;
        emit.startLifetime = shape.lifetime;
        emit.startColor = color;
        system.Emit(emit, 1);
    }

    private void Configure(Style style) {
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = style.Gravity;
        main.maxParticles = style.MaxParticles;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(style.FadeIn));

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = !Mathf.Approximately(style.Growth, 1f);
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, style.Growth));

        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = style.Drag > 0f;
        limit.limit = 1000f;
        limit.drag = style.Drag;
    }

    private static Gradient FadeGradient(float fadeIn) {
        Gradient gradient = new Gradient();
        GradientAlphaKey[] alpha = fadeIn > 0f
            ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) };
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
        return gradient;
    }

    private static void ConfigureRenderer(ParticleSystemRenderer renderer, Style style) {
        renderer.sharedMaterial = style.Material;
        renderer.sortingLayerName = EnvironmentLayers.Level;
        renderer.sortingOrder = style.SortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (style.Stretch > 0f) {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = style.Stretch;
            renderer.lengthScale = 1f;
            renderer.cameraVelocityScale = 0f;
        }
        else {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
