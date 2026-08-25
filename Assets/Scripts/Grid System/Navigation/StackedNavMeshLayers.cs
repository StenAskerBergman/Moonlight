using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Cell;

using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using CollectObjects = Unity.AI.Navigation.CollectObjects;

/// <summary>
/// Builds the NavMesh layers that have no geometry of their own: the submarine's
/// deep-sea layer and the aircraft altitude bands.
///
/// A submarine at -30 and an aircraft at +40 have nothing to walk on, so each band
/// gets a flat proxy quad at its own height, collected by its own NavMeshSurface
/// through a thin Volume box. All the proxies share one layer (NavProxy) and are
/// separated purely by height, which keeps them invisible to the terrain and water
/// surfaces and keeps each band invisible to the others.
///
/// Bands of the same agent type stack: Submarine owns both the surface layer (the
/// existing world-geometry surface) and the deep layer built here, which is what
/// makes a dive a NavMeshLink between two layers rather than an agent-type switch.
/// Unity has no cross-agent-type links, so this stacking is not optional.
/// </summary>
public class StackedNavMeshLayers : MonoBehaviour
{
    /// <summary>What blocks a band, expressed in the terrain generator's own vocabulary.</summary>
    public enum BandClearance
    {
        /// <summary>Nothing blocks it - open sky above every peak.</summary>
        None,

        /// <summary>Blocked over anything that is not navigable water.</summary>
        AboveLand,

        /// <summary>Only open over genuinely deep seabed. This is what stops a
        /// submarine diving in the shallows.</summary>
        DeepWaterOnly,

        /// <summary>Blocked over mountains, cliffs and hills.</summary>
        AboveMountains,
    }

    /// <summary>Where a band's walkable geometry comes from.</summary>
    public enum BandSource
    {
        /// <summary>A generated flat quad at the band's height - open water, open sky.</summary>
        Proxy,

        /// <summary>Real scene geometry, so the band follows the terrain. This is what
        /// an aircraft apron needs: you take off from the ground, not from a plane
        /// floating at a fixed altitude.</summary>
        World,
    }

    [System.Serializable]
    public class NavBand
    {
        [Tooltip("Name of the generated surface, for the hierarchy and the logs.")]
        public string label = "Band";

        [Tooltip("Agent type name from Navigation > Agents, e.g. Submarine or Aircraft.")]
        public string agentTypeName = NavAgentTypes.Submarine;

        [Tooltip("World height of the band.")]
        public float height = -30f;

        [Tooltip("NavMesh area for the whole band - see NavAreas.")]
        public int area = NavAreas.DeepSea;

        [Tooltip("What punches holes in this band.")]
        public BandClearance clearance = BandClearance.DeepWaterOnly;

        [Tooltip("Proxy generates a flat quad at 'height'. World bakes real geometry " +
                 "from 'worldLayers' instead, for bands that must follow the terrain.")]
        public BandSource source = BandSource.Proxy;

        [Tooltip("Layers collected when source is World.")]
        public LayerMask worldLayers = 0;

        [Tooltip("Vertical extent collected when source is World.")]
        public float worldThickness = 200f;

        [System.NonSerialized] public NavMeshSurface surface;
    }

    [Tooltip("Depth and altitude bands to generate. The surface-level water layers are " +
             "baked from real geometry elsewhere and do not belong here.")]
    [SerializeField]
    private NavBand[] bands =
    {
        new NavBand { label = "Sub - Deep",      agentTypeName = NavAgentTypes.Submarine, height = -30f, area = NavAreas.DeepSea,      clearance = BandClearance.DeepWaterOnly },
        new NavBand { label = "Air - Apron",     agentTypeName = NavAgentTypes.Aircraft,  height =   0f, area = NavAreas.Walkable,     clearance = BandClearance.None, source = BandSource.World, worldLayers = (1 << 4) | (1 << 6) },
        new NavBand { label = "Air - Low",       agentTypeName = NavAgentTypes.Aircraft,  height =  25f, area = NavAreas.LowAltitude,  clearance = BandClearance.AboveMountains },
        new NavBand { label = "Air - Mid",       agentTypeName = NavAgentTypes.Aircraft,  height =  50f, area = NavAreas.MidAltitude,  clearance = BandClearance.None },
        new NavBand { label = "Air - High",      agentTypeName = NavAgentTypes.Aircraft,  height =  75f, area = NavAreas.HighAltitude, clearance = BandClearance.None },
    };

