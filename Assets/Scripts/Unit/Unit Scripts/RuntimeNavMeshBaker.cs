using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes the scene's NavMeshSurfaces once the procedurally generated map exists.
///
/// MapManager raises OnMapGenerated at the end of its Start(), after every island
/// has been instantiated, so the geometry is guaranteed to be present by the time
/// this runs. Subscription happens in OnEnable, which precedes any Start() in the
/// scene, so the ordering holds without touching Script Execution Order.
///
/// Assign one NavMeshSurface per agent type (Humanoid for islands, Ship for water).
/// If the Inspector wiring is missing - which a scene re-serialization can silently
/// cause, e.g. an editor upgrade that saves the scene while this script is failing
/// to compile - the surfaces are discovered from the scene instead, so navigation
/// degrades to a warning rather than dying outright.
/// </summary>
public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Tooltip("One NavMeshSurface per agent type. Each bakes its own separate NavMesh. " +
             "Leave empty to bake every NavMeshSurface found in the scene.")]
    [SerializeField] private NavMeshSurface[] surfaces;

    [Tooltip("Bake automatically one frame after the scene starts.")]
    [SerializeField] private bool bakeOnStart = true;

    /// <summary>Raised after every assigned surface has finished baking.</summary>
    public static event System.Action OnNavMeshBaked;

    /// <summary>True once a bake has completed at least once this session.</summary>
    public static bool IsBaked { get; private set; }

    private void OnEnable()
    {
        MapManager.OnMapGenerated += HandleMapGenerated;
    }

    private void OnDisable()
    {
        MapManager.OnMapGenerated -= HandleMapGenerated;
    }

    private void HandleMapGenerated()
    {
        if (bakeOnStart)
        {
            BakeAll();
        }
    }

    /// <summary>
    /// Rebuilds every assigned surface. Safe to call again after the map changes;
    /// BuildNavMesh replaces that surface's data rather than accumulating.
    /// </summary>
    public void BakeAll()
    {
        NavMeshSurface[] targets = ResolveSurfaces();
        if (targets.Length == 0)
        {
            Debug.LogError("RuntimeNavMeshBaker: no NavMeshSurface exists in the scene - nothing to bake.");
            return;
        }

        foreach (NavMeshSurface surface in targets)
        {
            surface.BuildNavMesh();
            Debug.Log($"<color=green>RuntimeNavMeshBaker:</color> baked '{surface.name}' (agentTypeID {surface.agentTypeID}).");
        }

        IsBaked = true;
        OnNavMeshBaked?.Invoke();
    }

    /// <summary>
    /// The assigned surfaces minus any null slots, or - when that leaves nothing -
    /// every active NavMeshSurface in the scene.
    /// </summary>
    private NavMeshSurface[] ResolveSurfaces()
    {
        int assigned = 0;
        if (surfaces != null)
        {
            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface != null) assigned++;
            }
        }

        if (assigned > 0 && assigned == surfaces.Length) return surfaces;

        NavMeshSurface[] found = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        Debug.LogWarning(
            $"RuntimeNavMeshBaker: {(surfaces == null ? 0 : surfaces.Length) - assigned} of " +
            $"{(surfaces == null ? 0 : surfaces.Length)} surface slots are empty - re-assign them on " +
            $"'{name}'. Falling back to the {found.Length} NavMeshSurface(s) found in the scene.");

        // Whether some slots are wired or none are, the scene sweep is a superset of
        // what is assigned, so baking it covers the wired surfaces without baking twice.
        return found;
    }

    private void OnDestroy()
    {
        // Static state must not survive into the next play session / scene load.
        if (Application.isPlaying) IsBaked = false;
    }
}
