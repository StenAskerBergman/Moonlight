using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component attached to an Island that declares native and unlocked crop fertilities.
/// Queried by Farm Cores and Field Placers to validate whether a given crop can grow on this island.
/// </summary>
[DisallowMultipleComponent]
public class IslandFertility : MonoBehaviour
{
    [Header("Native Fertilities")]
    [Tooltip("Fertilities naturally present on this island.")]
    [SerializeField] private List<CropFertilityType> nativeFertilities = new List<CropFertilityType>();

    private readonly HashSet<CropFertilityType> _unlockedFertilities = new HashSet<CropFertilityType>();
    private Island _island;

    public IReadOnlyList<CropFertilityType> NativeFertilities => nativeFertilities;

    private void Awake()
    {
        _island = GetComponent<Island>() ?? GetComponentInParent<Island>();
        foreach (var f in nativeFertilities)
        {
            if (f != CropFertilityType.None)
            {
                _unlockedFertilities.Add(f);
            }
        }
    }

    /// <summary>
    /// Checks if this island supports the given fertility type.
    /// </summary>
    public bool HasFertility(CropFertilityType fertility)
    {
        if (fertility == CropFertilityType.None || fertility == CropFertilityType.Any)
        {
            return true;
        }

        if (_unlockedFertilities.Contains(fertility) || nativeFertilities.Contains(fertility))
        {
            return true;
        }

        // Check active seeds on Island if present
        if (_island != null && _island.ActiveSeeds != null)
        {
            foreach (var seed in _island.ActiveSeeds)
            {
                if (seed != null && MatchesSeed(fertility, seed))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Grants an additional fertility type to this island at runtime (e.g. via seeds, items, or research).
    /// </summary>
    public void AddFertility(CropFertilityType fertility)
    {
        if (fertility == CropFertilityType.None) return;
        if (_unlockedFertilities.Add(fertility))
        {
            if (!nativeFertilities.Contains(fertility))
            {
                nativeFertilities.Add(fertility);
            }
        }
    }

    /// <summary>
    /// Removes a granted fertility type.
    /// </summary>
    public void RemoveFertility(CropFertilityType fertility)
    {
        _unlockedFertilities.Remove(fertility);
        nativeFertilities.Remove(fertility);
    }

    private static bool MatchesSeed(CropFertilityType fertility, ItemData seed)
    {
        if (seed == null) return false;
        string seedName = seed.name ?? seed.displayName ?? "";
        return seedName.IndexOf(fertility.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Static helper to check fertility at an island or from a transform context.
    /// </summary>
    public static bool CheckFertility(Transform contextTransform, CropFertilityType requiredFertility)
    {
        if (requiredFertility == CropFertilityType.None || requiredFertility == CropFertilityType.Any)
        {
            return true;
        }

        if (contextTransform == null) return true;

        IslandFertility islandFertility = contextTransform.GetComponentInParent<IslandFertility>();
        if (islandFertility != null)
        {
            return islandFertility.HasFertility(requiredFertility);
        }

        Island island = contextTransform.GetComponentInParent<Island>();
        if (island != null)
        {
            islandFertility = island.GetComponent<IslandFertility>();
            if (islandFertility != null)
            {
                return islandFertility.HasFertility(requiredFertility);
            }
        }

        // Default to true if no fertility system is explicitly attached to the island
        return true;
    }
}
