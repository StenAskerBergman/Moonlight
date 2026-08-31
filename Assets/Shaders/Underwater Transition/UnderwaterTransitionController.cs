using UnityEngine;

namespace Moonlight.Rendering
{
    [DisallowMultipleComponent]
    public sealed class UnderwaterTransitionController : MonoBehaviour
    {
        [Header("Water Detection")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional visible water object. Its renderer's upper bound is the authoritative surface height; transform Y is used when it has no renderer.")]
        [SerializeField] private Transform waterSurface;
        [SerializeField] private float waterHeight;
        [Min(0f), SerializeField] private float surfaceHysteresis = 0.08f;

        [Header("Transition")]
        [Min(0.05f), SerializeField] private float diveDuration = 0.65f;
        [Min(0.05f), SerializeField] private float surfaceDuration = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Bathymetry Fog & Scattering")]
        [Tooltip("Light vibrant turquoise near the water surface.")]
        [SerializeField] private Color shallowWaterColor = new Color(0.12f, 0.72f, 0.78f, 1f);
        [Tooltip("Rich deep teal at intermediate depths.")]
        [SerializeField] private Color deepWaterColor = new Color(0.025f, 0.22f, 0.29f, 1f);
        [Tooltip("Dark ocean void / black at abyssal depths.")]
        [SerializeField] private Color abyssalColor = new Color(0.002f, 0.035f, 0.055f, 1f);
        [Tooltip("Wavelength absorption coefficients (RGB). Higher value = faster decay.")]
        [SerializeField] private Vector4 absorptionCoefficients = new Vector4(0.24f, 0.065f, 0.02f, 0f);
        [Range(0.001f, 0.2f), SerializeField] private float fogDensity = 0.038f;
        [Min(0.5f), SerializeField] private float deepDepthThreshold = 6f;
        [Min(1f), SerializeField] private float abyssDepthThreshold = 14f;
        [Range(0f, 2f), SerializeField] private float sunScatteringIntensity = 0.65f;
        [Range(0.01f, 0.5f), SerializeField] private float sunDepthExtinction = 0.12f;

        [Header("Caustics")]
        [SerializeField] private bool enableCaustics = true;
        [Range(0f, 2f), SerializeField] private float causticsStrength = 0.45f;
        [Range(0.05f, 2f), SerializeField] private float causticsScale = 0.35f;
        [Range(0.05f, 2f), SerializeField] private float causticsSpeed = 0.4f;
        [Min(1f), SerializeField] private float causticsFadeDepth = 9f;

        [Header("Marine Snow & Particles")]
        [SerializeField] private bool enableMarineSnow = true;
        [Range(0f, 2f), SerializeField] private float marineSnowIntensity = 0.45f;
        [Range(0.1f, 5f), SerializeField] private float marineSnowScale = 1.2f;
        [Range(0.01f, 0.5f), SerializeField] private float marineSnowSpeed = 0.08f;

        [Header("Surface Optics")]
        [SerializeField] private Color underwaterColor = new Color(0.28f, 0.72f, 0.78f, 0.22f);
        [Range(0f, 0.08f), SerializeField] private float distortionStrength = 0.014f;
        [Range(0.005f, 0.25f), SerializeField] private float surfaceEdgeWidth = 0.07f;

        [Header("Dive Audio")]
        [Tooltip("Optional. When empty, a procedural splash and bubble cue is generated at runtime.")]
        [SerializeField] private AudioClip diveSound;
        [Range(0f, 1f), SerializeField] private float diveVolume = 0.75f;
        [SerializeField] private bool playDiveSound = true;

        private bool isUnderwater;
        private bool isTransitioning;
        private float transitionTime;
        private float transitionDuration;
        private AudioSource audioSource;
        private AudioClip generatedDiveSound;
        private Renderer waterSurfaceRenderer;

        private void OnEnable()
        {
            EnsureAudioSource();

            if (targetCamera == null)
                targetCamera = Camera.main;

            ResolveWaterSurfaceRenderer();

            isUnderwater = targetCamera != null && targetCamera.transform.position.y < SurfaceHeight;
            ApplyState();
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                float cameraY = targetCamera.transform.position.y;
                if (!isUnderwater && cameraY < SurfaceHeight - surfaceHysteresis)
                    Dive();
                else if (isUnderwater && cameraY > SurfaceHeight + surfaceHysteresis)
                    Surface();
            }

            if (isTransitioning)
            {
                transitionTime += Time.unscaledDeltaTime;
                UnderwaterTransitionState.TransitionAmount = transitionCurve.Evaluate(
                    Mathf.Clamp01(transitionTime / transitionDuration));
                if (transitionTime >= transitionDuration)
                    isTransitioning = false;
            }

            ApplyState();
        }

