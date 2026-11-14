// tldr: Grid-side rules for the player planting and clearing foliage. Holds no input; mirrors
// CropFieldPlacer so both player-authored grid tools validate and mutate the same way.
using UnityEngine;

/// <summary>
/// Validates and performs player foliage edits against the island under a world position.
///
/// Everything here goes through <see cref="IslandFoliagePlacer.Plant"/> and the island's
/// <see cref="FoliageStore"/>, so a player-planted tree is indistinguishable from a generated one to
/// construction clearance and forestry - the only difference is its <see cref="FoliageOrigin"/>.
/// </summary>
public class FoliagePlacer : MonoBehaviour
{
    [Header("Limits")]
    [Tooltip("Stops the player stacking an unbounded number of plants on one cell.")]
    [Min(1)] public int maxInstancesPerCell = 4;

    [Header("Grid Reference")]
    [SerializeField] private GridSystem gridSystem;

    private readonly System.Collections.Generic.List<int> scratch = new System.Collections.Generic.List<int>();

    private GridSystem ActiveGridSystem
    {
        get
        {
            if (gridSystem != null) return gridSystem;
            return IslandManager.instance != null ? IslandManager.instance.GetCurrentGridSystem() : null;
        }
    }

    /// <summary>The island whose foliage a world point belongs to, or null.</summary>
    public IslandFoliagePlacer GetIslandAt(Collider hitCollider)
    {
        return hitCollider != null ? hitCollider.GetComponentInParent<IslandFoliagePlacer>() : null;
    }

    /// <summary>
    /// The cell under a terrain hit. Each island carries its own GridSystem on the same GameObject as
    /// its MapGrid and foliage placer, so the island actually under the cursor is preferred over
    /// IslandManager's notion of the current one - otherwise planting near an island the camera is not
    /// focused on would resolve against the wrong grid.
    /// </summary>
    public Cell GetCellAt(Collider hitCollider, Vector3 worldPosition)
    {
        GridSystem grid = hitCollider != null ? hitCollider.GetComponentInParent<GridSystem>() : null;
        if (grid == null) grid = ActiveGridSystem;
        return grid != null ? grid.GetCellAtWorldPosition(worldPosition) : null;
    }

    /// <summary>
    /// Whether the player may plant here.
    /// 1. Species must exist and be flagged plantable
    /// 2. Island and cell must resolve
    /// 3. Cell must be free of buildings, blockers and roads
    /// 4. Terrain must be buildable surface - the same bar the generator scatters against
    /// 5. Cell must not already be full
    /// </summary>
    public bool CanPlant(IslandFoliagePlacer island, Cell cell, FoliageSpecies species, out string failureReason)
    {
        failureReason = string.Empty;

        if (species == null) { failureReason = "No species selected."; return false; }
        if (!species.playerPlantable) { failureReason = $"{species.displayName} cannot be planted."; return false; }
        if (island == null) { failureReason = "Not over an island."; return false; }
        if (cell == null) { failureReason = "Invalid grid position."; return false; }

        if (cell.isOccupied || cell.isBlocked || cell.isRoad)
        {
            failureReason = "Tile is occupied or blocked.";
            return false;
        }

        if (!cell.IsBuildableSurface)
        {
            failureReason = "Terrain is invalid (water, steep slope, or mountain).";
            return false;
        }

        if (island.Store.QueryCell(new Vector2Int(cell.cellPosition.x, cell.cellPosition.z), scratch) >= maxInstancesPerCell)
        {
            failureReason = "No room left on this tile.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Plants one instance at a world point, which is expected to be a terrain raycast hit so its
    /// height is already the ground height. Returns the new id, or 0.
    /// </summary>
    public int Plant(IslandFoliagePlacer island, Cell cell, FoliageSpecies species, Vector3 worldPoint)
    {
        if (!CanPlant(island, cell, species, out _)) return 0;

        Vector3 local = island.transform.InverseTransformPoint(worldPoint);
        float yaw = UnityEngine.Random.Range(0f, 360f);
        float scale = UnityEngine.Random.Range(species.scaleMin, species.scaleMax);
        return island.Plant(cell, species, local, yaw, scale, FoliageOrigin.Player);
    }

    /// <summary>
    /// Removes the plant nearest a world point, within that species' pick radius. Returns the id
    /// removed, or 0 when the click landed on bare ground.
    ///
    /// Deliberately no colliders: the store's cell index makes "nearest instance to this point" a
    /// lookup over the handful of cells the radius covers, which is cheaper than putting a collider
    /// on every tree and matches the grid granularity the rest of the game works in.
    /// </summary>
    public int ClearNearest(IslandFoliagePlacer island, Vector3 worldPoint, float searchRadius)
    {
        if (island == null) return 0;

        Vector3 local = island.transform.InverseTransformPoint(worldPoint);
        int id = island.Store.FindNearest(local, searchRadius);
        if (id == 0) return 0;

        // Honour the species' own pick radius once we know which species was actually hit.
        if (island.Store.TryGet(id, out FoliageInstance instance))
        {
            FoliageSpecies species = island.Store.GetSpecies(instance.SpeciesId);
            if (species != null)
            {
                float dx = instance.LocalPosition.x - local.x;
                float dz = instance.LocalPosition.z - local.z;
                if (dx * dx + dz * dz > species.pickRadius * species.pickRadius) return 0;
            }
        }

        return island.Store.Remove(id, FoliageRemovalCause.PlayerClear) ? id : 0;
    }
}
