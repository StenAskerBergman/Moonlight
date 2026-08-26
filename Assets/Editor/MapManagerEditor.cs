using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MapManager))]
public sealed class MapManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        
        MapManager mapManager = (MapManager)target;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel, GUILayout.Width(88));
        if (mapManager.LastGenerationTimeMs >= 0)
        {
            GUIStyle blackTimeStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.black },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            if (!string.IsNullOrEmpty(mapManager.LastGenerationBreakStatus))
            {
                GUIStyle alertStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.8f, 0.1f, 0.1f) },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField($"[{mapManager.LastGenerationBreakStatus}]", alertStyle);
            }
            else
            {
                EditorGUILayout.LabelField($"({mapManager.LastGenerationTimeMs} ms)", blackTimeStyle);
            }
        }
        if (mapManager.IsSelectionInverted)
        {
            GUIStyle invertedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(1f, 0.45f, 0.05f) },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("● INVERTED", invertedStyle, GUILayout.Width(82));
        }
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {

            if (GUILayout.Button("Generate Map"))
            {
                RunEditModeAction(mapManager, mapManager.GenerateMap);
            }

            if (GUILayout.Button("Regenerate Map"))
            {
                RunEditModeAction(mapManager, mapManager.RegenerateMap);
            }

            if (GUILayout.Button("Clear Map"))
            {
                RunEditModeAction(mapManager, mapManager.ClearMap);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI Terrain References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Captures indexed orthographic TOP + SIDE composites for every generated island. " +
                "The previous capture is replaced in Temp/TerrainGenerationReferences/Latest.",
                MessageType.Info);

            if (GUILayout.Button("Capture Current Map References"))
            {
                CaptureTerrainReferences(mapManager);
            }

            using (new EditorGUI.DisabledScope(!System.IO.Directory.Exists(
                       TerrainGenerationReferenceCapture.LatestOutputDirectory)))
            {
                if (GUILayout.Button("Open Latest Reference Folder"))
                {
                    EditorUtility.RevealInFinder(TerrainGenerationReferenceCapture.LatestOutputDirectory);
                }
            }

            using (new EditorGUI.DisabledScope(!System.IO.Directory.Exists(
                       TerrainGenerationReferenceCapture.LatestFailedOutputDirectory)))
            {
                if (GUILayout.Button("Open Latest Failed Reference Folder"))
                {
                    EditorUtility.RevealInFinder(TerrainGenerationReferenceCapture.LatestFailedOutputDirectory);
                }
            }
        }
    }

    private static void CaptureTerrainReferences(MapManager mapManager)
    {
        if (!string.IsNullOrEmpty(mapManager.LastGenerationBreakStatus))
        {
            Debug.LogError(
                "Terrain reference capture skipped because the latest map generation did not complete: " +
                mapManager.LastGenerationBreakStatus,
                mapManager);
            return;
        }

        try
        {
            TerrainGenerationReferenceCapture.CaptureLatest(mapManager);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, mapManager);
        }
    }

    private static void RunEditModeAction(MapManager mapManager, System.Action action)
    {
        action();
        EditorUtility.SetDirty(mapManager);
        EditorSceneManager.MarkSceneDirty(mapManager.gameObject.scene);
        SceneView.RepaintAll();
    }
}
