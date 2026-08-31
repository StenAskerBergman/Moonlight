using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity does not serialize procedural Cell arrays, terrain providers, or generated
/// texture objects. Rebuild loaded map previews once assemblies settle so recompiling
/// scripts does not leave otherwise valid generated chunks white or incomplete.
/// </summary>
[InitializeOnLoad]
public static class TerrainGenerationDomainReloadRestorer
{
    private const int MaximumRestoreFrames = 30;
    private static int remainingRestoreFrames;

    static TerrainGenerationDomainReloadRestorer()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        ScheduleRestore();
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        ScheduleRestore();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ScheduleRestore();
    }

    private static void ScheduleRestore()
    {
        remainingRestoreFrames = MaximumRestoreFrames;
        EditorApplication.update -= RestoreLoadedMapPreviewsWhenReady;
        EditorApplication.update += RestoreLoadedMapPreviewsWhenReady;
    }

    private static void RestoreLoadedMapPreviewsWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            StopRetrying();
            return;
        }

        bool foundGeneratedPreview = false;
        foreach (MapManager manager in Resources.FindObjectsOfTypeAll<MapManager>())
        {
            if (manager == null
                || !manager.gameObject.scene.IsValid()
                || !manager.gameObject.scene.isLoaded)
            {
                continue;
            }

            Transform generatedRoot = manager.transform.Find("Generated Map");
            if (generatedRoot == null
                || generatedRoot.GetComponentInChildren<MapGrid>(true) == null)
            {
                continue;
            }

            foundGeneratedPreview = true;
            manager.RestoreGeneratedStateAfterDomainReload();
        }

        remainingRestoreFrames--;
        if (foundGeneratedPreview || remainingRestoreFrames <= 0)
        {
            StopRetrying();
        }
    }

    private static void StopRetrying()
    {
        EditorApplication.update -= RestoreLoadedMapPreviewsWhenReady;
    }
}
