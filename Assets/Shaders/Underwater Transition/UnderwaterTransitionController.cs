using System;
using UnityEngine;

/*
 Docs
 https://docs.unity3d.com/2022.3/Documentation/ScriptReference/RenderTexture.html
 https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/api/UnityEngine.Rendering.Universal.ScriptableRendererFeature.
*/

namespace Moonlight.Rendering
{
    [DisallowMultipleComponent]
    public sealed class UnderwaterTransitionController : MonoBehaviour
    {
        public enum TransitionPhase
        {
            Surfaced,
            DivingCover,
            DivingReveal,
            Underwater,
            SurfacingCover,
            SurfacingReveal
        }

        [Header("Water Transition")]
        public Animator transition;
        public float transistionTime = 1f;

        [Header("Water Detection")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional visible water object. Its renderer's upper bound is the authoritative surface height; transform Y is used when it has no renderer.")]
        [SerializeField] private Transform waterSurface;
        [SerializeField] private float waterHeight;
        [Min(0f), SerializeField] private float surfaceHysteresis = 0.08f;

        [Header("Dive Interaction Sync")]
        [Tooltip("Optional. When assigned, this unit's dive/surface events also drive the underwater transition (combined with camera height via OR). Can also be set at runtime via SetTrackedUnit.")]
        [SerializeField] private DiveInteraction trackedDiveUnit;

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
        [Tooltip("Wavelength absorption coefficients (RGB). Higher value = faster decay. Defaults approximate real seawater: red absorbs ~8x faster than blue, green ~2.5x faster.")]
        [SerializeField] private Vector4 absorptionCoefficients = new Vector4(0.16f, 0.05f, 0.02f, 0f);
        [Range(0.001f, 0.2f), SerializeField] private float fogDensity = 0.038f;
        [Min(0.5f), SerializeField] private float deepDepthThreshold = 6f;
        [Min(1f), SerializeField] private float abyssDepthThreshold = 14f;
        [Range(0f, 2f), SerializeField] private float sunScatteringIntensity = 0.65f;
        [Range(0.01f, 0.5f), SerializeField] private float sunDepthExtinction = 0.12f;

        [Header("Lower Apron Fade")]
        [SerializeField] private bool enableLowerApronFade = true;
        [Min(0f), SerializeField] private float lowerApronFadeStart = 10f;
        [Min(0.1f), SerializeField] private float lowerApronFadeEnd = 24f;
        [Range(0f, 1f), SerializeField] private float lowerApronFadeStrength = 0.95f;

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

        [Header("God Rays")]
        [Tooltip("Screen-space light shafts radiating from the surface. Fades out with depth.")]
        [Range(0f, 2f), SerializeField] private float godRayIntensity = 0.5f;

        [Header("Debris")]
        [Tooltip("Density of drifting sediment specks in the water column.")]
        [SerializeField] private bool enableDebris = true;
        [Range(0f, 3f), SerializeField] private float debrisDensity = 0.45f;
        [Range(0f, 1f), SerializeField] private float debrisBrightness = 0.28f;
        [Range(0f, 2f), SerializeField] private float debrisDriftSpeed = 0.45f;

        [Header("Surface Droplets")]
        [SerializeField] private bool enableSurfaceDroplets = true;
        [Range(0f, 2f), SerializeField] private float dropletIntensity = 0.8f;
        [Range(0.1f, 3f), SerializeField] private float dropletFallSpeed = 1.15f;

        [Header("Surface Optics")]
        [SerializeField] private Color underwaterColor = new Color(0.28f, 0.72f, 0.78f, 0.22f);
        [Range(0f, 0.08f), SerializeField] private float distortionStrength = 0.014f;
        [Range(0.005f, 0.25f), SerializeField] private float surfaceEdgeWidth = 0.07f;

        [Header("Pre-Crossing Timing")]
        [Tooltip("Anticipates transition before camera height crosses the water surface.")]
        [SerializeField] private bool enablePreCrossingTiming = true;
        [Tooltip("Distance in meters above/below the surface where pre-crossing anticipation initiates.")]
        [Range(0.05f, 2f), SerializeField] private float preCrossingDistance = 0.35f;

        [Header("HUD Concealment")]
        [Tooltip("Conceals or fades out the surface HUD/UI when transitioning into or submerged underwater.")]
        [SerializeField] private bool enableHudConcealment = true;
        [Tooltip("Dedicated fullscreen UI concealment layer above all HUD canvases. If unassigned, automatically retrieved or created at runtime.")]
        [SerializeField] private UnderwaterUIConcealmentLayer uiConcealmentLayer;
        [Tooltip("Target CanvasGroup for the surface HUD. If null, attempts to find a CanvasGroup on active HUD objects.")]
        [SerializeField] private CanvasGroup hudCanvasGroup;
        [Tooltip("Optional underwater-only HUD GameObject activated strictly while submerged.")]
        [SerializeField] private GameObject underwaterHudObject;
        [Range(0f, 1f), SerializeField] private float submergedHudAlpha = 0.0f;
        [Range(0f, 1f), SerializeField] private float surfacedHudAlpha = 1.0f;
        [SerializeField] private bool hideCanvasWhenFullySubmerged = false;

        [Header("Dive Audio")]
        [Tooltip("Optional. When empty, a procedural splash and bubble cue is generated at runtime.")]
        [SerializeField] private AudioClip diveSound;
        [Range(0f, 1f), SerializeField] private float diveVolume = 0.75f;
        [SerializeField] private bool playDiveSound = true;
        [Tooltip("Optional. When empty, a procedural surface splash is generated at runtime.")]
        [SerializeField] private AudioClip surfaceSound;
        [Range(0f, 1f), SerializeField] private float surfaceVolume = 0.7f;
        [SerializeField] private bool playSurfaceSound = true;

        [Header("Underwater Audio")]
        [SerializeField] private bool enableUnderwaterMuffling = true;
        [Range(1000f, 22000f), SerializeField] private float underwaterCutoffFrequency = 1800f;
        [Range(1000f, 22000f), SerializeField] private float surfacedCutoffFrequency = 22000f;
        [Range(1f, 10f), SerializeField] private float lowPassResonance = 1.25f;

        public event Action<bool> OnSubmersionChanged;

        private TransitionPhase currentPhase = TransitionPhase.Surfaced;
        private float phaseTimer;
        private float coverDuration;
        private float revealDuration;

        private bool isUnderwater;
        private bool isTransitioning;
        private AudioSource audioSource;
        private AudioClip generatedDiveSound;
        private AudioClip generatedSurfaceSound;
        private AudioLowPassFilter underwaterLowPassFilter;
        private Renderer waterSurfaceRenderer;

        private bool cameraSubmerged;
        private bool unitSubmerged;
        private DiveInteraction subscribedDiveUnit;

        public TransitionPhase CurrentPhase => currentPhase;
        public bool IsUnderwater => isUnderwater;
        public bool IsSubmerged => isUnderwater;
        public bool IsTransitioning => isTransitioning;
        public float TransitionCover { get; private set; }

        public bool IsCrossingConcealed => (currentPhase == TransitionPhase.DivingCover || currentPhase == TransitionPhase.SurfacingCover)
            ? (phaseTimer >= coverDuration * 0.95f)
            : (currentPhase == TransitionPhase.DivingReveal || currentPhase == TransitionPhase.SurfacingReveal);

        private void OnEnable()
        {
            EnsureAudioSource();

            if (targetCamera == null)
                targetCamera = Camera.main;

            EnsureUnderwaterAudioFilter();
            EnsureUiConcealmentLayer();
            ResolveWaterSurfaceRenderer();

            cameraSubmerged = targetCamera != null && targetCamera.transform.position.y < SurfaceHeight;
            unitSubmerged = trackedDiveUnit != null && trackedDiveUnit.IsSubmerged;
            isUnderwater = cameraSubmerged || unitSubmerged;

            currentPhase = isUnderwater ? TransitionPhase.Underwater : TransitionPhase.Surfaced;
            TransitionCover = 0f;
            isTransitioning = false;
            phaseTimer = 0f;

            SubscribeToTrackedUnit();
            SwitchHudState(isUnderwater);
            ApplyState();
            UpdateAudioMuffling();
            UpdateHudConcealment();
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            UpdateTransitionOrchestration();
            CheckProximityCrossingTriggers();

            ApplyState();
            UpdateAudioMuffling();
            UpdateHudConcealment();
        }

        private void UpdateTransitionOrchestration()
        {
            float dt = Time.unscaledDeltaTime;

            switch (currentPhase)
            {
                case TransitionPhase.DivingCover:
                {
                    phaseTimer += dt;
                    float progress = Mathf.Clamp01(phaseTimer / coverDuration);
                    TransitionCover = transitionCurve.Evaluate(progress);

                    // Waterline sweeps from bottom toward top in shader during dive cover
                    UnderwaterTransitionState.Direction = 1f;
                    UnderwaterTransitionState.TransitionProgress = progress * 0.95f;
                    UnderwaterTransitionState.TransitionAmount = progress;

                    // Strictly prevent camera from crossing sea level until fully concealed
                    ClampCameraAboveWater();

                    if (phaseTimer >= coverDuration)
                    {
                        CommitToUnderwater();
                    }
                    break;
                }

                case TransitionPhase.DivingReveal:
                {
                    phaseTimer += dt;
                    float progress = Mathf.Clamp01(phaseTimer / revealDuration);
                    TransitionCover = 1f - transitionCurve.Evaluate(progress);

                    UnderwaterTransitionState.Direction = 1f;
                    UnderwaterTransitionState.TransitionProgress = 1f;
                    UnderwaterTransitionState.TransitionAmount = 1f;

                    if (phaseTimer >= revealDuration)
                    {
                        currentPhase = TransitionPhase.Underwater;
                        TransitionCover = 0f;
                        isTransitioning = false;
                    }
                    break;
                }

                case TransitionPhase.SurfacingCover:
                {
                    phaseTimer += dt;
                    float progress = Mathf.Clamp01(phaseTimer / coverDuration);
                    TransitionCover = transitionCurve.Evaluate(progress);

                    UnderwaterTransitionState.Direction = -1f;
                    UnderwaterTransitionState.TransitionProgress = 1f - progress * 0.95f;
                    UnderwaterTransitionState.TransitionAmount = progress;

                    // Strictly prevent camera from crossing sea level until fully concealed
                    ClampCameraBelowWater();

                    if (phaseTimer >= coverDuration)
                    {
                        CommitToSurfaced();
                    }
                    break;
                }

                case TransitionPhase.SurfacingReveal:
                {
                    phaseTimer += dt;
                    float progress = Mathf.Clamp01(phaseTimer / revealDuration);
                    TransitionCover = 1f - transitionCurve.Evaluate(progress);

                    UnderwaterTransitionState.Direction = -1f;
                    UnderwaterTransitionState.TransitionProgress = 0f;
                    UnderwaterTransitionState.TransitionAmount = 1f - progress;

                    if (phaseTimer >= revealDuration)
                    {
                        currentPhase = TransitionPhase.Surfaced;
                        TransitionCover = 0f;
                        isTransitioning = false;
                    }
                    break;
                }

                case TransitionPhase.Surfaced:
                case TransitionPhase.Underwater:
                default:
                {
                    TransitionCover = 0f;
                    isTransitioning = false;
                    break;
                }
            }
        }

        private float lastCameraY;
        private bool hasLastCameraY;

        private void CheckProximityCrossingTriggers()
        {
            if (isTransitioning)
            {
                if (targetCamera != null)
                    lastCameraY = targetCamera.transform.position.y;
                return;
            }

            // Tracked unit submersion command takes priority
            if (trackedDiveUnit != null)
            {
                if (!isUnderwater && unitSubmerged && currentPhase == TransitionPhase.Surfaced)
                {
                    RequestDive();
                    return;
                }
                if (isUnderwater && !unitSubmerged && currentPhase == TransitionPhase.Underwater)
                {
                    RequestSurface();
                    return;
                }
            }

            // Camera directional crossing anticipation for unguided cameras
            if (enablePreCrossingTiming && targetCamera != null)
            {
                float cameraY = targetCamera.transform.position.y;
                float surface = SurfaceHeight;

                if (hasLastCameraY)
                {
                    // Only trigger dive if camera was above the anticipation threshold and crossed below it heading downward
                    if (!isUnderwater && currentPhase == TransitionPhase.Surfaced)
                    {
                        float diveThreshold = surface + preCrossingDistance;
                        if (lastCameraY >= diveThreshold && cameraY < diveThreshold && cameraY < lastCameraY)
                        {
                            RequestDive();
                        }
                    }
                    // Only trigger surface if camera was below the anticipation threshold and crossed above it heading upward
                    else if (isUnderwater && currentPhase == TransitionPhase.Underwater)
                    {
                        float surfaceThreshold = surface - preCrossingDistance;
                        if (lastCameraY <= surfaceThreshold && cameraY > surfaceThreshold && cameraY > lastCameraY)
                        {
                            RequestSurface();
                        }
                    }
                }

                lastCameraY = cameraY;
                hasLastCameraY = true;
            }
        }

        private void ClampCameraAboveWater()
        {
            if (targetCamera == null) return;
            float minSafeY = SurfaceHeight + 0.05f;
            if (targetCamera.transform.position.y < minSafeY)
            {
                Vector3 pos = targetCamera.transform.position;
                pos.y = minSafeY;
                targetCamera.transform.position = pos;
            }
        }

        private void ClampCameraBelowWater()
        {
            if (targetCamera == null) return;
            float maxSafeY = SurfaceHeight - 0.05f;
            if (targetCamera.transform.position.y > maxSafeY)
            {
                Vector3 pos = targetCamera.transform.position;
                pos.y = maxSafeY;
                targetCamera.transform.position = pos;
            }
        }

        private void CommitToUnderwater()
        {
            isUnderwater = true;
            cameraSubmerged = true;
            PlayDiveCue();
            SwitchHudState(true);
            OnSubmersionChanged?.Invoke(true);

            currentPhase = TransitionPhase.DivingReveal;
            phaseTimer = 0f;

            if (targetCamera != null)
            {
                lastCameraY = targetCamera.transform.position.y;
                hasLastCameraY = true;
            }
        }

        private void CommitToSurfaced()
        {
            isUnderwater = false;
            cameraSubmerged = false;
            PlaySurfaceCue();
            SwitchHudState(false);
            OnSubmersionChanged?.Invoke(false);

            currentPhase = TransitionPhase.SurfacingReveal;
            phaseTimer = 0f;

            if (targetCamera != null)
            {
                lastCameraY = targetCamera.transform.position.y;
                hasLastCameraY = true;
            }
        }

        public void RequestDive() => Dive();
        public void RequestSurface() => Surface();

        public void Dive()
        {
            if (isUnderwater && currentPhase == TransitionPhase.Underwater)
                return;
            if (currentPhase == TransitionPhase.DivingCover || currentPhase == TransitionPhase.DivingReveal)
                return;

            currentPhase = TransitionPhase.DivingCover;
            phaseTimer = 0f;
            coverDuration = Mathf.Max(0.05f, diveDuration * 0.5f);
            revealDuration = Mathf.Max(0.05f, diveDuration * 0.5f);
            isTransitioning = true;
            EnsureUiConcealmentLayer();
        }

        public void Surface()
        {
            if (!isUnderwater && currentPhase == TransitionPhase.Surfaced)
                return;
            if (currentPhase == TransitionPhase.SurfacingCover || currentPhase == TransitionPhase.SurfacingReveal)
                return;

            currentPhase = TransitionPhase.SurfacingCover;
            phaseTimer = 0f;
            coverDuration = Mathf.Max(0.05f, surfaceDuration * 0.5f);
            revealDuration = Mathf.Max(0.05f, surfaceDuration * 0.5f);
            isTransitioning = true;
            EnsureUiConcealmentLayer();
        }

        /// <summary>
        /// Guards proposed camera world Y against crossing sea level before full transition concealment.
        /// </summary>
        public float ConstrainCameraY(float proposedWorldY)
        {
            float surface = SurfaceHeight;
            if (!isUnderwater || currentPhase == TransitionPhase.DivingCover)
            {
                return Mathf.Max(proposedWorldY, surface + 0.05f);
            }
            if (isUnderwater && currentPhase == TransitionPhase.SurfacingCover)
            {
                return Mathf.Min(proposedWorldY, surface - 0.05f);
            }
            return proposedWorldY;
        }

        private void SwitchHudState(bool submerged)
        {
            ResolveHudCanvasGroup();

            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = submerged ? submergedHudAlpha : surfacedHudAlpha;
                if (hideCanvasWhenFullySubmerged)
                    hudCanvasGroup.gameObject.SetActive(!submerged);
            }

            if (underwaterHudObject != null)
            {
                underwaterHudObject.SetActive(submerged);
            }
        }

