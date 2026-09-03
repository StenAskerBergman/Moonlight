using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot audiovisual weight feedback for a successfully placed building.
/// The effect is generated at runtime so every building using the shared placer gets it
/// without requiring another component or prefab reference on every building prefab.
/// </summary>
public sealed class BuildingPlacementImpact : MonoBehaviour
{
    private const float SoundDelay = 0.025f;
    private static Material landMaterial;
    private static Material underwaterMaterial;
    private static AudioClip impactClip;

    public static void Play(GameObject building, GridSystem grid, Vector2Int footprint, bool underwater)
    {
        if (building == null) return;

        GameObject root = new GameObject(underwater
            ? "Underwater Building Placement Impact"
            : "Building Placement Impact");
        root.transform.position = building.transform.position;

        BuildingPlacementImpact impact = root.AddComponent<BuildingPlacementImpact>();
        impact.CreatePerimeterBursts(grid, footprint, underwater);
        impact.StartCoroutine(impact.PlaySoundThenDispose(underwater));
    }

    private void CreatePerimeterBursts(GridSystem grid, Vector2Int footprint, bool underwater)
    {
        float cellSize = grid != null ? Mathf.Max(0.1f, grid.cellSize) : 1f;
        float halfX = Mathf.Max(0.5f, footprint.x * cellSize * 0.5f);
        float halfZ = Mathf.Max(0.5f, footprint.y * cellSize * 0.5f);
        Vector3 right = grid != null ? grid.transform.right : Vector3.right;
        Vector3 forward = grid != null ? grid.transform.forward : Vector3.forward;

        CreateBurst("East", right * halfX, right, footprint.y, underwater);
        CreateBurst("West", -right * halfX, -right, footprint.y, underwater);
        CreateBurst("North", forward * halfZ, forward, footprint.x, underwater);
        CreateBurst("South", -forward * halfZ, -forward, footprint.x, underwater);
    }

    private void CreateBurst(string side, Vector3 offset, Vector3 outward, int edgeCells, bool underwater)
    {
        GameObject emitter = new GameObject(side + " Displacement");
        emitter.transform.SetParent(transform, false);
        emitter.transform.localPosition = offset + Vector3.up * (underwater ? 0.08f : 0.03f);
        emitter.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

        ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.12f;
        main.startLifetime = underwater
            ? new ParticleSystem.MinMaxCurve(0.35f, 0.55f)
            : new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSpeed = underwater
            ? new ParticleSystem.MinMaxCurve(0.25f, 0.7f)
            : new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = underwater
            ? new ParticleSystem.MinMaxCurve(0.35f, 0.75f)
            : new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = underwater
            ? new ParticleSystem.MinMaxGradient(
                new Color(0.32f, 0.45f, 0.43f, 0.52f),
                new Color(0.53f, 0.56f, 0.43f, 0.35f))
            : new ParticleSystem.MinMaxGradient(
                new Color(0.28f, 0.20f, 0.12f, 0.8f),
                new Color(0.58f, 0.46f, 0.30f, 0.62f));
        main.gravityModifier = underwater
            ? new ParticleSystem.MinMaxCurve(-0.02f, 0.03f)
            : new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.5f, edgeCells * 0.82f), 0.08f, 0.12f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.55f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fade;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.25f, 1f, underwater ? 1.8f : 1.35f));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = underwater ? 0.22f : 0.38f;
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.2f;

        ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = GetParticleMaterial(underwater);

        particles.Play();
        particles.Emit(Mathf.Clamp(edgeCells * (underwater ? 7 : 5), 8, 36));
    }

    private IEnumerator PlaySoundThenDispose(bool underwater)
    {
        yield return new WaitForSeconds(SoundDelay);

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = 45f;
        source.volume = underwater ? 0.42f : 0.58f;
        source.pitch = underwater ? 0.72f : Random.Range(0.92f, 1.04f);
        source.PlayOneShot(GetImpactClip());

        Destroy(gameObject, 1.25f);
    }

    private static Material GetParticleMaterial(bool underwater)
    {
        Material cached = underwater ? underwaterMaterial : landMaterial;
        if (cached != null) return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        cached = new Material(shader) { name = underwater ? "Underwater Sediment VFX" : "Land Dust VFX" };
        cached.renderQueue = (int)RenderQueue.Transparent;
        if (cached.HasProperty("_Surface")) cached.SetFloat("_Surface", 1f);
        if (cached.HasProperty("_ZWrite")) cached.SetFloat("_ZWrite", 0f);

        if (underwater) underwaterMaterial = cached;
        else landMaterial = cached;
        return cached;
    }

    private static AudioClip GetImpactClip()
    {
        if (impactClip != null) return impactClip;

        const int sampleRate = 22050;
        const float length = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * length);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 12f);
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(74f, 43f, t / length) * t);
            float grit = (Random.value * 2f - 1f) * Mathf.Exp(-t * 32f);
            samples[i] = Mathf.Clamp((body * 0.72f + grit * 0.28f) * envelope, -1f, 1f);
        }

        impactClip = AudioClip.Create("Building Placement Thud", sampleCount, 1, sampleRate, false);
        impactClip.SetData(samples, 0);
        return impactClip;
    }
}
