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
    [SerializeField, Min(1)] private int fallbackProductionRate = 1;
    [SerializeField, Min(0.1f)] private float productionIntervalSeconds = 2f;

    private Building building;
    private BuildingOutput output;
    private float nextProductionTime;

    private void Awake()
    {
        building = GetComponent<Building>();
        output = GetComponent<BuildingOutput>();
        if (output == null) output = gameObject.AddComponent<BuildingOutput>();
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
        return _data != null ? _data.ProductionRate : fallbackProductionRate;
    }

    public void SetProductionRate(int rate)
    {
        if (_data != null) _data.ProductionRate = rate;
        else fallbackProductionRate = Mathf.Max(1, rate);
    }

    public int GetProductionCapacity()
    {
        return _data != null ? _data.ProductionCapacity : output != null ? output.OutputCapacity : 30;
    }

    public void SetProductionCapacity(int capacity)
    {
        if (_data != null) _data.ProductionCapacity = capacity;
    }

    public ResourceType GetProducedResource()
    {
        return _data != null ? _data.ProducedResource : fallbackResource;
    }

    public void SetProducedResource(ResourceType resource)
    {
        if (_data != null) _data.ProducedResource = resource;
        else fallbackResource = resource;
    }

    #endregion

}

// Really Old Way
//private float lastDeliveryTime;
//public float deliveryInterval = 1.0f;
//public float resetInterval = 60.0f;

//private void DeliveryInterval()
//{
//	float elapsedTime = Time.time - lastDeliveryTime;

//	if (elapsedTime > deliveryInterval)
//	{
//		int deliveries = Mathf.FloorToInt(elapsedTime / deliveryInterval);

//		for (int i = 0; i < deliveries; i++)
//		{
//			DeliverChunk();
//		}

//		lastDeliveryTime += deliveries * deliveryInterval;
//	}

//	if (Time.time > lastDeliveryTime + resetInterval)
//	{
//		lastDeliveryTime = Time.time % resetInterval;
//	}
//}


//   private void DeliverChunk()
//   {
//	// Legacy Model
//       //Bank.BM = Bank.BM + 20; // Add 20 units of building material to the player's bank
//   }

//   void FixedUpdate()
//   {
//       DeliveryInterval(); // Call the DeliveryInterval() method every physics frame
//   }
