using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps <see cref="BuildingData"/> namespaced identifiers (e.g. "core:worker_resident")
/// to their corresponding building prefabs. Stamps store identifiers rather than
/// direct prefab references, so this registry is needed to resolve them back to
/// prefabs at placement time.
///
/// Auto-populates at startup by scanning all <see cref="BuildingButton"/> components
/// in the scene and any manually registered entries.
/// </summary>
public class BuildingPrefabRegistry : MonoBehaviour
{
    public static BuildingPrefabRegistry Instance { get; private set; }

    [Header("Manual Overrides")]
    [Tooltip("Drag prefabs here if they aren't reachable via BuildingButton UI.")]
    [SerializeField] private List<GameObject> additionalPrefabs = new List<GameObject>();

    private Dictionary<string, GameObject> _lookup = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeFromScene();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── Public API ─────────

    /// <summary>
    /// Returns the building prefab for the given namespaced identifier,
    /// or null if not found.
    /// </summary>
    public GameObject GetPrefab(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;
        _lookup.TryGetValue(identifier, out GameObject prefab);
        return prefab;
    }

    /// <summary>
    /// Manually registers a prefab against its <see cref="BuildingData.Id"/>.
    /// </summary>
    public void RegisterPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        BuildingProperties props = prefab.GetComponent<BuildingProperties>();
        if (props == null) return;

        string id = props.buildingData != null ? props.buildingData.Id.ToString() : null;

        // Not every building prefab carries a BuildingData asset yet - the Depot does
        // not. Those used to be dropped here without a word, which left the manual
        // override list unable to publish the very prefabs it exists for. Fall back to a
        // stable name-derived identifier so they can still be resolved.
        if (string.IsNullOrEmpty(id))
        {
            string source = !string.IsNullOrEmpty(props.buildingName) ? props.buildingName : prefab.name;
            id = $"moonlight:{source.ToLowerInvariant().Replace(' ', '_')}";
        }

        _lookup[id] = prefab;
    }

    /// <summary>Returns true if the registry contains the given identifier.</summary>
    public bool Contains(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) && _lookup.ContainsKey(identifier);
    }

    /// <summary>Returns all registered identifiers.</summary>
    public IEnumerable<string> AllIdentifiers => _lookup.Keys;

    // ───────── Initialisation ─────────

    /// <summary>
    /// Scans all <see cref="BuildingButton"/> components in the scene (the UI
    /// already holds a reference to each building prefab) and registers them.
    /// Also registers any manually assigned <see cref="additionalPrefabs"/>.
    /// </summary>
    public void InitializeFromScene()
    {
        _lookup.Clear();

        // 1. Scan BuildingButton UI components
        BuildingButton[] buttons = FindObjectsOfType<BuildingButton>(includeInactive: true);
        foreach (BuildingButton btn in buttons)
        {
            GameObject prefab = btn.GetBuildingPrefab();
            if (prefab != null)
            {
                RegisterPrefab(prefab);
            }
        }

        // 2. Register additional manual overrides
        foreach (GameObject prefab in additionalPrefabs)
        {
            RegisterPrefab(prefab);
        }

        Debug.Log($"[BuildingPrefabRegistry] Registered {_lookup.Count} building prefab(s).");
    }
}
