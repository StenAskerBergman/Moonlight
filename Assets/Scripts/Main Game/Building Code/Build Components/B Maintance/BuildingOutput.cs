using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingOutput : MonoBehaviour
{
    private Building building;

    public Dictionary<ItemEnums.ResourceType, int> PendingOutput { get; private set; } = new Dictionary<ItemEnums.ResourceType, int>();

    public static event Action<Building, ItemEnums.ResourceType, int> OnOutputReady;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    public void AddOutput(ItemEnums.ResourceType resource, int amount)
    {
        if (amount <= 0) return;
        if (building == null || building.CurrentState != BuildingEnums.BuildingState.Active) return;

        if (PendingOutput.ContainsKey(resource))
        {
            PendingOutput[resource] += amount;
        }
        else
        {
            PendingOutput[resource] = amount;
        }

        OnOutputReady?.Invoke(building, resource, amount);
    }

    public Dictionary<ItemEnums.ResourceType, int> CollectOutput()
    {
        Dictionary<ItemEnums.ResourceType, int> collected = PendingOutput;
        PendingOutput = new Dictionary<ItemEnums.ResourceType, int>();
        return collected;
    }
}
