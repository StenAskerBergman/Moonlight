using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemEnums;

public class BuildingProductionController : MonoBehaviour, IBuildingProduction
{
    #region Default Production

    [SerializeField] private BuildingProductionData _data;
    [Header("No-input fallback production")]
    [SerializeField] private ResourceType fallbackResource = ResourceType.RawGravel;
    [SerializeField] private ItemData fallbackItemData;
    [SerializeField, Min(1)] private int fallbackProductionRate = 1;
    [SerializeField, Min(0.1f)] private float productionIntervalSeconds = 2f;

    private Building building;
    private BuildingOutput output;
    private float nextProductionTime;

    // Per-instance runtime overrides prevent mutating shared BuildingProductionData SO assets.
    private int? runtimeProductionRate;
    private int? runtimeProductionCapacity;
    private ResourceType? runtimeProducedResource;

    private void Awake()
    {
        building = GetComponent<Building>();
        output = GetComponent<BuildingOutput>();
        if (output == null) output = gameObject.AddComponent<BuildingOutput>();
        output.RegisterItemDefinition(GetProducedResource(), GetProducedItemData());
    }

    public float ProductionIntervalSeconds => productionIntervalSeconds;
    public float NextProductionTime => nextProductionTime;
    public float CycleProgress
    {
        get
        {
            if (building == null || building.CurrentState != BuildingEnums.BuildingState.Active) return 0f;
            if (output != null && output.IsFull) return 0f;
            BuildingSupply supply = GetComponent<BuildingSupply>();
            if (supply != null && !supply.HasRequiredSupplies()) return 0f;

            if (productionIntervalSeconds <= 0f) return 1f;
            float remaining = nextProductionTime - Time.time;
            if (remaining <= 0f) return 1f;
            return Mathf.Clamp01(1f - (remaining / productionIntervalSeconds));
        }
    }

    private void Update()
    {
        if (building == null || building.CurrentState != BuildingEnums.BuildingState.Active) return;
        if (Time.time < nextProductionTime) return;
        nextProductionTime = Time.time + productionIntervalSeconds;
        BuildingSupply supply = GetComponent<BuildingSupply>();
        if (supply != null && !supply.HasRequiredSupplies()) return;
        if (output.IsFull) return;
        supply?.ConsumeSupplies();
        output.AddOutput(GetProducedResource(), GetProductionRate());
    }

    public int GetProductionRate()
    {
        if (runtimeProductionRate.HasValue) return runtimeProductionRate.Value;
        return _data != null ? _data.ProductionRate : fallbackProductionRate;
    }

    public void SetProductionRate(int rate)
    {
        runtimeProductionRate = Mathf.Max(1, rate);
    }

    public int GetProductionCapacity()
    {
        if (runtimeProductionCapacity.HasValue) return runtimeProductionCapacity.Value;
        return _data != null ? _data.ProductionCapacity : output != null ? output.OutputCapacity : 30;
    }

    public void SetProductionCapacity(int capacity)
    {
        runtimeProductionCapacity = Mathf.Max(1, capacity);
    }

    public ResourceType GetProducedResource()
    {
        if (runtimeProducedResource.HasValue) return runtimeProducedResource.Value;
        return _data != null ? _data.ProducedResource : fallbackResource;
    }

    public ItemData GetProducedItemData()
    {
        return _data != null && _data.ProducedItemData != null ? _data.ProducedItemData : fallbackItemData;
    }

    public void SetProducedResource(ResourceType resource)
    {
        runtimeProducedResource = resource;
    }

    #endregion
}
