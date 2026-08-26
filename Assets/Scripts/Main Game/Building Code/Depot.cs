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
        // For example, to add items:
        behaviour.AddItem(itemData, amount);
    }
}
