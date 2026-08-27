#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pre-flight asset audit tool for building deliverables (3D Models, Animators, VFX, Audio, and UI Badges).
/// Accessible via Unity Editor menu: Moonlight > Validate Building Deliverables.
/// </summary>
public class BuildingAssetValidator : EditorWindow
{
    private Vector2 _scrollPos;
    private List<BuildingAuditReport> _reports = new List<BuildingAuditReport>();
    private bool _hasAudited = false;

    private struct BuildingAuditReport
    {
        public string PrefabName;
        public GameObject PrefabObject;
        public bool HasCustomMesh;
        public bool HasAnimator;
        public bool HasSmokeVfx;
        public bool HasAudioLoop;
        public bool HasStatusBadges;
    }

    [MenuItem("Moonlight/Validate Building Deliverables")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildingAssetValidator>("Building Deliverables Validator");
        window.minSize = new Vector2(650, 400);
        window.RunAudit();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Building Deliverables & Asset Readiness Audit", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Audits building prefabs for assigned art, animators, VFX, audio, and UI deliverables. Missing deliverables fall back safely at runtime without crashing the game loop.", MessageType.Info);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Run / Refresh Asset Audit", GUILayout.Height(30)))
        {
            RunAudit();
        }

        EditorGUILayout.Space(10);

        if (!_hasAudited) return;

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel, GUILayout.Width(180));
        EditorGUILayout.LabelField("3D Mesh", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField("Smoke VFX", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField("Audio Loop", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField("Status Badges", EditorStyles.boldLabel, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        foreach (var report in _reports)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(report.PrefabName, EditorStyles.linkLabel, GUILayout.Width(180)))
            {
                Selection.activeObject = report.PrefabObject;
                EditorGUIUtility.PingObject(report.PrefabObject);
            }

            DrawStatusLabel(report.HasCustomMesh ? "Custom" : "Primitive", report.HasCustomMesh);
            DrawStatusLabel(report.HasAnimator ? "Ready" : "Missing", report.HasAnimator);
            DrawStatusLabel(report.HasSmokeVfx ? "Ready" : "Missing", report.HasSmokeVfx);
            DrawStatusLabel(report.HasAudioLoop ? "Ready" : "Missing", report.HasAudioLoop);
            DrawStatusLabel(report.HasStatusBadges ? "Ready" : "Missing", report.HasStatusBadges);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawStatusLabel(string text, bool isReady)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = isReady ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.4f, 0.2f);
        EditorGUILayout.LabelField(text, style, GUILayout.Width(80));
    }

    public void RunAudit()
    {
        _reports.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Building Prefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // Only audit building prefabs (with Building, BuildingProperties, or BuildingSimulation)
            if (prefab.GetComponent<Building>() == null &&
                prefab.GetComponent<BuildingProperties>() == null &&
                prefab.GetComponent<BuildingSimulation>() == null)
            {
                continue;
            }

            BuildingAuditReport report = new BuildingAuditReport
            {
                PrefabName = prefab.name,
                PrefabObject = prefab
            };

            // Check 3D Mesh (Does it use custom mesh or primitive cube?)
            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool hasCustom = false;
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null && !mf.sharedMesh.name.StartsWith("Cube") && !mf.sharedMesh.name.StartsWith("Default"))
                {
                    hasCustom = true;
                    break;
                }
            }
            report.HasCustomMesh = hasCustom;

            // Check Animator
            report.HasAnimator = prefab.GetComponentInChildren<Animator>(true) != null;

            // Check Presentation component / VFX & Audio
            BuildingPresentation pres = prefab.GetComponent<BuildingPresentation>();
            AudioSource audioSource = prefab.GetComponentInChildren<AudioSource>(true);
            ParticleSystem particle = prefab.GetComponentInChildren<ParticleSystem>(true);

            report.HasSmokeVfx = particle != null;
            report.HasAudioLoop = audioSource != null && audioSource.clip != null;

            // Check Status Badges
            report.HasStatusBadges = prefab.GetComponentInChildren<BuildingStatusBadge>(true) != null;

            _reports.Add(report);
        }

        _hasAudited = true;
        Repaint();
    }
}
#endif