    [Tooltip("Layer the proxy quads and their carve volumes live on. Must be outside " +
             "the terrain and water surfaces' layer masks.")]
    [SerializeField] private int proxyLayer = 10; // NavProxy

    [Tooltip("Width and depth of the generated proxy quads. Should cover the map.")]
    [SerializeField] private float mapSize = 1000f;

    [Tooltip("Height of each band's collection box. Tall enough to contain the proxy " +
             "and its carve volumes, short enough not to reach the next band.")]
    [SerializeField] private float bandThickness = 6f;

    [Tooltip("Voxel size for band bakes. Bands are dead flat, so the default 0.17 is " +
             "wasted precision over a 1000x1000 quad - this is the difference between " +
             "a bake measured in seconds and one measured in minutes.")]
    [SerializeField] private float bandVoxelSize = 1f;

    [Tooltip("Log each band's surface and carve count.")]
    [SerializeField] private bool logBands = false;

    private const string RootName = "Generated Nav Bands";
    private const string CarveRootPrefix = "Band Carve - ";

    private static readonly HashSet<TerrainType> NavigableWater = new HashSet<TerrainType>
    {
        TerrainType.Abyssal, TerrainType.Deep, TerrainType.Plateau, TerrainType.Shallow,
        TerrainType.Water, TerrainType.Sea, TerrainType.Ocean, TerrainType.River, TerrainType.Stream,
    };

    private static readonly HashSet<TerrainType> DeepSeabed = new HashSet<TerrainType>
    {
        TerrainType.Abyssal, TerrainType.Deep, TerrainType.Plateau,
    };

    private static readonly HashSet<TerrainType> HighGround = new HashSet<TerrainType>
    {
        TerrainType.Hill, TerrainType.HillSide,
        TerrainType.Cliff, TerrainType.CliffWall, TerrainType.CliffPeak,
        TerrainType.Mountain, TerrainType.MountainWall, TerrainType.MountainPeak, TerrainType.MountainSummit,
    };

    private Transform root;

