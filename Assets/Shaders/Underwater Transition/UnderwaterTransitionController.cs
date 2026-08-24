using UnityEngine;

namespace Moonlight.Rendering
{
    [DisallowMultipleComponent]
    public sealed class UnderwaterTransitionController : MonoBehaviour
    {
        [Header("Water Detection")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional. Its Y position is used as the surface height.")]
        [SerializeField] private Transform waterSurface;
        [SerializeField] private float waterHeight;
        [Min(0f), SerializeField] private float surfaceHysteresis = 0.08f;

        [Header("Transition")]
        [Min(0.05f), SerializeField] private float diveDuration = 0.65f;
        [Min(0.05f), SerializeField] private float surfaceDuration = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Look")]
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

        private void OnEnable()
        {
            EnsureAudioSource();

            if (targetCamera == null)
                targetCamera = Camera.main;

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

        private float SurfaceHeight => waterSurface != null ? waterSurface.position.y : waterHeight;

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
        internal static bool ShouldRender => IsTransitioning || UnderwaterAmount > 0f;

        internal static void Reset()
        {
            TransitionAmount = 0f;
            UnderwaterAmount = 0f;
            IsTransitioning = false;
        }
    }
}
