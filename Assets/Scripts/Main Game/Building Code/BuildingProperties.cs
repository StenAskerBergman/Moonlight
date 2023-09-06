using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BuildingProperties : MonoBehaviour
{    
    #region Building Variables

	// Island & Grid Refs
        public Island currentIsland;
		public GridSystem gridSystem;
    
    // Building Properties
		public BuildingProperties buildingProperties;
    
    // Building Data
        public BuildingData buildingData;
        public CostData costData;
        
    // Building Settings & Variables
        public Vector3 buildingSize;
        public string buildingName;
        public string buildingDescription;
        public string[] buildingTags;

    // Building Model Components
        //public Renderer buildingRenderer;

    // Ideas:
    // Make this a string[] and hold all the names for this building,
    // remember that different player see different names depending
    // on a lot of things! Being enemies, allies, under attack vice
    // versa. Therefore there needs to be a generative method to fix
    // the different names for the different views.
    
    #endregion


    // private 



    private void Start()
    {
        GetData();
        SetData();
    }

    public void GetData()
    {
        buildingProperties = GetComponent<BuildingProperties>();
        buildingProperties.buildingData = buildingData;
    }

    public void SetData()
    {
        buildingProperties = GetComponent<BuildingProperties>();
        buildingProperties.buildingData = buildingData;

        // Fetch Resource Cost Data
        int[] costResources = costData.costResource;

        // Fetch Size Data
        Vector3 size = buildingData.buildingSize;

        // Fetch Requirement
        RequirementEnums.RequirementType[] requirements = buildingData.Requirements;
    }

    // Update the Initialize method to take currentIsland and gridSystem as parameters
    public void Initialize(Island island, GridSystem detectedGridSystem)
    {
        currentIsland = island;
        gridSystem = detectedGridSystem;
    }
}