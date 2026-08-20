using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes the scene's NavMeshSurfaces once the procedurally generated map exists.
///
/// MapManager instantiates islands inside Start(), and Unity does not define the
/// order of Start() between components. Waiting one frame is order independent:
/// every Start() in the scene has run by the time the first yield resumes, so the
/// island geometry is guaranteed to be present without touching Script Execution
/// Order or coupling this to MapManager.
///
/// Assign one NavMeshSurface per agent type (Humanoid for islands, Ship for water).
/// </summary>
public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Tooltip("One NavMeshSurface per agent type. Each bakes its own separate NavMesh.")]
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
        if (surfaces == null || surfaces.Length == 0)
        {
            Debug.LogError("RuntimeNavMeshBaker: no NavMeshSurfaces assigned - nothing to bake.");
            return;
        }

        int baked = 0;
        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface == null) continue;

            surface.BuildNavMesh();
            baked++;
            Debug.Log($"<color=green>RuntimeNavMeshBaker:</color> baked '{surface.name}' (agentTypeID {surface.agentTypeID}).");
        }

        if (baked == 0)
        {
            Debug.LogError("RuntimeNavMeshBaker: every assigned surface slot was null.");
            return;
        }

        IsBaked = true;
        OnNavMeshBaked?.Invoke();
    }

    private void OnDestroy()
    {
        // Static state must not survive into the next play session / scene load.
        if (Application.isPlaying) IsBaked = false;
    }
}
