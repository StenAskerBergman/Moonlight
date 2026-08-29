using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGrid))]
public class MapGridEditor : Editor
{
    private Editor climateEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGrid mapGrid = (MapGrid)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Terrain Diagnostics & Heatmap", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        TerrainDebugViewMode newMode = (TerrainDebugViewMode)EditorGUILayout.EnumPopup("Debug View Mode", mapGrid.debugViewMode);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mapGrid, "Change Terrain Debug View Mode");
            mapGrid.debugViewMode = newMode;
            EditorUtility.SetDirty(mapGrid);
            mapGrid.UpdateTerrainTexture();
        }

        if (GUILayout.Button("Update Terrain Texture"))
        {
            mapGrid.UpdateTerrainTexture();
        }

        if (mapGrid.climateProfile != null)
        {
            GUILayout.Space(15);
            EditorGUILayout.LabelField("Inline Climate Profile Editor", EditorStyles.boldLabel);
            
            // Draw the ScriptableObject inline
            CreateCachedEditor(mapGrid.climateProfile, null, ref climateEditor);
            
            EditorGUI.BeginChangeCheck();
            climateEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(mapGrid.climateProfile);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Climate Profile to edit its values inline.", MessageType.Info);
        }
    }
}
