using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemEnums;

public interface IBuildingProduction
{
    int GetProductionRate();
    void SetProductionRate(int rate);
    int GetProductionCapacity();
    void SetProductionCapacity(int capacity);
    ResourceType GetProducedResource();
    void SetProducedResource(ResourceType resource);
}