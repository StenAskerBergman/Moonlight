using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPop : MonoBehaviour
{
    #region Population Variables

    public int 
        nowPop, // Current Population in a Building
        maxPop, // Max Population in a Building
        minPop, // Min Population in a Building
        allPop; // All Pop of a Certain Class

    public int
        TotalPopulation,        // Total Player Population

        // Faction Tycoons
        WorkerPopulation,       // Total Player Worker Population
        EmployeePopulation,     // Total Player Employee Population
                                
        // Faction Neutral 
        Tier1Population,        // Total Player Worker Population 
        Tier2Population,        // Total Player Employee Population 
        Tier3Population,        // Total Player Worker Population 
        Tier4Population,        // Total Player Worker Population 
        Tier5Population;        // Total Player Worker Population 


    public int 
        WorkerPopularity,       // Current Worker Popularity Level  
        EmployeePopularity,     // Current Employee Popularity Level
        ExecuativePopularity;   // Current Executive Popularity Level


    #endregion

    #region Population Methods

    #region Start + Update Methods

    #region Start Method
    // Start is called before the first frame update
    void Start()
    {
        
    }
    #endregion

    #region Update Method
    // Update is called once per frame
    void Update()
    {
        
    }
    #endregion

    #endregion

    #endregion
}
