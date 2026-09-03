using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSupply : MonoBehaviour
{
    private Building building;

    // TODO: assign in the Inspector per building prefab - the resources this
    // building needs on hand to keep operating.
    [SerializeField] private List<ResourceRequirement> requiredSupplies = new List<ResourceRequirement>();

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    public bool HasRequiredSupplies()
    {
        if (requiredSupplies == null || requiredSupplies.Count == 0) return true;
        if (building == null || building.buildingInventory == null) return false;

        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (requirement == null) continue;

            if (!TryResolveRequirement(requirement, out ItemEnums.ResourceType resourceType, out _)) return false;

            int available = building.buildingInventory.GetResourceCount(resourceType);
            if (!requirement.IsSatisfiedBy(available))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryGetNextDeliveryRequest(int capacity, out ItemEnums.ResourceType resource, out int amount)
    {
        resource = default;
        amount = 0;
        if (building == null || building.buildingInventory == null || requiredSupplies == null) return false;
        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (requirement == null || requirement.amount <= 0 ||
                !TryResolveRequirement(requirement, out ItemEnums.ResourceType parsed, out _)) continue;
            int onHand = building.buildingInventory.GetResourceCount(parsed);
            int target = Mathf.Max(requirement.amount * 3, requirement.amount);
            int missing = Mathf.Min(target - onHand, building.buildingInventory.FreeCapacity);
            if (missing <= 0) continue;
            resource = parsed;
            amount = Mathf.Min(missing, Mathf.Max(1, capacity));
            return true;
        }
        return false;
    }

    public int ReceiveSupply(ItemEnums.ResourceType resource, int amount)
    {
        return building != null && building.buildingInventory != null
            ? building.buildingInventory.AddResourceToBuilding(resource, amount)
            : 0;
    }

    public void ConsumeSupplies()
    {
        if (requiredSupplies == null) return;
        if (building == null || building.buildingInventory == null) return;

        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (requirement == null || requirement.amount <= 0) continue;

            if (!TryResolveRequirement(requirement, out ItemEnums.ResourceType resourceType, out _)) continue;

            building.buildingInventory.TryRemoveResource(resourceType, requirement.amount);
        }
    }

    // Called from BuildingCondition's periodic check.
    public void CheckSupplyState()
    {
        if (building == null) return;

        if (!HasRequiredSupplies())
        {
            building.SetState(BuildingEnums.BuildingState.Paused);
        }
        else if (building.CurrentState == BuildingEnums.BuildingState.Paused)
        {
            building.SetState(BuildingEnums.BuildingState.Active);
        }
    }

    public ItemData GetItemDefinition(ItemEnums.ResourceType resource)
    {
        if (requiredSupplies == null) return null;
        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (TryResolveRequirement(requirement, out ItemEnums.ResourceType parsed, out ItemData item) &&
                parsed == resource)
            {
                return item;
            }
        }
        return null;
    }

    private bool TryResolveRequirement(
        ResourceRequirement requirement,
        out ItemEnums.ResourceType resource,
        out ItemData item)
    {
        resource = ItemEnums.ResourceType.None;
        item = requirement != null ? requirement.item : null;
        if (requirement == null) return false;

        if (item != null && item.HasResourceType)
        {
            resource = item.ResourceType;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(requirement.requiredResource) &&
            System.Enum.TryParse(requirement.requiredResource, true, out resource) &&
            resource != ItemEnums.ResourceType.None)
        {
            return true;
        }

        Debug.LogWarning($"{name}: supply requirement needs an ItemData with a ResourceType mapping.", this);
        return false;
    }
}
