using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// See WaterNavMeshCarver: the scene uses the package NavMeshSurface, and the
// vendored copy in Assets/NavMeshComponents makes the bare name ambiguous.
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;

/// <summary>
/// The map has four independent navigable worlds stacked on top of each other, and
/// they do not all become bakeable at the same moment. This splits the bake into
/// ordered phases so each one can be rebuilt on its own trigger:
///
///   Terrain      - land, mountains, rivers, lakes.       Needs island meshes.
///   SurfaceWater - sea/ocean at the waterline.           Needs the islands carved out.
///   SubSurface   - submarine surface + deep-sea layers.  Needs the seabed.
///   Airspace     - low/mid/high altitude bands.          Needs mountains to block Low.
///
/// A surface's phase is derived from its agent type, so the flat list stays valid
/// and adding a Submarine or Aircraft surface is enough to bring that phase to life.
///
/// MapManager instantiates islands inside Start(), and Unity does not define the
/// order of Start() between components. Waiting one frame is order independent:
/// every Start() in the scene has run by the time the first yield resumes, so the
/// island geometry is guaranteed to be present without touching Script Execution
/// Order or coupling this to MapManager.
/// </summary>
public class RuntimeNavMeshBaker : MonoBehaviour
{
    public enum BakePhase
    {
        Terrain,
        SurfaceWater,
        SubSurface,
        Airspace,
    }

    /// <summary>Phases in the order they must be baked.</summary>
    private static readonly BakePhase[] PhaseOrder =
    {
        BakePhase.Terrain,
        BakePhase.SurfaceWater,
        BakePhase.SubSurface,
        BakePhase.Airspace,
    };

    /// <summary>
    /// Agent type name (as configured in Navigation &gt; Agents) to the phase that
    /// bakes it. Anything unrecognised falls back to Terrain with a warning.
    /// </summary>
    private static readonly Dictionary<string, BakePhase> PhaseByAgentType =
        new Dictionary<string, BakePhase>
        {
            { "Humanoid",  BakePhase.Terrain      },
            { "Ship",      BakePhase.SurfaceWater },
            { "Submarine", BakePhase.SubSurface   },
            { "Aircraft",  BakePhase.Airspace     },
        };

    [Tooltip("Optional override. Leave empty to bake every NavMeshSurface in the scene, " +
             "which is usually what you want - the bake phase is derived from each " +
             "surface's agent type, so a new Submarine or Aircraft surface is picked up " +
             "with no rewiring.")]
    [SerializeField] private NavMeshSurface[] surfaces;

    [Tooltip("Carves the islands out of the water NavMeshes. Runs immediately before " +
             "the water phases. Found or created automatically if left empty.")]
    [SerializeField] private WaterNavMeshCarver waterCarver;

    [Tooltip("Builds the submarine deep layer and the aircraft altitude bands before " +
             "their phases bake. Found or created automatically if left empty.")]
    [SerializeField] private StackedNavMeshLayers stackedLayers;

    [Tooltip("Builds the dive and lift-off NavMeshLinks after their phases bake. " +
             "Found or created automatically if left empty.")]
    [SerializeField] private NavTransitionLinks transitionLinks;

    [Tooltip("Bake automatically one frame after the scene starts.")]
    [SerializeField] private bool bakeOnStart = true;

    /// <summary>Raised after each phase finishes, in bake order.</summary>
    public static event Action<BakePhase> OnPhaseBaked;

    /// <summary>Raised after every phase has finished baking.</summary>
    public static event Action OnNavMeshBaked;

    /// <summary>True once a full bake has completed at least once this session.</summary>
    public static bool IsBaked { get; private set; }

    // The water phases share one set of carve volumes, so a full bake must not
    // rebuild them twice. Cleared once they are current, raised again whenever the
    // map changes underneath us.
    private bool carveDirty = true;

    /// <summary>
    /// Marks the island footprint as changed, so the next water phase recarves.
    /// Call after terraforming, or after an island is created or destroyed.
    /// </summary>
    public void InvalidateCarve()
    {
        carveDirty = true;
    }

    private IEnumerator Start()
    {
        if (!bakeOnStart) yield break;

        // Let every other Start() run first so generated islands exist.
        yield return null;

        BakeAll();
    }

    /// <summary>
    /// Rebuilds every phase in order. Safe to call again after the map changes;
    /// BuildNavMesh replaces a surface's data rather than accumulating.
    /// </summary>
    public void BakeAll()
    {
        carveDirty = true;

        int baked = 0;
        foreach (BakePhase phase in PhaseOrder)
        {
            baked += Bake(phase);
        }

        if (baked == 0)
        {
            Debug.LogError("RuntimeNavMeshBaker: found no NavMeshSurface to bake.");
            return;
        }

        IsBaked = true;
        OnNavMeshBaked?.Invoke();
    }

