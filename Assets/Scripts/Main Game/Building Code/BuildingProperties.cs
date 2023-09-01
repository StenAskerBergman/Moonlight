using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BuildingProperties : MonoBehaviour
{    
    #region Building Variables
	// Island & Grid Refs
        public Island currentIsland;
		public GridSystem gridSystem;
    
    // Building Properties & Data
		public BuildingProperties buildingProperties;
        public BuildingData buildingData;

    // Building Settings & Variables
    public Vector3 buildingSize;
        public string[] buildingName;
        public string[] buildingDescription;
        public string[] buildingTags;

    // Building Model Components
        public Renderer buildingRenderer;

    #endregion



    // private enum Direction { North, East, South, West };
    public enum ConditionType
    {
        // Bool
        ConResourceExist,   // Condition Island Resource Exists 
        ConPower,

        // Ints
        ConResourceHas,     // Condition Island Resource Has X Amount
        ConEcologyPos,      // Condition Island Ecology is Positive X
        ConEcologyNeg,      // Condition Island Ecology is Negative X
        ConEcologyLvl,      // Condition Island Ecology is Level
        ConBuildings,       // Condition Island Buildings Within Range
        ConPowerLvl,
        ConSeed,
    }



    public Enums.RequirementType[] Requirements = new Enums.RequirementType[]
    {
            Enums.RequirementType.ReqShore,   // Requires Shoreline
            Enums.RequirementType.ReqSea,     // Requires Above at Sea
            Enums.RequirementType.ReqSub,     // Requires Above Submerged
            Enums.RequirementType.ReqLand,    // Requires Only Land
            Enums.RequirementType.ReqOther    // Requires Edge Cases
    };

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
        int[] costResources = buildingData.costResource;

        // Fetch Size Data
        Vector3 size = buildingData.buildingSize;

        // Fetch Requirement
        Enums.RequirementType[] requirements = buildingData.Requirements;
    }

    // Update the Initialize method to take currentIsland and gridSystem as parameters
    public void Initialize(Island island, GridSystem detectedGridSystem)
    {
        currentIsland = island;
        gridSystem = detectedGridSystem;
    }
}