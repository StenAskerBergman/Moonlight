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

    // future issue:
    // There also needs to be a way for preview buildings not to be
    // viewed by other players

    // future feature:
    // we need a way to control the way we show + hold building data,
    // from players in the future in each building to be read so that
    // it is read correctly but also so that depending on each player
    // and their intelligence levels / insight into a another player,
    // is show the correct data, so that in the future players cannot
    // do object manipulate to see this data inorder to cheat / edge.
    // 
    // Real Reason: plus it would be cool to have. :spy!: :poggers:

    #endregion

    private void Start()
    {
        SetData();
    }

    public void SetData()
    {
        /// Edit: In the future include, what replacement was added in its place!
        // Legacy Code 
        // Fetch Resource Cost Data
        // int[] costResources = costData.costResource;

        // Fetch Size Data. A missing BuildingData asset is normal - the Depot has none -
        // and simply means there is nothing to override the size authored on the prefab.
        // Reading through it unguarded threw in Start() on every building placed from
        // such a prefab.
        if (buildingData != null)
        {
            buildingSize = buildingData.buildingSize;
        }
    }


    // Update the Initialize method to take currentIsland and gridSystem as parameters
    public void Initialize(Island island, GridSystem detectedGridSystem)
    {
        currentIsland = island;
        gridSystem = detectedGridSystem;
    }
}