        private void ResolveHudCanvasGroup()
        {
            if (hudCanvasGroup == null && Application.isPlaying)
            {
                var hudObj = GameObject.Find("HUD") ?? GameObject.Find("UI");
                if (hudObj != null)
                    hudCanvasGroup = hudObj.GetComponent<CanvasGroup>();
            }
        }

        private void EnsureUiConcealmentLayer()
        {
            if (uiConcealmentLayer == null)
            {
                uiConcealmentLayer = UnderwaterUIConcealmentLayer.GetOrCreate(transform);
            }
        }

        private void UpdateHudConcealment()
        {
            if (!enableHudConcealment)
            {
                if (uiConcealmentLayer != null)
                    uiConcealmentLayer.SetCoverAmount(0f);
                return;
            }

            EnsureUiConcealmentLayer();
            if (uiConcealmentLayer != null)
            {
                Color veil = Color.Lerp(deepWaterColor, underwaterColor, 0.4f);
                veil.a = 1f;
                uiConcealmentLayer.SetVeilColor(veil);
                uiConcealmentLayer.SetCoverAmount(TransitionCover);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromTrackedUnit();
            UnderwaterTransitionState.Reset();

            if (underwaterLowPassFilter != null)
                underwaterLowPassFilter.enabled = false;

            if (uiConcealmentLayer != null)
                uiConcealmentLayer.SetCoverAmount(0f);

            // Avoid calling GameObject.Find during teardown/OnDisable
            if (hudCanvasGroup != null && hudCanvasGroup.gameObject != null)
            {
                hudCanvasGroup.alpha = surfacedHudAlpha;
                if (hideCanvasWhenFullySubmerged)
                    hudCanvasGroup.gameObject.SetActive(true);
            }

            if (underwaterHudObject != null)
            {
                underwaterHudObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (generatedDiveSound != null)
                Destroy(generatedDiveSound);
            if (generatedSurfaceSound != null)
                Destroy(generatedSurfaceSound);
        }

        public void Configure(Camera camera, float surfaceHeight)
        {
            targetCamera = camera;
            waterSurface = null;
            waterSurfaceRenderer = null;
            waterHeight = surfaceHeight;
            cameraSubmerged = targetCamera != null && targetCamera.transform.position.y < SurfaceHeight;
            isUnderwater = cameraSubmerged || unitSubmerged;
            currentPhase = isUnderwater ? TransitionPhase.Underwater : TransitionPhase.Surfaced;
            TransitionCover = 0f;
            isTransitioning = false;
            phaseTimer = 0f;
            SwitchHudState(isUnderwater);
            ApplyState();
        }

        public void SetTrackedUnit(DiveInteraction unit)
        {
            UnsubscribeFromTrackedUnit();
            trackedDiveUnit = unit;
            unitSubmerged = trackedDiveUnit != null && trackedDiveUnit.IsSubmerged;
            SubscribeToTrackedUnit();
        }

        private void SubscribeToTrackedUnit()
        {
            if (trackedDiveUnit == null || subscribedDiveUnit == trackedDiveUnit)
                return;

            subscribedDiveUnit = trackedDiveUnit;
            subscribedDiveUnit.OnDiveStateChanged += HandleDiveStateChanged;
        }

        private void UnsubscribeFromTrackedUnit()
        {
            if (subscribedDiveUnit == null)
                return;

            subscribedDiveUnit.OnDiveStateChanged -= HandleDiveStateChanged;
            subscribedDiveUnit = null;
        }

        private void HandleDiveStateChanged(bool isSub)
        {
            unitSubmerged = isSub;
            if (unitSubmerged && !isUnderwater)
                RequestDive();
            else if (!unitSubmerged && isUnderwater)
                RequestSurface();
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

        private void PlaySurfaceCue()
        {
            if (!playSurfaceSound)
                return;

            EnsureAudioSource();
            AudioClip clip = surfaceSound;
            if (clip == null)
            {
                if (generatedSurfaceSound == null)
                    generatedSurfaceSound = CreateProceduralSurfaceSound();
                clip = generatedSurfaceSound;
            }

            if (clip != null)
                audioSource.PlayOneShot(clip, surfaceVolume);
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

        private static AudioClip CreateProceduralSurfaceSound()
        {
            const int sampleRate = 44100;
            const float duration = 0.9f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            uint noiseState = 0x6D2B79F5u;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388607.5f) - 1f;

                float splashEnvelope = Mathf.Exp(-time * 7.5f) * Mathf.Clamp01(time * 55f);
                float sheet = noise * splashEnvelope * 0.42f;
                float crest = Mathf.Sin(2f * Mathf.PI * (115f - time * 48f) * time)
                    * Mathf.Exp(-time * 5.5f) * 0.24f;
                float droplets = Mathf.Sin(2f * Mathf.PI * (420f + time * 180f) * time)
                    * Mathf.Exp(-time * 8f) * 0.08f;
                samples[i] = Mathf.Clamp(sheet + crest + droplets, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Surface Splash", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void EnsureUnderwaterAudioFilter()
        {
            if (!enableUnderwaterMuffling || targetCamera == null)
                return;

            underwaterLowPassFilter = targetCamera.GetComponent<AudioLowPassFilter>();
            if (underwaterLowPassFilter == null)
                underwaterLowPassFilter = targetCamera.gameObject.AddComponent<AudioLowPassFilter>();
        }

        private void UpdateAudioMuffling()
        {
            if (!enableUnderwaterMuffling)
            {
                if (underwaterLowPassFilter != null)
                    underwaterLowPassFilter.enabled = false;
                return;
            }

            EnsureUnderwaterAudioFilter();
            if (underwaterLowPassFilter == null)
                return;

            underwaterLowPassFilter.enabled = true;
            float blend = Mathf.SmoothStep(0f, 1f, UnderwaterTransitionState.TransitionProgress);
            underwaterLowPassFilter.cutoffFrequency = Mathf.Lerp(surfacedCutoffFrequency, underwaterCutoffFrequency, blend);
            underwaterLowPassFilter.lowpassResonanceQ = lowPassResonance;
        }

        private void ApplyState()
        {
            UnderwaterTransitionState.TargetCamera = targetCamera;
            UnderwaterTransitionState.UnderwaterAmount = isUnderwater ? 1f : 0f;
            UnderwaterTransitionState.Color = underwaterColor;
            UnderwaterTransitionState.DistortionStrength = distortionStrength;
            UnderwaterTransitionState.EdgeWidth = surfaceEdgeWidth;
            UnderwaterTransitionState.IsTransitioning = isTransitioning;
            UnderwaterTransitionState.WaterLevel = SurfaceHeight;

            UnderwaterTransitionState.ShallowWaterColor = shallowWaterColor;
            UnderwaterTransitionState.DeepWaterColor = deepWaterColor;
            UnderwaterTransitionState.AbyssalColor = abyssalColor;
            UnderwaterTransitionState.AbsorptionCoefficients = absorptionCoefficients;
            UnderwaterTransitionState.FogDensity = fogDensity;
            UnderwaterTransitionState.DeepDepthThreshold = deepDepthThreshold;
            UnderwaterTransitionState.AbyssDepthThreshold = abyssDepthThreshold;
            UnderwaterTransitionState.SunScatteringIntensity = sunScatteringIntensity;
            UnderwaterTransitionState.SunDepthExtinction = sunDepthExtinction;

            UnderwaterTransitionState.LowerApronFadeStrength = enableLowerApronFade ? lowerApronFadeStrength : 0f;
            UnderwaterTransitionState.LowerApronFadeStart = lowerApronFadeStart;
            UnderwaterTransitionState.LowerApronFadeEnd = Mathf.Max(lowerApronFadeStart + 0.1f, lowerApronFadeEnd);

            UnderwaterTransitionState.EnableCaustics = enableCaustics;
            UnderwaterTransitionState.CausticsStrength = enableCaustics ? causticsStrength : 0f;
            UnderwaterTransitionState.CausticsScale = causticsScale;
            UnderwaterTransitionState.CausticsSpeed = causticsSpeed;
            UnderwaterTransitionState.CausticsFadeDepth = causticsFadeDepth;

            UnderwaterTransitionState.EnableMarineSnow = enableMarineSnow;
            UnderwaterTransitionState.MarineSnowIntensity = enableMarineSnow ? marineSnowIntensity : 0f;
            UnderwaterTransitionState.MarineSnowScale = marineSnowScale;
            UnderwaterTransitionState.MarineSnowSpeed = marineSnowSpeed;

            UnderwaterTransitionState.GodRayIntensity = godRayIntensity;
            UnderwaterTransitionState.DebrisDensity = enableDebris ? debrisDensity : 0f;
            UnderwaterTransitionState.DebrisBrightness = debrisBrightness;
            UnderwaterTransitionState.DebrisDriftSpeed = debrisDriftSpeed;

            UnderwaterTransitionState.DropletIntensity = enableSurfaceDroplets ? dropletIntensity : 0f;
            UnderwaterTransitionState.DropletFallSpeed = dropletFallSpeed;
        }
    }

    internal static class UnderwaterTransitionState
    {
        internal static Camera TargetCamera;
        internal static float TransitionAmount;
        internal static float Direction = 1f;
        internal static float UnderwaterAmount;
        internal static float TransitionProgress;
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

        internal static float LowerApronFadeStart = 10f;
        internal static float LowerApronFadeEnd = 24f;
        internal static float LowerApronFadeStrength = 0.95f;

        internal static bool EnableCaustics = true;
        internal static float CausticsStrength = 0.45f;
        internal static float CausticsScale = 0.35f;
        internal static float CausticsSpeed = 0.4f;
        internal static float CausticsFadeDepth = 9f;

        internal static bool EnableMarineSnow = true;
        internal static float MarineSnowIntensity = 0.45f;
        internal static float MarineSnowScale = 1.2f;
        internal static float MarineSnowSpeed = 0.08f;

        internal static float GodRayIntensity = 0.5f;
        internal static float DebrisDensity = 0.45f;
        internal static float DebrisBrightness = 0.28f;
        internal static float DebrisDriftSpeed = 0.45f;

        internal static float DropletIntensity = 0.8f;
        internal static float DropletFallSpeed = 1.15f;

        internal static bool ShouldRender => IsTransitioning || UnderwaterAmount > 0f || TransitionProgress > 0f;

        internal static void Reset()
        {
            TargetCamera = null;
            TransitionAmount = 0f;
            UnderwaterAmount = 0f;
            TransitionProgress = 0f;
            IsTransitioning = false;
        }
    }
}
