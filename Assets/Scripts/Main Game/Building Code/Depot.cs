// Depo
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Depot : MonoBehaviour
{
    [SerializeField] private BuildingInventoryData buildingInvData;
    private BuildingInventoryBehaviour behaviour;

    private void Awake()
    {
        if (GetComponent<WarehouseLogisticsScheduler>() == null)
        {
            gameObject.AddComponent<WarehouseLogisticsScheduler>();
        }
    }

    private void Start()
    {
        // Without this guard an unassigned inventory asset throws on the first frame,
        // which took down every other Start on the building with it.
        if (buildingInvData == null)
        {
            Debug.LogWarning(
                $"Depot on '{name}' has no BuildingInventoryData assigned, so its inventory " +
                "behaviour is inactive. Warehouse logistics still run via WarehouseLogisticsScheduler.",
                this);
            return;
        }

        // Check building type and initialize behavior accordingly
        switch (buildingInvData.CurrentInventoryType)
        {
            case InventoryType.Depot:
                behaviour = new DepotInventoryBehaviour(buildingInvData.IslandInventoryReference);
                break;
            case InventoryType.Consumer: // Assuming Harbour is the "Consumer" in this case
                behaviour = new HarbourInventoryBehaviour(buildingInvData.IslandInventoryReference);
                break;
            // Add other cases if necessary...
        }
    }

    /// <summary>
    /// The island stockpile this depot deposits into. Falls back to the island's own
    /// Inventory when no BuildingInventoryData is assigned - the depot is the island's
    /// warehouse either way, and returning null here used to make every delivery
    /// silently vanish.
    /// </summary>
    public Inventory IslandInventory
    {
        get
        {
            Island island = GetComponentInParent<Island>();
            return island != null ? island.GetComponent<Inventory>() : null;
        }
    }

    /// <summary>
    /// Whether this depot can currently take <paramref name="amount"/> of an item.
    /// </summary>
    public bool CanAccept(ItemData itemData, int amount)
    {
        if (itemData == null || amount <= 0) return false;

        Inventory target = IslandInventory;
        return target != null && target.CanAdd(itemData, amount);
    }

    /// <summary>
    /// Deposits into the island stockpile. Returns false when there was nowhere to put
    /// it, so callers can keep the cargo rather than destroying it.
    /// </summary>
    public bool InteractWithInventory(ItemData itemData, int amount)
    {
        if (itemData == null || amount <= 0) return false;

        // Preserve the configured behaviour when one exists...
        if (behaviour != null)
        {
            behaviour.AddItem(itemData, amount);
            return true;
        }

        // ...otherwise deposit straight into the island stockpile.
        Inventory target = IslandInventory;
        if (target == null)
        {
            Debug.LogWarning($"Depot on '{name}' has no island Inventory to deposit into.", this);
            return false;
        }

        if (!target.CanAdd(itemData, amount)) return false;

        target.AddItem(itemData, amount);
        return true;
    }
}
