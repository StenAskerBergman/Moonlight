using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    private Dictionary<Island, List<Building>> buildings = new Dictionary<Island, List<Building>>();


    public void AddBuilding(Island island, Building building)
    {
        if (!buildings.ContainsKey(island))
        {
            buildings[island] = new List<Building>();
        }

        buildings[island].Add(building);
        //CalculateMonthlyReturns(island);
    }

    // private void CalculateMonthlyReturns(Island island)
    // {
    //     int monthlyReturns = 0;
    //     foreach (Building building in buildings[island])
    //     {
    //         monthlyReturns += building.MonthlyReturns;
    //     }

    //     // Give monthly returns to player for this island
    //     ResourceManager.Instance.AddResourceToIsland(island.Id, Enums.Resource.Money, monthlyReturns);
    // }

    public void RemoveBuilding(Building building)
    {
        foreach (var kvp in buildings)
        {
            Island island = kvp.Key;
            List<Building> islandBuildings = kvp.Value;

            if (islandBuildings.Contains(building))
            {
                islandBuildings.Remove(building);
                //CalculateMonthlyReturns(island);
                break;
            }
        }
    }


    public int GetBuildingCount()
    {
        return buildings.Count;
    }
}