        private void OnDisable() => UnderwaterTransitionState.Reset();

        private void OnDestroy()
        {
            if (generatedDiveSound != null)
                Destroy(generatedDiveSound);
        }

        public void Configure(Camera camera, float surfaceHeight)
        {
            targetCamera = camera;
            waterSurface = null;
            waterSurfaceRenderer = null;
            waterHeight = surfaceHeight;
            isUnderwater = targetCamera != null && targetCamera.transform.position.y < SurfaceHeight;
            ApplyState();
        }

        public void Dive()
        {
            if (isUnderwater && !isTransitioning)
                return;
            isUnderwater = true;
            PlayDiveCue();
            BeginTransition(1f, diveDuration);
        }

        public void Surface()
        {
            if (!isUnderwater && !isTransitioning)
                return;
            isUnderwater = false;
            BeginTransition(-1f, surfaceDuration);
        }

        private float SurfaceHeight
        {
            get
            {
                if (waterSurface == null) return waterHeight;
                ResolveWaterSurfaceRenderer();
                return waterSurfaceRenderer != null
                    ? waterSurfaceRenderer.bounds.max.y
                    : waterSurface.position.y;
            }
        }

        private void ResolveWaterSurfaceRenderer()
        {
            if (waterSurface == null)
            {
                waterSurfaceRenderer = null;
                return;
            }

            if (waterSurfaceRenderer == null || waterSurfaceRenderer.transform != waterSurface)
            {
                waterSurfaceRenderer = waterSurface.GetComponent<Renderer>();
            }
        }

        private void BeginTransition(float direction, float duration)
        {
            UnderwaterTransitionState.Direction = direction;
            UnderwaterTransitionState.TransitionAmount = 0f;
            transitionTime = 0f;
            transitionDuration = duration;
            isTransitioning = true;
        }

        private void EnsureAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        private void PlayDiveCue()
        {
            if (!playDiveSound)
                return;

            EnsureAudioSource();
            AudioClip clip = diveSound;
            if (clip == null)
            {
                if (generatedDiveSound == null)
                    generatedDiveSound = CreateProceduralDiveSound();
                clip = generatedDiveSound;
            }

            if (clip != null)
                audioSource.PlayOneShot(clip, diveVolume);
        }

        private static AudioClip CreateProceduralDiveSound()
        {
            const int sampleRate = 44100;
            const float duration = 1.15f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            uint noiseState = 0x9E3779B9u;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388607.5f) - 1f;

                float splashEnvelope = Mathf.Exp(-time * 9f) * Mathf.Clamp01(time * 70f);
                float splash = noise * splashEnvelope * 0.55f;

                float bubbles = 0f;
                for (int bubble = 0; bubble < 7; bubble++)
                {
                    float start = 0.08f + bubble * 0.105f;
                    float age = time - start;
                    if (age < 0f || age > 0.22f)
                        continue;

                    float envelope = Mathf.Sin(age / 0.22f * Mathf.PI);
                    float frequency = 310f + bubble * 47f + age * 620f;
                    bubbles += Mathf.Sin(2f * Mathf.PI * frequency * age) * envelope * 0.085f;
                }

