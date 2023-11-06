using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemEnums;

[CreateAssetMenu(fileName = "New Building Production Data", menuName = "Data/Building/Building Production Data")]
public class BuildingProductionData : ScriptableObject
{
    public int ProductionRate;
    public int ProductionCapacity;
    public ResourceType ProducedResource;
    public int ProducedCapacity;

}