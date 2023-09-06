using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemEnums;

[CreateAssetMenu(fileName = "BuildingProductionData", menuName = "Building Production Data")]
public class BuildingProductionData : ScriptableObject
{
    public int ProductionRate;
    public int ProductionCapacity;
    public ResourceType ProducedResource;
    public int ProducedCapacity;

}