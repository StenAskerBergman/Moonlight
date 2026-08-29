#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor migration and audit tool for namespaced Identifiers on BuildingData, ItemData, and UnitDefinition assets.
/// Detects Missing IDs, Duplicate IDs, Unmigrated Placeholders, and Invalid Namespaces.
/// Guarantees 100% deterministic uniqueness across all suggested IDs before writing to disk.
/// Derives suggested IDs strictly from unique asset file names.
/// </summary>
public class DataIdMigrationValidator : EditorWindow
{
    private Vector2 _scrollPos;
    private List<AssetIdAuditEntry> _entries = new List<AssetIdAuditEntry>();
    private bool _hasScanned = false;
    private int _issueCount = 0;

    public enum IdStatus
    {
        Valid,
        MissingOrEmpty,
        UnmigratedPlaceholder,
        Duplicate,
        InvalidFormat
    }

    public class AssetIdAuditEntry
    {
        public ScriptableObject Asset;
        public string AssetPath;
        public string TypeName;
        public string CurrentId;
        public string SuggestedId;
        public IdStatus Status;
        public string StatusDetail;
    }

    [MenuItem("Moonlight/Audit and Migrate Data IDs")]
    public static void ShowWindow()
    {
        var window = GetWindow<DataIdMigrationValidator>("Data ID Migration & Audit");
        window.minSize = new Vector2(780, 480);
        window.RunAudit();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Data ID Migration & Audit Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Audits all BuildingData, ItemData, and UnitDefinition ScriptableObjects. Suggested IDs are derived from unique asset file names and deterministically resolved to guarantee zero collisions.", MessageType.Info);

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan / Re-Audit Assets", GUILayout.Height(30)))
        {
            RunAudit();
        }

        GUI.enabled = _issueCount > 0;
        if (GUILayout.Button($"Auto-Migrate Problematic IDs ({_issueCount})", GUILayout.Height(30)))
        {
            AutoMigrateProblematicIds();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        if (!_hasScanned) return;

        if (_issueCount == 0)
        {
            EditorGUILayout.HelpBox("All data assets have valid, unique namespaced Identifiers!", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"Found {_issueCount} asset(s) with ID issues (Missing, Placeholder, Duplicate, or Invalid). Click 'Auto-Migrate' to assign unique collision-free IDs.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Asset Name", EditorStyles.boldLabel, GUILayout.Width(220));
        EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Current ID", EditorStyles.boldLabel, GUILayout.Width(180));
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Deterministic Suggested ID", EditorStyles.boldLabel, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        foreach (var entry in _entries)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(entry.Asset.name, EditorStyles.linkLabel, GUILayout.Width(220)))
            {
                Selection.activeObject = entry.Asset;
                EditorGUIUtility.PingObject(entry.Asset);
            }

            EditorGUILayout.LabelField(entry.TypeName, GUILayout.Width(100));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(entry.CurrentId) ? "<empty>" : entry.CurrentId, GUILayout.Width(180));

            DrawStatusBadge(entry.Status);

            EditorGUILayout.LabelField(entry.SuggestedId, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawStatusBadge(IdStatus status)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label);
        switch (status)
        {
            case IdStatus.Valid:
                style.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                EditorGUILayout.LabelField("Valid", style, GUILayout.Width(100));
                break;
            case IdStatus.MissingOrEmpty:
                style.normal.textColor = Color.red;
                EditorGUILayout.LabelField("Missing", style, GUILayout.Width(100));
                break;
            case IdStatus.UnmigratedPlaceholder:
                style.normal.textColor = new Color(0.9f, 0.5f, 0.1f);
                EditorGUILayout.LabelField("Placeholder", style, GUILayout.Width(100));
                break;
            case IdStatus.Duplicate:
                style.normal.textColor = Color.magenta;
                EditorGUILayout.LabelField("Duplicate", style, GUILayout.Width(100));
                break;
            case IdStatus.InvalidFormat:
                style.normal.textColor = Color.yellow;
                EditorGUILayout.LabelField("Invalid Format", style, GUILayout.Width(100));
                break;
        }
    }

    public void RunAudit()
    {
        _entries.Clear();
        _issueCount = 0;

        // 1. Collect all assets and audit their current status
        AuditAssetType<BuildingData>("t:BuildingData", "BuildingData", "core");
        AuditAssetType<ItemData>("t:ItemData", "ItemData", "core");
        AuditAssetType<UnitDefinition>("t:UnitDefinition", "UnitDefinition", "core");

        // Deterministically sort entries by AssetPath (ordinal) so collision resolution order is 100% reproducible
        _entries.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));