    /// <summary>
    /// Rebuilds a single phase, running that phase's prerequisites first. Use this
    /// for targeted rebakes - a terraform only needs Terrain and SurfaceWater, not
    /// the whole map. Returns how many surfaces were rebuilt.
    /// </summary>
    public int Bake(BakePhase phase)
    {
        RunPrerequisites(phase);

        int baked = 0;
        foreach (NavMeshSurface surface in SurfacesFor(phase))
        {
            surface.BuildNavMesh();
            baked++;
            Debug.Log($"<color=green>RuntimeNavMeshBaker:</color> baked '{surface.name}' " +
                      $"(phase {phase}, agentTypeID {surface.agentTypeID}).");
        }

        if (baked > 0)
        {
            RunPostBake(phase);
            OnPhaseBaked?.Invoke(phase);
        }

        return baked;
    }

    /// <summary>
    /// Non-blocking version of <see cref="Bake"/> for rebakes during play, so a
    /// terraform or a destroyed island does not stall the frame.
    /// </summary>
    public IEnumerator BakeAsync(BakePhase phase)
    {
        RunPrerequisites(phase);

        bool any = false;
        foreach (NavMeshSurface surface in SurfacesFor(phase))
        {
            any = true;
            yield return surface.UpdateNavMesh(surface.navMeshData);
        }

        if (any)
        {
            RunPostBake(phase);
            OnPhaseBaked?.Invoke(phase);
        }
    }

    /// <summary>Non-blocking full rebake, phase by phase.</summary>
    public IEnumerator BakeAllAsync()
    {
        carveDirty = true;

        foreach (BakePhase phase in PhaseOrder)
        {
            yield return BakeAsync(phase);
        }

        IsBaked = true;
        OnNavMeshBaked?.Invoke();
    }

    /// <summary>
    /// Work a phase needs done before its surfaces are collected. Kept here rather
    /// than in the callers so every bake path gets it, sync and async alike.
    /// </summary>
    private void RunPrerequisites(BakePhase phase)
    {
        // The depth and altitude bands have no geometry of their own, so their proxy
        // quads, surfaces and carve volumes have to exist before the surfaces are
        // collected - not after.
        if (phase == BakePhase.SubSurface) Layers().BuildBands(NavAgentTypes.Submarine);
        if (phase == BakePhase.Airspace) Layers().BuildBands(NavAgentTypes.Aircraft);

        if (phase != BakePhase.SurfaceWater && phase != BakePhase.SubSurface) return;
        if (!carveDirty) return;

        if (waterCarver == null) waterCarver = FindObjectOfType<WaterNavMeshCarver>();

        // Create one rather than warn: without it the water NavMesh covers the islands
        // and ships sail straight through them, which is a far worse default than an
        // auto-added component with sensible values.
        if (waterCarver == null) waterCarver = gameObject.AddComponent<WaterNavMeshCarver>();

        waterCarver.RebuildCarveVolumes();
        carveDirty = false;
    }

    /// <summary>
    /// Work that can only happen once a phase's NavMesh exists. Links are the whole
    /// of it: a NavMeshLink needs baked NavMesh at both ends to attach to, so
    /// building one before the bake silently produces a link joining nothing.
    /// </summary>
    private void RunPostBake(BakePhase phase)
    {
        if (phase == BakePhase.SubSurface) Links().BuildLinks(NavAgentTypes.Submarine);
        if (phase == BakePhase.Airspace) Links().BuildLinks(NavAgentTypes.Aircraft);
    }

    private StackedNavMeshLayers Layers()
    {
        if (stackedLayers == null) stackedLayers = FindObjectOfType<StackedNavMeshLayers>();
        if (stackedLayers == null) stackedLayers = gameObject.AddComponent<StackedNavMeshLayers>();
        return stackedLayers;
    }

    private NavTransitionLinks Links()
    {
        if (transitionLinks == null) transitionLinks = FindObjectOfType<NavTransitionLinks>();
        if (transitionLinks == null) transitionLinks = gameObject.AddComponent<NavTransitionLinks>();
        return transitionLinks;
    }

    private IEnumerable<NavMeshSurface> SurfacesFor(BakePhase phase)
    {
        foreach (NavMeshSurface surface in AllSurfaces())
        {
            if (surface == null) continue;
            if (PhaseOf(surface) == phase) yield return surface;
        }
    }

    /// <summary>
    /// The surfaces to bake: the serialized override when it holds anything real,
    /// otherwise every NavMeshSurface in the scene.
    ///
    /// Discovery is the default because the map is generated at runtime and surfaces
    /// get added per agent type as the game grows - an inspector list silently rots
    /// into null slots the moment a scene is reworked, and a null slot bakes nothing
    /// while looking perfectly fine in the inspector.
    /// </summary>
    private IEnumerable<NavMeshSurface> AllSurfaces()
    {
        if (surfaces != null)
        {
            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface != null) return surfaces;
            }
        }

        return FindObjectsOfType<NavMeshSurface>();
    }

    private static BakePhase PhaseOf(NavMeshSurface surface)
    {
        string agentType = NavMesh.GetSettingsNameFromID(surface.agentTypeID);

        BakePhase phase;
        if (PhaseByAgentType.TryGetValue(agentType, out phase)) return phase;

        Debug.LogWarning($"RuntimeNavMeshBaker: agent type '{agentType}' on '{surface.name}' " +
                         $"has no bake phase - defaulting to {BakePhase.Terrain}.", surface);
        return BakePhase.Terrain;
    }

    private void OnDestroy()
    {
        // Static state must not survive into the next play session / scene load.
        if (Application.isPlaying) IsBaked = false;
    }
}
