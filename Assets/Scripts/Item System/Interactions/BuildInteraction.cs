// Start - BuildInteraction.cs
using System.Collections.Generic;
using UnityEngine;

public class BuildInteraction : MonoBehaviour, IBuildable
{
    public Inventory unitInventory;
    private UnitInventory unitCargoInventory;

    [Tooltip("Maximum reach/influence distance from the boat to a candidate harbor cell. Defaults to InfluenceManager.BoatFoundingRange (30).")]
    [SerializeField] private float buildRange = InfluenceManager.BoatFoundingRange;
    [SerializeField] private List<ResourceRequirement> requiredResources = new List<ResourceRequirement>();

    // The harbor building prefab (e.g. Depot). Must carry BuildingProperties and InfluenceZone.
    // If not assigned in the inspector, it is resolved dynamically from BuildingPrefabRegistry or scene buttons.
    [SerializeField] private GameObject harborBuildingPrefab;

    public float BuildRange
    {
        get
        {
            // The vessel's own Settlement range wins, so the circle the player sees is
            // exactly the reach this check enforces.
            Settlement settlement = GetComponent<Settlement>();
            if (settlement != null) return settlement.SettleRange;

            return buildRange > 0f ? buildRange : InfluenceManager.BoatFoundingRange;
        }
        set => buildRange = value;
    }

    public GameObject HarborBuildingPrefab
    {
        get
        {
            if (harborBuildingPrefab != null) return harborBuildingPrefab;
            harborBuildingPrefab = ResolveHarborPrefab();
            return harborBuildingPrefab;
        }
        set => harborBuildingPrefab = value;
    }