        // 2. Identify duplicate IDs in currently valid entries
        Dictionary<string, List<AssetIdAuditEntry>> idToEntries = new Dictionary<string, List<AssetIdAuditEntry>>();
        foreach (var entry in _entries)
        {
            if (!string.IsNullOrEmpty(entry.CurrentId))
            {
                if (!idToEntries.ContainsKey(entry.CurrentId))
                {
                    idToEntries[entry.CurrentId] = new List<AssetIdAuditEntry>();
                }
                idToEntries[entry.CurrentId].Add(entry);
            }
        }

        foreach (var kvp in idToEntries)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var duplicateEntry in kvp.Value)
                {
                    duplicateEntry.Status = IdStatus.Duplicate;
                    duplicateEntry.StatusDetail = $"Duplicate ID shared by {kvp.Value.Count} assets";
                }
            }
        }

        // 3. Deterministically compute unique suggested IDs across the entire project
        HashSet<string> reservedIds = new HashSet<string>();

        // First reserve all IDs that are already completely valid and unique
        foreach (var entry in _entries)
        {
            if (entry.Status == IdStatus.Valid && !string.IsNullOrEmpty(entry.CurrentId))
            {
                reservedIds.Add(entry.CurrentId);
                entry.SuggestedId = entry.CurrentId;
            }
        }

        // Now resolve unique suggested IDs for problematic entries
        _issueCount = 0;
        foreach (var entry in _entries)
        {
            if (entry.Status != IdStatus.Valid)
            {
                _issueCount++;

                // Derive base slug strictly from asset file name
                string baseSlug = Slugify(entry.Asset.name);
                string candidateId = $"core:{baseSlug}";

                // Deterministically resolve collisions by incrementing counter
                int counter = 2;
                while (reservedIds.Contains(candidateId))
                {
                    candidateId = $"core:{baseSlug}_{counter}";
                    counter++;
                }

                reservedIds.Add(candidateId);
                entry.SuggestedId = candidateId;
            }
        }

        _hasScanned = true;
        Repaint();
    }

    private void AuditAssetType<T>(string searchFilter, string typeName, string defaultNamespace) where T : ScriptableObject, IIdentifiable
    {
        string[] guids = AssetDatabase.FindAssets(searchFilter);
        System.Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty idProp = so.FindProperty("identifier");
            string currentId = idProp != null ? idProp.stringValue : "";

            IdStatus status = IdStatus.Valid;
            string detail = "";

            if (string.IsNullOrWhiteSpace(currentId) || currentId == "core:empty")
            {
                status = IdStatus.MissingOrEmpty;
                detail = "ID is missing or empty";
            }
            else if (currentId == "core:building" || currentId == "core:item" || currentId == "core:unit")
            {
                status = IdStatus.UnmigratedPlaceholder;
                detail = "Unmigrated default placeholder";
            }
            else if (!IsValidNamespacedFormat(currentId))
            {
                status = IdStatus.InvalidFormat;
                detail = "Invalid format: must be lowercase 'namespace:path' without special characters";
            }

            _entries.Add(new AssetIdAuditEntry
            {
                Asset = asset,
                AssetPath = path,
                TypeName = typeName,
                CurrentId = currentId,
                SuggestedId = "",
                Status = status,
                StatusDetail = detail
            });
        }
    }

    private void AutoMigrateProblematicIds()
    {
        // Re-run audit first to guarantee collision-free suggested IDs
        RunAudit();

        // Safety assertion: Ensure all suggested IDs in the entire batch are 100% unique
        HashSet<string> verifyUniqueness = new HashSet<string>();
        foreach (var entry in _entries)
        {
            string finalId = (entry.Status == IdStatus.Valid) ? entry.CurrentId : entry.SuggestedId;
            if (!verifyUniqueness.Add(finalId))
            {
                Debug.LogError($"[DataIdMigration] Aborting migration: Collision detected for ID '{finalId}'. No assets were modified.");
                return;
            }
        }

        int migrated = 0;
        foreach (var entry in _entries)
        {
            if (entry.Status != IdStatus.Valid && entry.Asset != null)
            {
                SerializedObject so = new SerializedObject(entry.Asset);
                SerializedProperty idProp = so.FindProperty("identifier");
                if (idProp != null)
                {
                    idProp.stringValue = entry.SuggestedId;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(entry.Asset);
                    migrated++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=cyan>[DataIdMigration]</color> Successfully migrated {migrated} asset IDs with guaranteed uniqueness.");
        RunAudit();
    }

    private static bool IsValidNamespacedFormat(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        // Format: namespace:path where namespace is [a-z0-9_] and path is [a-z0-9_/]
        return Regex.IsMatch(id, @"^[a-z0-9_]+:[a-z0-9_/\-]+$");
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "unnamed";
        string slug = input.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9_]+", "_");
        slug = slug.Trim('_');
        return string.IsNullOrEmpty(slug) ? "unnamed" : slug;
    }
}
#endif
