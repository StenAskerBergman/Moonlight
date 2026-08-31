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

    public void InteractWithInventory(ItemData itemData, int amount)
    {
        if (behaviour == null) return;
        behaviour.AddItem(itemData, amount);
    }
}
