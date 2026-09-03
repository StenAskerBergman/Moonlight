using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Cell;

// Assets/NavMeshComponents duplicates these types in UnityEngine.AI, so the bare
// name is ambiguous. The scene's surfaces resolve to the package, so alias to it.
using NavMeshModifierVolume = Unity.AI.Navigation.NavMeshModifierVolume;

/// <summary>
/// Punches island-shaped holes in the water NavMeshes.
///
/// Why this is needed at all: marking island geometry "Not Walkable" with a
/// NavMeshModifier does NOT remove the ocean surface underneath it. Recast only
/// drops a walkable span when something above it is closer than the agent's
/// height, and the Ship agent is 0.5 units tall while island terrain sits several
/// units higher - so the ocean span at water level survives and ships sail
/// straight through the island. A NavMeshModifierVolume is different: it rewrites
/// the area of *every* span inside the box, whichever geometry produced it, so
/// area 1 (Not Walkable) genuinely removes the ocean surface there.
///
/// The volumes are placed on the Water layer, which is inside the water surfaces'
/// layer masks but outside the Humanoid surface's, so the same boxes carve the
/// ocean while leaving the island's own walkable NavMesh untouched.
/// </summary>
public class WaterNavMeshCarver : MonoBehaviour
{
    /// <summary>Unity's built-in "Not Walkable" area index.</summary>
    public const int NotWalkableArea = 1;

    [Tooltip("Layer the carve volumes are placed on. Must be inside the layer mask of " +
             "every water NavMeshSurface, and outside the Humanoid surface's mask.")]
    [SerializeField] private int carveLayer = 4; // Water

    [Tooltip("World height of the water surface the volumes are centred on.")]
    [SerializeField] private float waterSurfaceY = 0f;

    [Tooltip("Vertical thickness of each carve volume. Only has to be tall enough to " +
             "swallow the surface NavMesh - keep it short so it does not also carve a " +
             "future deep-sea layer.")]
    [SerializeField] private float carveHeight = 8f;

    [Tooltip("Grow each carved rectangle outwards by this much, in cells, to keep ships " +
             "off the shoreline. 0 carves the exact land footprint.")]
    [SerializeField] private float shoreMargin = 4f; //1.5f;

    [Tooltip("Log how many volumes each island produced.")]
    [SerializeField] private bool logCarving = false;

    private const string CarveRootName = "Water Carve Volumes";

    /// <summary>
    /// Terrain the water agents are allowed to occupy. Everything not in here is
    /// treated as land and carved out of the water NavMeshes.
    /// </summary>
    private static readonly HashSet<TerrainType> NavigableWater = new HashSet<TerrainType>
    {
        TerrainType.Abyssal,
        TerrainType.Deep,
        TerrainType.Plateau,
        TerrainType.Shallow,
        TerrainType.Water,
        TerrainType.Sea,
        TerrainType.Ocean,
        TerrainType.River,
        TerrainType.Lake,
        TerrainType.Stream,
    };

    /// <summary>
    /// Rebuilds the carve volumes for every generated island in the scene.
    /// Safe to call repeatedly - each island's previous volumes are destroyed first.
    /// Must run before the water surfaces bake.
    /// </summary>
    public int RebuildCarveVolumes()
    {
        int total = 0;

        foreach (MapGrid mapGrid in FindObjectsOfType<MapGrid>())
        {
            total += CarveIsland(mapGrid);
        }

        return total;
    }

    private int CarveIsland(MapGrid mapGrid)
    {
        Transform island = mapGrid.transform;

        // Drop the previous pass before measuring again, so a terraform that turned
        // land back into water does not leave a stale hole behind.
        Transform stale = island.Find(CarveRootName);
        if (stale != null) DestroyImmediate(stale.gameObject);

        Cell[,] grid = mapGrid.Grid;
        if (grid == null) return 0; // Terrain not generated - nothing to carve.

        int width = grid.GetLength(0);
        int depth = grid.GetLength(1);

        bool[,] blocked = new bool[width, depth];
        bool anyBlocked = false;

        for (int y = 0; y < depth; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Cell cell = grid[x, y];
                bool isLand = cell == null || !NavigableWater.Contains(cell.currentTerrainType);
                blocked[x, y] = isLand;
                anyBlocked |= isLand;
            }
        }

        if (!anyBlocked) return 0; // A pure ocean tile - leave the water intact.

        GameObject root = new GameObject(CarveRootName);
        root.transform.SetParent(island, false);
        root.layer = carveLayer;

        int count = 0;
        foreach (RectInt rect in NavCarve.MergeIntoRectangles(blocked, width, depth))
        {
            NavCarve.CreateVolume(root.transform, rect, carveLayer, waterSurfaceY,
                                  carveHeight, shoreMargin, NotWalkableArea);
            count++;
        }

        if (logCarving)
        {
            Debug.Log($"<color=cyan>WaterNavMeshCarver:</color> '{island.name}' carved with {count} volume(s).");
        }

        return count;
    }

}
