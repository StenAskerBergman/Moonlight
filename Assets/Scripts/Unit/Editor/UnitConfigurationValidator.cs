#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public static class UnitConfigurationValidator
{
    [MenuItem("Tools/Validate Unit Configuration")]
    public static void ValidateAll()
    {
        int errors = 0;
        int warnings = 0;

        Debug.Log("<b>--- Starting Unit Configuration Validation ---</b>");

        // 1. Validate prefabs in the project
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Units Prefabs" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab.GetComponent<Unit>() != null)
            {
                ValidateUnitObject(prefab, true, ref errors, ref warnings);
            }
        }

        // 2. Validate units currently in the open scene
        Unit[] sceneUnits = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in sceneUnits)
        {
            ValidateUnitObject(unit.gameObject, false, ref errors, ref warnings);
        }

        Debug.Log($"<b>--- Validation Complete: {errors} Errors, {warnings} Warnings ---</b>");
    }

    private static void ValidateUnitObject(GameObject go, bool isPrefab, ref int errors, ref int warnings)
    {
        string context = isPrefab ? $"Prefab '{go.name}'" : $"Scene Unit '{go.name}'";
        
        Unit unit = go.GetComponent<Unit>();
        UnitMovement movement = go.GetComponent<UnitMovement>();
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();

        if (movement == null)
        {
            Debug.LogWarning($"{context}: Missing UnitMovement component.");
            warnings++;
            return;
        }

        // Check Layer 9 (Clickable)
        if (go.layer != 9)
        {
            Debug.LogError($"{context}: Root layer is {go.layer} instead of 9 (Clickable). Unit cannot be selected.");
            errors++;
        }

        // Check Agent vs Domain consistency if it has an agent
        if (agent != null)
        {
            // If it's a boat/watercraft, it should be Ship (-1372625422)
            if (unit.moveType == MoveType.Watercraft && agent.agentTypeID != -1372625422)
            {
                Debug.LogError($"{context}: is Watercraft but has agentTypeID {agent.agentTypeID} (Expected -1372625422 for Ship).");
                errors++;
            }
            
            // TravelMedium should not be 0
            if (movement.TravelMedium.value == 0)
            {
                Debug.LogError($"{context}: TravelMedium is Nothing (0). Move orders will be silently dropped.");
                errors++;
            }
        }
        else if (unit.moveType != MoveType.Aircraft)
        {
            Debug.LogError($"{context}: Missing NavMeshAgent, but moveType is {unit.moveType}.");
            errors++;
        }

        if (!isPrefab)
        {
            Debug.LogWarning($"{context}: Hand-placed in scene. UnitFactory cannot manage this object.");
            warnings++;
        }
    }
}
#endif
