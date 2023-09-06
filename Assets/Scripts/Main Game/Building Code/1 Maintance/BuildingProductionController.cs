using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemEnums;

public class BuildingProductionController : MonoBehaviour, IBuildingProduction
{
    private BuildingProductionData _data;

    public int GetProductionRate()
    {
        return _data.ProductionRate;
    }

    public void SetProductionRate(int rate)
    {
        _data.ProductionRate = rate;
    }

    public int GetProductionCapacity()
    {
        return _data.ProductionCapacity;
    }

    public void SetProductionCapacity(int capacity)
    {
        _data.ProductionCapacity = capacity;
    }

    public ResourceType GetProducedResource()
    {
        return _data.ProducedResource;
    }

    public void SetProducedResource(ResourceType resource)
    {
        _data.ProducedResource = resource;
    }
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
