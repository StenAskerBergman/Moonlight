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
        EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            MapManager mapManager = (MapManager)target;

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
