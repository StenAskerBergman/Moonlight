using UnityEngine;

/// <summary>
/// Internal authority for unit creation. 
/// Called exclusively by UnitService when gameplay requests a unit.
/// </summary>
internal static class UnitFactory
{
    public static Unit Create(UnitDefinition def, Vector3 position, Quaternion rotation)
    {
        if (def == null || def.prefab == null)
        {
            Debug.LogError("UnitFactory: Cannot create unit from null definition or missing prefab.");
            return null;
        }

        // Instantiate inactive to prevent Awake/Start from running before we configure it
        GameObject go = Object.Instantiate(def.prefab, position, rotation);
        go.SetActive(false);

        Unit unit = go.GetComponent<Unit>();
        if (unit != null)
        {
            unit.type = def.unitType;
            if (def.movementProfile != null)
            {
                unit.moveType = def.movementProfile.Domain;
            }
            unit.SetDisplayName(NameGenerator.RandomNameSelector(def.nameType));
        }
        else
        {
            Debug.LogError($"UnitFactory: Prefab '{def.prefab.name}' lacks a Unit component.");
        }

        // Apply movement configuration (agentTypeID, areaMask, TravelMedium, etc.)
        if (def.movementProfile != null)
        {
            def.movementProfile.Apply(go);
        }

        // Configuration complete, wake the unit up
        go.SetActive(true);
        
        return unit;
    }
}
