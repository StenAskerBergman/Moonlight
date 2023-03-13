using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // Player Set Variables
        public Text islandNameText;
        public bool IslandSettled = false;

    // Power Related
        public Text CurrentPowerText;
        public Text TotalPowerText;
        public Text SpentPowerText;

    // Eco Related
        public Text EcoValueText;
        public Text EcoPosText;
        public Text EcoNegText;

    // Money Related
        public Text MoneyText;
    
    // Resource Related
        public Text resource1Text;
        public Text resource2Text;
        public Text resource3Text;
        
    // Refs 1
        public IslandResource islandResource;
        public IslandPower islandPower;
        public IslandEcology islandEcology;

    // Refs 2
        private PlayerMaterialManager playerMaterialManager;
        private IslandResourceManager islandResourceManager;
        private Island currentIsland;
        
    private void Start()
    {
        // Subscribe to event for the current island.
        playerMaterialManager = FindObjectOfType<PlayerMaterialManager>();
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;
    }

    private void OnDestroy()
    {
        // Unsubscribes on Destruction
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;

    }

    private void OnCurrentIslandChanged(Island island)
    {
        
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }
        currentIsland = island;
        islandResource = island.GetComponent<IslandResource>();
        islandPower = island.GetComponent<IslandPower>();
        islandEcology = island.GetComponent<IslandEcology>();
        islandNameText.text = island.name.ToString();
        islandResourceManager = island.GetComponent<IslandResourceManager>(); 
    }
    
    // Update is called once per frame
    void Update()
    {
        if (currentIsland != null) {
            islandResource = currentIsland.GetComponent<IslandResource>();
            islandPower = currentIsland.GetComponent<IslandPower>();
            islandEcology = currentIsland.GetComponent<IslandEcology>();
            islandNameText.text = currentIsland.name.ToString();
            islandResourceManager = currentIsland.GetComponent<IslandResourceManager>(); 
            
            if (islandResource == null)
            {
                Debug.Log("islandResource = Null");
                return;
            } else {

                // Display the amount of each resource in the UI
                resource1Text.text = ""+islandResource.GetResourceAmount(Enums.Resource.Resource1); // Selects a Resource of type #
                resource2Text.text = ""+islandResource.GetResourceAmount(Enums.Resource.Resource2); // Sends the Resource# Amounts back
                resource3Text.text = ""+islandResource.GetResourceAmount(Enums.Resource.Resource3); // Displays the Resource# Amount
            }

            

                if (islandPower == null)
                {
                    Debug.Log("islandPower = Null");
                    IslandSettled = false;
                    return;

                } else {
                    
                    if(islandPower.Settled == true) 
                    {
                        IslandSettled = islandPower.Settled;
                        // Display the amount of Power in the UI
                        CurrentPowerText.text = ""+islandPower.GetCurrentPower();   // Current Power
                        SpentPowerText.text = ""+islandPower.GetPowerSpent();       // Spent Power
                        TotalPowerText.text = ""+islandPower.GetTotalPower();       // Total Power
                    }
                }

            if (islandEcology == null)
            {
                Debug.Log("islandEcology = Null");
                return;

            } else {
                // Display the amount of Power in the UI
                EcoValueText.text = ""+islandEcology.GetCurrentEco();       // Current Eco Value
                EcoPosText.text = ""+islandEcology.GetNegativeEco();        // Negative Eco Value
                EcoNegText.text = ""+islandEcology.GetPositiveEco();        // Positive Eco Value

            }
        }
    }
}
