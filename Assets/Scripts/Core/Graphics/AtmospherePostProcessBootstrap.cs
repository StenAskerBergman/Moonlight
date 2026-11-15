using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures a lightweight global URP post-processing Volume with subtle HDR Bloom exists in scenes
/// that omit one, and enables post-processing on base cameras so blueprint rims, laser seams, and
/// dependency highlights bloom crisply without blowing out terrain or water.
/// </summary>
public static class AtmospherePostProcessBootstrap
{
    private static Volume runtimeVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyToActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToActiveScene();
    }

    private static void ApplyToActiveScene()
    {
        EnsureGlobalBloomVolume();
        EnableBaseCameraPostProcessing();
    }

    private static void EnsureGlobalBloomVolume()
    {
        Volume[] existingVolumes = Object.FindObjectsOfType<Volume>(true);
        for (int i = 0; i < existingVolumes.Length; i++)
        {
            Volume vol = existingVolumes[i];
            if (vol == null || vol == runtimeVolume) continue;
            if (vol.isGlobal && vol.sharedProfile != null && vol.sharedProfile.Has<Bloom>())
            {
                return;
            }
        }

        if (runtimeVolume != null) return;

        GameObject host = new GameObject("Moonlight Global Bloom Volume");
        Object.DontDestroyOnLoad(host);

        runtimeVolume = host.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = -5f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Moonlight Runtime Bloom Profile";
        profile.hideFlags = HideFlags.HideAndDontSave;

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.08f);
        bloom.intensity.Override(0.26f);
        bloom.scatter.Override(0.55f);
        bloom.highQualityFiltering.Override(true);

        runtimeVolume.sharedProfile = profile;
    }

    private static void EnableBaseCameraPostProcessing()
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null) continue;

            UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null && data.renderType == CameraRenderType.Base && !data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
            }
        }
    }
}
