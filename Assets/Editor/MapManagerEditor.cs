using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(MapManager))]
public sealed class MapManagerEditor : Editor
{
    private static bool showPatterns = true;
    private static bool showMapSettings = true;
    private static bool showReviewTools;
    private static bool showTerrainReferenceTools;

    public override void OnInspectorGUI()
    {
        MapManager mapManager = (MapManager)target;

        serializedObject.Update();

        DrawRunPanel(mapManager);

        EditorGUILayout.Space(8f);
        DrawPatternsSection(mapManager);

        EditorGUILayout.Space(8f);
        DrawMapSceneSettings();

        EditorGUILayout.Space(8f);
        DrawReviewTools(mapManager);

        EditorGUILayout.Space(8f);
        DrawTerrainReferenceTools(mapManager);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRunPanel(MapManager mapManager)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Map Run Controls", EditorStyles.boldLabel);

            DrawPatternSelector(mapManager);

            DrawGenerationStatus(mapManager);

            EditorGUILayout.Space(3f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
            {
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                if (GUILayout.Button("Generate", GUILayout.Height(28f)))
                {
                    RunEditModeAction(mapManager, mapManager.GenerateMap);
                }

                GUI.backgroundColor = previousBackground;
                if (GUILayout.Button("Regenerate", GUILayout.Height(28f)))
                {
                    RunEditModeAction(mapManager, mapManager.RegenerateMap);
                }

                GUI.backgroundColor = new Color(1f, 0.72f, 0.72f);
                if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(64f)))
                {
                    RunEditModeAction(mapManager, mapManager.ClearMap);
                }

                GUI.backgroundColor = previousBackground;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.LabelField(
                    "Map preview controls are available in Edit Mode.",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawPatternSelector(MapManager mapManager)
    {
        List<MapManager.PatternData> patternData = mapManager.patternDataList;
        if (patternData == null || patternData.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Add entries to Pattern Data List below to configure map spawn patterns.",
                MessageType.Info);
            return;
        }

        string[] labels = new string[patternData.Count];
        for (int i = 0; i < patternData.Count; i++)
        {
            MapManager.PatternData data = patternData[i];
            labels[i] = data == null
                ? $"Missing Data ({i})"
                : string.IsNullOrWhiteSpace(data.displayName)
                    ? data.spawnPattern.ToString()
                    : data.displayName;
        }

        int selectedIndex = mapManager.SelectedPatternDataIndex;
        if (selectedIndex < 0 || selectedIndex >= patternData.Count)
        {
            selectedIndex = Mathf.Max(0, patternData.FindIndex(data => data != null));
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Active Spawn Pattern", selectedIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mapManager, "Select Spawn Pattern");
            mapManager.SelectPatternData(newIndex);
            EditorUtility.SetDirty(mapManager);
        }

        MapManager.PatternData selected = mapManager.SelectedPatternData;
        if (selected != null)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int gridSlots = selected.gridSize;
                float spacing = selected.slotSpacing;
                int islandsCount = mapManager.ActiveIslandSelection.Count;
                int underwaterCount = mapManager.ActiveOceanSelection.Count;

                DrawSummaryRow("Layout", selected.spawnPattern.ToString());
                DrawSummaryRow("Grid Size", $"{gridSlots} x {gridSlots} ({gridSlots * gridSlots} slots)");
                DrawSummaryRow("Slot Spacing", $"{spacing:F0} units");
                DrawSummaryRow("Inverted Selection", selected.invertSelection ? "Yes" : "No");

                if (islandsCount > 0 || underwaterCount > 0)
                {
                    DrawSummaryRow(
                        "Selection Masks",
                        $"Islands: {islandsCount}, {selected.underwaterSelectionTerrainType}: {underwaterCount}");
                }

                if (selected.configuration != null)
                {
                    DrawSummaryRow("Configuration", selected.configuration.name);
                }

                DrawSummaryRow("Default Terrain", selected.defaultTerrainType.ToString());
                DrawSummaryRow("Default Prefab", selected.defaultChunkPrefab != null
                    ? selected.defaultChunkPrefab.name
                    : "MISSING");
                DrawSummaryRow("Slot Overrides", (selected.slotOverrides?.Count ?? 0).ToString());

                if (HasUnreachableRules(selected, out int firstUnreachableRule))
                {
                    EditorGUILayout.HelpBox(
                        $"Rule {firstUnreachableRule + 1} and later rules are unreachable because an earlier All rule always matches.",
                        MessageType.Warning);
                }

                if (!string.IsNullOrWhiteSpace(selected.description))
                {
                    EditorGUILayout.HelpBox(selected.description, MessageType.None);
                }
            }
        }
    }

    private void DrawPatternsSection(MapManager mapManager)
    {
        showPatterns = EditorGUILayout.Foldout(
            showPatterns,
            "Spawn Patterns (Pattern Data List)",
            true);
        if (!showPatterns)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "This list is the complete map-generation authority. Each entry owns layout, grid size, spacing, masks, default terrain, chunk prefab, configuration, water, and ordered slot overrides.",
                MessageType.None);

            SerializedProperty patternListProp = serializedObject.FindProperty("patternDataList");
            EditorGUILayout.PropertyField(patternListProp, true);

        }
    }

    private void DrawMapSceneSettings()
    {
        showMapSettings = EditorGUILayout.Foldout(
            showMapSettings,
            "Map Runtime Wiring",
            true);
        if (!showMapSettings)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "patternDataList",
                "legacySelectedSpawnPattern",
                "selectedPatternDataIndex");
        }
    }

    private static void DrawSummaryRow(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(value, EditorStyles.miniBoldLabel, GUILayout.MaxWidth(180f));
        }
    }

    private static bool HasUnreachableRules(MapManager.PatternData pattern, out int firstUnreachableRule)
    {
        firstUnreachableRule = -1;
        if (pattern.slotOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < pattern.slotOverrides.Count - 1; i++)
        {
            MapManager.SpawnRule rule = pattern.slotOverrides[i];
            if (rule != null
                && rule.condition == MapManager.SpawnCondition.All
                && !rule.invertCondition)
            {
                firstUnreachableRule = i + 1;
                return true;
            }
        }

        return false;
    }

    private static void DrawGenerationStatus(MapManager mapManager)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Last Preview", EditorStyles.miniBoldLabel, GUILayout.Width(78));
        if (mapManager.LastGenerationTimeMs >= 0)
        {
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
                EditorGUILayout.LabelField(
                    $"Ready  •  {mapManager.LastGenerationTimeMs} ms",
                    EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Not generated yet", EditorStyles.miniLabel);
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
    }

    private static void DrawReviewTools(MapManager mapManager)
    {
        showReviewTools = EditorGUILayout.Foldout(showReviewTools, "Review & Chat Captures", true);
        if (!showReviewTools)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "Component capture isolates Map Manager for focused review. Inspector capture includes the complete docked Inspector window.",
                MessageType.Info);

            if (GUILayout.Button("Open Map Manager Component Capture"))
            {
                ComponentCaptureWindow.Open(mapManager);
            }

            if (GUILayout.Button("Capture Full Inspector Now"))
            {
                EditorWindowCapture.CaptureInspector();
            }

            using (new EditorGUI.DisabledScope(!System.IO.Directory.Exists(EditorWindowCapture.OutputDirectory)))
            {
                if (GUILayout.Button("Open Capture Folder"))
                {
                    EditorUtility.RevealInFinder(EditorWindowCapture.OutputDirectory);
                }
            }
        }
    }

    private static void DrawTerrainReferenceTools(MapManager mapManager)
    {
        showTerrainReferenceTools = EditorGUILayout.Foldout(
            showTerrainReferenceTools,
            "AI Terrain Reference Tools",
            true);
        if (!showTerrainReferenceTools)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
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
