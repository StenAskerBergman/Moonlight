using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Enums;

public interface IBuildingProduction
{
    int GetProductionRate();
    void SetProductionRate(int rate);
    int GetProductionCapacity();
    void SetProductionCapacity(int capacity);
    Resource GetProducedResource();
    void SetProducedResource(Resource resource);
}