                float lowPlunge = Mathf.Sin(2f * Mathf.PI * (72f - time * 24f) * time)
                    * Mathf.Exp(-time * 4.2f) * 0.2f;
                samples[i] = Mathf.Clamp(splash + bubbles + lowPlunge, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Camera Dive", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void ApplyState()
        {
            // The moving waterline owns the tint during a transition. This avoids
            // snapping the whole frame blue on the exact crossing frame.
            UnderwaterTransitionState.UnderwaterAmount = !isTransitioning && isUnderwater ? 1f : 0f;
            UnderwaterTransitionState.Color = underwaterColor;
            UnderwaterTransitionState.DistortionStrength = distortionStrength;
            UnderwaterTransitionState.EdgeWidth = surfaceEdgeWidth;
            UnderwaterTransitionState.IsTransitioning = isTransitioning;
            UnderwaterTransitionState.WaterLevel = SurfaceHeight;

            // Fog & Bathymetry parameters
            UnderwaterTransitionState.ShallowWaterColor = shallowWaterColor;
            UnderwaterTransitionState.DeepWaterColor = deepWaterColor;
            UnderwaterTransitionState.AbyssalColor = abyssalColor;
            UnderwaterTransitionState.AbsorptionCoefficients = absorptionCoefficients;
            UnderwaterTransitionState.FogDensity = fogDensity;
            UnderwaterTransitionState.DeepDepthThreshold = deepDepthThreshold;
            UnderwaterTransitionState.AbyssDepthThreshold = abyssDepthThreshold;
            UnderwaterTransitionState.SunScatteringIntensity = sunScatteringIntensity;
            UnderwaterTransitionState.SunDepthExtinction = sunDepthExtinction;

            // Caustics
            UnderwaterTransitionState.EnableCaustics = enableCaustics;
            UnderwaterTransitionState.CausticsStrength = enableCaustics ? causticsStrength : 0f;
            UnderwaterTransitionState.CausticsScale = causticsScale;
            UnderwaterTransitionState.CausticsSpeed = causticsSpeed;
            UnderwaterTransitionState.CausticsFadeDepth = causticsFadeDepth;

            // Marine Snow
            UnderwaterTransitionState.EnableMarineSnow = enableMarineSnow;
            UnderwaterTransitionState.MarineSnowIntensity = enableMarineSnow ? marineSnowIntensity : 0f;
            UnderwaterTransitionState.MarineSnowScale = marineSnowScale;
            UnderwaterTransitionState.MarineSnowSpeed = marineSnowSpeed;
        }
    }

    internal static class UnderwaterTransitionState
    {
        internal static float TransitionAmount;
        internal static float Direction = 1f;
        internal static float UnderwaterAmount;
        internal static Color Color = Color.white;
        internal static float DistortionStrength;
        internal static float EdgeWidth = 0.07f;
        internal static bool IsTransitioning;
        internal static float WaterLevel = 0f;

        internal static Color ShallowWaterColor = new Color(0.12f, 0.72f, 0.78f, 1f);
        internal static Color DeepWaterColor = new Color(0.025f, 0.22f, 0.29f, 1f);
        internal static Color AbyssalColor = new Color(0.002f, 0.035f, 0.055f, 1f);
        internal static Vector4 AbsorptionCoefficients = new Vector4(0.24f, 0.065f, 0.02f, 0f);
        internal static float FogDensity = 0.038f;
        internal static float DeepDepthThreshold = 6f;
        internal static float AbyssDepthThreshold = 14f;
        internal static float SunScatteringIntensity = 0.65f;
        internal static float SunDepthExtinction = 0.12f;

        internal static bool EnableCaustics = true;
        internal static float CausticsStrength = 0.45f;
        internal static float CausticsScale = 0.35f;
        internal static float CausticsSpeed = 0.4f;
        internal static float CausticsFadeDepth = 9f;

        internal static bool EnableMarineSnow = true;
        internal static float MarineSnowIntensity = 0.45f;
        internal static float MarineSnowScale = 1.2f;
        internal static float MarineSnowSpeed = 0.08f;

        internal static bool ShouldRender => IsTransitioning || UnderwaterAmount > 0f;

        internal static void Reset()
        {
            TransitionAmount = 0f;
            UnderwaterAmount = 0f;
            IsTransitioning = false;
        }
    }
}
