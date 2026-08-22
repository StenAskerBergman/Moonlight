using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps a movement domain (MoveType) to its concrete UnitDefinition.
/// Used by gameplay systems to request units by category rather than holding
/// direct prefab references.
/// </summary>
[CreateAssetMenu(fileName = "Unit Catalogue", menuName = "Data/Unit/Unit Catalogue")]
public class UnitCatalogue : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public MoveType domain;
        public UnitDefinition definition;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public UnitDefinition Resolve(MoveType domain)
    {
        foreach (var entry in entries)
        {
            if (entry.domain == domain)
            {
                return entry.definition;
            }
        }
        
        Debug.LogError($"UnitCatalogue: No definition found for domain {domain}");
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var seen = new HashSet<MoveType>();
        foreach (var entry in entries)
        {
            if (entry.definition == null)
            {
                Debug.LogWarning($"UnitCatalogue '{name}': contains an entry with a null UnitDefinition.");
            }
            else if (!seen.Add(entry.domain))
            {
                Debug.LogError($"UnitCatalogue '{name}': Duplicate entry for domain {entry.domain}. Each domain can only be mapped once in this slice.");
            }
        }
    }
#endif
}
