// Start - BuildInteraction.cs
using System.Collections.Generic;
using UnityEngine;

public class BuildInteraction : MonoBehaviour, IBuildable
{
    public Inventory unitInventory;
    private UnitInventory unitCargoInventory;

    [SerializeField] private float buildRange = 5f;
    [SerializeField] private List<ResourceRequirement> requiredResources = new List<ResourceRequirement>();

    // TODO: Assign the harbor building prefab in the inspector. It must carry
    // BuildingProperties (+ BuildingData with BuildingType.OnShore) and, if it should
    // extend trade influence, an InfluenceZone so InfluenceManager.RegisterZone picks it up.
    [SerializeField] private GameObject harborBuildingPrefab;

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

        if (!HasRequiredResources())
        {
            Debug.Log("BuildInteraction: Cannot build - missing required resources.");
            return;
        }

        // TODO: project-specific wiring - route this through BuildingPlacer/BuildingChecker
        // instead once units can drive the BuildingPreview pipeline directly; for now this
        // places the harbor prefab straight onto the grid.
        if (harborBuildingPrefab == null)
        {
            Debug.LogWarning("BuildInteraction: harborBuildingPrefab is not assigned - cannot place building.");
            return;
        }

        GameObject buildingInstance = Instantiate(harborBuildingPrefab, targetPosition, Quaternion.identity, targetIsland.islandObject.transform);

        InfluenceZone zone = buildingInstance.GetComponent<InfluenceZone>();
        InfluenceManager influenceManager = targetIsland.islandObject.GetComponent<InfluenceManager>();
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
        return TryGetHarborPlacement(out _, out _, out _, out _) && HasRequiredResources();
    }

    private bool TryGetHarborPlacement(out Island targetIsland, out GridSystem targetGrid, out Vector3 targetPosition, out string failReason)
    {
        targetIsland = null;
        targetGrid = null;
        targetPosition = Vector3.zero;
        failReason = null;

        if (IslandManager.instance == null || IslandManager.instance.islands == null)
        {
            failReason = "no IslandManager or islands in scene";
            return false;
        }

        Cell closestCell = null;
        float minDistance = float.MaxValue;
        
        foreach (Island island in IslandManager.instance.islands)
        {
            GridSystem gs = island.GetComponent<GridSystem>();
            if (gs == null) continue;
            
            for (int x = 0; x < gs.gridSize; x++)
            {
                for (int z = 0; z < gs.gridSize; z++)
                {
                    Cell cell = gs.GetCell(x, z);
                    if (cell == null || cell.isBlocked || cell.isOccupied) continue;
                    
                    Vector3 cellWorldPos = gs.transform.TransformPoint(cell.localCenter);
                    float dist = Vector3.Distance(transform.position, cellWorldPos);
                    
                    if (dist < minDistance && dist <= buildRange)
                    {
                        minDistance = dist;
                        closestCell = cell;
                        targetIsland = island;
                        targetGrid = gs;
                        targetPosition = cellWorldPos;
                    }
                }
            }
        }

        if (closestCell == null)
        {
            failReason = "no buildable cell within build range";
            return false;
        }


        InfluenceManager influenceManager = targetIsland.islandObject != null
            ? targetIsland.islandObject.GetComponent<InfluenceManager>()
            : null;

        if (influenceManager != null && !influenceManager.CanPlaceWarehouse(targetPosition, targetGrid))
        {
            failReason = "harbor placement rules not satisfied (needs a Beach cell or existing trade influence)";
            return false;
        }

        return true;
    }

    private bool HasRequiredResources()
    {
        if (requiredResources == null || requiredResources.Count == 0) return true;

        foreach (var requirement in requiredResources)
        {
            if (requirement == null || requirement.item == null) continue;

            if (!requirement.IsSatisfiedBy(GetAvailableQuantity(requirement.item)))
            {
                return false;
            }
        }

        return true;
    }

    private void DeductRequiredResources()
    {
        if (requiredResources == null) return;

        foreach (var requirement in requiredResources)
        {
            if (requirement == null || requirement.item == null || requirement.amount <= 0) continue;

            if (unitCargoInventory != null)
            {
                unitCargoInventory.RemoveItem(requirement.item, requirement.amount);
            }
            else if (unitInventory != null)
            {
                unitInventory.RemoveItem(requirement.item, requirement.amount);
            }
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