    /// <summary>The surfaces this component generated, in band order.</summary>
    public IEnumerable<NavMeshSurface> Surfaces
    {
        get
        {
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].surface != null) yield return bands[i].surface;
            }
        }
    }

    /// <summary>
    /// Creates (or refreshes) the proxy geometry, surfaces and carve volumes for
    /// every band of the given agent type. Does not bake - RuntimeNavMeshBaker
    /// discovers the surfaces and bakes them as part of the matching phase.
    /// </summary>
    public void BuildBands(string agentTypeName)
    {
        if (!NavAgentTypes.Exists(agentTypeName))
        {
            Debug.LogWarning($"StackedNavMeshLayers: no '{agentTypeName}' agent type - " +
                             "skipping its bands.");
            return;
        }

        EnsureRoot();

        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i].agentTypeName != agentTypeName) continue;
            BuildBand(bands[i]);
        }
    }

    private void EnsureRoot()
    {
        if (root != null) return;

        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            root = existing;
            return;
        }

        GameObject go = new GameObject(RootName);
        go.transform.SetParent(transform, false);
        root = go.transform;
    }

    private void BuildBand(NavBand band)
    {
        GameObject bandGO = FindOrCreateChild(root, band.label);
        bandGO.layer = proxyLayer;
        bandGO.transform.position = new Vector3(0f, band.height, 0f);

        bool proxy = band.source == BandSource.Proxy;
        if (proxy) EnsureProxyQuad(bandGO);

        NavMeshSurface surface = bandGO.GetComponent<NavMeshSurface>();
        if (surface == null) surface = bandGO.AddComponent<NavMeshSurface>();

        surface.agentTypeID = NavAgentTypes.Id(band.agentTypeName);
        surface.collectObjects = CollectObjects.Volume;
        surface.center = Vector3.zero;
        surface.size = new Vector3(mapSize, proxy ? bandThickness : band.worldThickness, mapSize);
        surface.layerMask = proxy ? (1 << proxyLayer) : band.worldLayers.value;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = band.area;

        // Flat proxies do not need sub-decimetre voxels over a 1000x1000 quad; world
        // bands follow terrain and do, so leave their voxel size alone.
        surface.overrideVoxelSize = proxy;
        if (proxy) surface.voxelSize = bandVoxelSize;

        band.surface = surface;

        int carved = RebuildBandCarving(band);

        if (logBands)
        {
            Debug.Log($"<color=cyan>StackedNavMeshLayers:</color> band '{band.label}' at y={band.height} " +
                      $"(agent {band.agentTypeName}, area {band.area}), {carved} carve volume(s).");
        }
    }

    /// <summary>
    /// A flat quad is all Recast needs for a band. Two triangles, one MeshCollider,
    /// no renderer - these are pathfinding scaffolding and must never be visible.
    /// </summary>
    private void EnsureProxyQuad(GameObject bandGO)
    {
        MeshCollider collider = bandGO.GetComponent<MeshCollider>();
        if (collider != null) return;

        float half = mapSize * 0.5f;
        Mesh mesh = new Mesh { name = "NavBandProxy" };
        mesh.vertices = new[]
        {
            new Vector3(-half, 0f, -half),
            new Vector3(-half, 0f,  half),
            new Vector3( half, 0f,  half),
            new Vector3( half, 0f, -half),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        collider = bandGO.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    /// <summary>
    /// Punches this band's holes, per island, from the terrain grid. Runs before the
    /// bake and replaces the previous pass so a terraform cannot leave a stale hole.
    /// </summary>
    private int RebuildBandCarving(NavBand band)
    {
        int total = 0;
        string carveRootName = CarveRootPrefix + band.label;

        foreach (MapGrid mapGrid in FindObjectsOfType<MapGrid>())
        {
            Transform stale = mapGrid.transform.Find(carveRootName);
            if (stale != null) DestroyImmediate(stale.gameObject);

            if (band.clearance == BandClearance.None) continue;
            if (band.source != BandSource.Proxy) continue; // World bands are already terrain-shaped.

            Cell[,] grid = mapGrid.Grid;
            if (grid == null) continue;

            int width = grid.GetLength(0);
            int depth = grid.GetLength(1);

            bool[,] blocked = new bool[width, depth];
            bool any = false;

            for (int y = 0; y < depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool b = IsBlocked(grid[x, y], band.clearance);
                    blocked[x, y] = b;
                    any |= b;
                }
            }

            if (!any) continue;

            GameObject carveRoot = new GameObject(carveRootName);
            carveRoot.transform.SetParent(mapGrid.transform, false);
            carveRoot.layer = proxyLayer;

            foreach (RectInt rect in NavCarve.MergeIntoRectangles(blocked, width, depth))
            {
                NavCarve.CreateVolume(carveRoot.transform, rect, proxyLayer,
                                      band.height, bandThickness * 0.5f, 0f, NavAreas.NotWalkable);
                total++;
            }
        }

        return total;
    }

    private static bool IsBlocked(Cell cell, BandClearance clearance)
    {
        // A missing cell is treated as solid: better a hole in a band than a
        // submarine pathing through terrain that failed to generate.
        if (cell == null) return true;

        TerrainType terrain = cell.currentTerrainType;

        switch (clearance)
        {
            case BandClearance.AboveLand:
                return !NavigableWater.Contains(terrain);

            case BandClearance.DeepWaterOnly:
                return !DeepSeabed.Contains(terrain);

            case BandClearance.AboveMountains:
                return HighGround.Contains(terrain);

            default:
                return false;
        }
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }
}