    public delegate void BuildSucceededHandler(ItemData item);
    public event BuildSucceededHandler OnBuildSucceeded;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
        unitCargoInventory = GetComponent<UnitInventory>();
    }

    public void Build(ItemData item)
    {
        if (!TryGetHarborPlacement(out Island targetIsland, out GridSystem targetGrid, out Vector3 targetPosition, out string placementFailReason))
        {
            Debug.Log($"BuildInteraction: Cannot build - {placementFailReason}");
            return;
        }

        if (IslandAlreadySettled(targetIsland))
        {
            Debug.Log($"BuildInteraction: Cannot build - '{targetIsland.islandName}' already has a harbor.");
            return;
        }

        if (!HasRequiredResources())
        {
            Debug.Log("BuildInteraction: Cannot build - missing required resources.");
            return;
        }

        GameObject prefabToBuild = HarborBuildingPrefab;
        if (prefabToBuild == null)
        {
            Debug.LogWarning("BuildInteraction: harborBuildingPrefab is not assigned and could not be resolved - cannot place building.");
            return;
        }

        // If interactive building placement is available in the scene, enter preview placement mode
        BuildingSelector buildingSelector = FindObjectOfType<BuildingSelector>();
        if (buildingSelector != null)
        {
            buildingSelector.CancelPreview();
            buildingSelector.SpawnPreview(prefabToBuild, targetIsland, GetComponent<Unit>());
            OnBuildSucceeded?.Invoke(item);
            return;
        }

        // Direct placement fallback (e.g. headless or direct instantiation)
        Transform parentTransform = targetIsland.islandObject != null ? targetIsland.islandObject.transform : targetIsland.transform;
        GameObject buildingInstance = Instantiate(prefabToBuild, targetPosition, Quaternion.identity, parentTransform);

        InfluenceZone zone = buildingInstance.GetComponent<InfluenceZone>();
        InfluenceManager influenceManager = targetIsland.islandObject != null
            ? targetIsland.islandObject.GetComponent<InfluenceManager>()
            : targetIsland.GetComponent<InfluenceManager>();
        if (zone != null && influenceManager != null)
        {
            influenceManager.RegisterZone(zone);
        }

        DeductRequiredResources();

        Debug.Log($"BuildInteraction: Harbor built at {targetPosition} by {gameObject.name}.");
        OnBuildSucceeded?.Invoke(item);
    }

    /// <summary>
    /// Runs the same checks as Build() without performing any of the side effects.
    /// Used by UI to decide whether the build action should be shown/enabled.
    /// </summary>
    public bool CanBuild()
    {
        if (!TryGetHarborPlacement(out Island targetIsland, out _, out _, out _)) return false;

        // Founding is a one-shot action per island. Without this the "Build Harbor"
        // button stayed on the action bar forever, offering to found an island that
        // already had a depot on it.
        if (IslandAlreadySettled(targetIsland)) return false;

        return HasRequiredResources();
    }

    /// <summary>
    /// Whether this island already carries a harbor, so the founding action is spent.
    /// Asks the island's InfluenceManager, which is the same authority the placement
    /// rules use, rather than counting buildings independently.
    /// </summary>
    private static bool IslandAlreadySettled(Island island)
    {
        if (island == null) return false;

        InfluenceManager influenceManager = island.islandObject != null
            ? island.islandObject.GetComponent<InfluenceManager>()
            : island.GetComponent<InfluenceManager>();

        return influenceManager != null && influenceManager.HasWarehouse;
    }

    private bool TryGetHarborPlacement(out Island targetIsland, out GridSystem targetGrid, out Vector3 targetPosition, out string failReason)
    {
        targetIsland = null;
        targetGrid = null;
        targetPosition = Vector3.zero;
        failReason = null;

        IReadOnlyList<Island> candidateIslands = GetPlaceableIslands();
        if (candidateIslands.Count == 0)
        {
            failReason = "no generated islands in scene";
            return false;
        }

        float maxRange = BuildRange;
        Vector3 boatPos = transform.position;
        Vector3 boatFlat = new Vector3(boatPos.x, 0f, boatPos.z);

        Cell closestCell = null;
        float minDistance = float.MaxValue;
        Island bestIsland = null;
        GridSystem bestGrid = null;
        Vector3 bestWorldPos = Vector3.zero;

        foreach (Island island in candidateIslands)
        {
            if (island == null) continue;
            GridSystem gs = island.GetComponent<GridSystem>() ?? island.GetComponentInChildren<GridSystem>();
            if (gs == null) continue;

            InfluenceManager influenceManager = island.islandObject != null
                ? island.islandObject.GetComponent<InfluenceManager>()
                : island.GetComponent<InfluenceManager>();

            for (int x = 0; x < gs.gridSize; x++)
            {
                for (int z = 0; z < gs.gridSize; z++)
                {
                    Cell cell = gs.GetCell(x, z);
                    if (cell == null || cell.isBlocked || cell.isOccupied) continue;

                    Vector3 cellWorldPos = gs.transform.TransformPoint(cell.localCenter);
                    Vector3 cellFlat = new Vector3(cellWorldPos.x, 0f, cellWorldPos.z);
                    float dist = Vector3.Distance(boatFlat, cellFlat);

                    if (dist > maxRange) continue;

                    // Verify whether this candidate cell satisfies harbor placement rules
                    bool canPlaceHere = false;
                    if (influenceManager != null)
                    {
                        canPlaceHere = influenceManager.CanPlaceWarehouse(cellWorldPos, gs);
                    }
                    else
                    {
                        canPlaceHere = cell.currentTerrainType == Cell.TerrainType.Beach;
                    }

                    if (canPlaceHere && dist < minDistance)
                    {
                        minDistance = dist;
                        closestCell = cell;
                        bestIsland = island;
                        bestGrid = gs;
                        bestWorldPos = cellWorldPos;
                    }
                }
            }
        }

        if (closestCell == null)
        {
            failReason = "no valid harbor cell within boat reach as influence";
            return false;
        }

        targetIsland = bestIsland;
        targetGrid = bestGrid;
        targetPosition = bestWorldPos;
        return true;
    }

    /// <summary>
    /// The islands harbor placement may consider. MapManager owns map generation and is
    /// the list that actually gets filled; IslandManager.islands is created empty in its
    /// Start() and nothing ever adds to it, so reading only that made every placement
    /// search iterate nothing and CanBuild() return false on every map.
    /// </summary>
    private static IReadOnlyList<Island> GetPlaceableIslands()
    {
        if (MapManager.instance != null && MapManager.instance.islands != null && MapManager.instance.islands.Count > 0)
        {
            return MapManager.instance.islands;
        }

        if (IslandManager.instance != null && IslandManager.instance.islands != null && IslandManager.instance.islands.Count > 0)
        {
            return IslandManager.instance.islands;
        }

        return System.Array.Empty<Island>();
    }

    private GameObject ResolveHarborPrefab()
    {
        // 1. Try BuildingPrefabRegistry if active
        if (BuildingPrefabRegistry.Instance != null)
        {
            foreach (string id in BuildingPrefabRegistry.Instance.AllIdentifiers)
            {
                GameObject prefab = BuildingPrefabRegistry.Instance.GetPrefab(id);
                if (IsHarborOrWarehousePrefab(prefab))
                {
                    return prefab;
                }
            }
        }

        // 2. Scan BuildingButtons in the scene
        BuildingButton[] buttons = FindObjectsOfType<BuildingButton>(includeInactive: true);
        foreach (BuildingButton btn in buttons)
        {
            GameObject prefab = btn.GetBuildingPrefab();
            if (IsHarborOrWarehousePrefab(prefab))
            {
                return prefab;
            }
        }

        return null;
    }

    // One shared definition of "this is a harbor", used by the action bar, the placement
    // preview and the influence check alike, so they can never disagree about whether the
    // founding building is being placed.
    private static bool IsHarborOrWarehousePrefab(GameObject prefab)
    {
        return InfluenceManager.IsHarborBuilding(prefab);
    }

    private bool HasRequiredResources()
    {
        if (requiredResources != null && requiredResources.Count > 0)
        {
            foreach (var requirement in requiredResources)
            {
                if (requirement == null || requirement.item == null) continue;

                if (!requirement.IsSatisfiedBy(GetAvailableQuantity(requirement.item)))
                {
                    return false;
                }
            }
        }

        GameObject prefab = HarborBuildingPrefab;
        if (prefab != null)
        {
            BuildingCost costComponent = prefab.GetComponent<BuildingCost>();
            if (costComponent != null && costComponent.costData != null)
            {
                Dictionary<ItemData, int> costItems = costComponent.costData.GetCostItemsDictionary();
                if (costItems != null)
                {
                    foreach (var kvp in costItems)
                    {
                        if (kvp.Key != null && kvp.Value > 0)
                        {
                            if (GetAvailableQuantity(kvp.Key) < kvp.Value)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }

        return true;
    }

    private void DeductRequiredResources()
    {
        if (requiredResources != null)
        {
            foreach (var requirement in requiredResources)
            {
                if (requirement == null || requirement.item == null || requirement.amount <= 0) continue;
                DeductItem(requirement.item, requirement.amount);
            }
        }

        GameObject prefab = HarborBuildingPrefab;
        if (prefab != null)
        {
            BuildingCost costComponent = prefab.GetComponent<BuildingCost>();
            if (costComponent != null && costComponent.costData != null)
            {
                Dictionary<ItemData, int> costItems = costComponent.costData.GetCostItemsDictionary();
                if (costItems != null)
                {
                    foreach (var kvp in costItems)
                    {
                        if (kvp.Key != null && kvp.Value > 0)
                        {
                            DeductItem(kvp.Key, kvp.Value);
                        }
                    }
                }
            }
        }
    }

    private void DeductItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (unitCargoInventory != null)
        {
            unitCargoInventory.RemoveItem(item, amount);
        }
        else if (unitInventory != null)
        {
            unitInventory.RemoveItem(item, amount);
        }
    }

    private int GetAvailableQuantity(ItemData item)
    {
        if (unitCargoInventory != null)
        {
            return unitCargoInventory.GetItemQuantity(item);
        }

        if (unitInventory != null)
        {
            return unitInventory.GetItemAmount(item);
        }

        return 0;
    }
}
// End - BuildInteraction.cs
