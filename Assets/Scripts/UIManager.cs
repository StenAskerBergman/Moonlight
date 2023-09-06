using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerEnter == BalanceText.gameObject)
        {
            ShowBankInfoPanel();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerEnter == BalanceText.gameObject)
        {
            HideBankInfoPanel();
        }
    }

    [Header("UI Header")]
    
    // Player Set Variables

        [Space(10)]
        public Text islandNameText;
        public bool IslandSettled = false;

    // Power Related

        [Header("Power Related")]
        public Text CurrentPowerText;
        
        public Text MadePowerText;
        public Text TotalPowerText;
        public Text SpentPowerText;
        
        
    // Eco Related

        [Header("Eco Related")]
        [Space(8)]
        public Text EcoValueText;
        public Text EcoPosText;
        public Text EcoNegText;  

    // Resource Related

        [Header("Resource Related")]
        [Space(8)]
        // Tier 1 
        public Text resource1Text; // Gravel
        public Text resource2Text; // Coal
        public Text resource3Text; // Iron
        // Tier 2
        public Text resource4Text; // Sand
        public Text resource5Text; // Copper
        public Text resource6Text; // Oil
        // Tier 3
        public Text resource7Text; // Uranium
        public Text resource8Text; // Gold
        public Text resource9Text; // Unknown

    // Material Related

        [Header("Default Materials")]
        [Space(8)]
        
        // Default Materials:
        public Text material1Text; // Building Moduls
        public Text material2Text; // Building Tools

        // Faction Materials
        [Header("Main Faction Materials")]
        [Space(8)]

        // Tier 2 Faction Naterials
        public Text material3Text; // Basic Materials: z / x
        public Text material4Text; // Basic Materials: z / x
        
        // Tier 3 Faction Materials
        public Text material5Text; // Advance Faction Materials: z / x
        public Text material6Text; // Advance Faction Materials: z / x 

        // Science Materials
        [Header("Science Materials")]
        [Space(8)]

        // Tier 4 Science Material 
        public Text material7Text; // Super Materials - Carbon Fiber
        public Text material8Text; // Super Materials - Protype Material 
        public Text material9Text; // Ultra Materials - Kerosine



    // Good Related
    /*
        [Header("Default Goods")]
        [Space(8)]
        public Text good1Text;
        public Text good2Text;
        public Text good3Text;
    */

    // Bank Related

        [Header("Bank Related")]
        [Space(8)]
        public Bank bank; 
        public GameObject BankInfoPanel;

        [Space(8)]
        public Text BalanceText;    // Current Balance from Total Economic Sum 
        public Text SavingsText;    // Current Savings from Paused Facilities 
        public Text TradeText;      // Current # of Trade Earnings by a Route
        public Text TaxText;        // Current # of Tax Profits by Island 
        public Text IncomeText;     // Current # of Income
        public Text ExpenseText;    // Current # of Expenses
        public Text RevenueText;    // Current Profits Numbers

    [Space(8)]
        public Text LicenseText; // Current # of Licences


    // Refs 1: 
        [Header("Island Related")]
        [Space(8)]
        public IslandItems islandItems;
        public IslandPower islandPower;
        public IslandEcology islandEcology;


    // Refs 2
        public PlayerFactionController playerFactionController;
        private PlayerMaterialManager playerMaterialManager;
        private IslandItemManager islandItemManager;
        private Island currentIsland;
        
    private void Start()
    {
        // Locates PlayerFactionController
        playerFactionController = FindObjectOfType<PlayerFactionController>();

        // Subscribe to event for the current island.
        playerMaterialManager = FindObjectOfType<PlayerMaterialManager>();
        IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;
        bank.OnBankValueChanged -= UpdateBankUI; 

        // Initially hide the bank info panel
        BankInfoPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Unsubscribes on Destruction
        IslandManager.instance.OnPlayerHoverIsland -= OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;
        bank.OnBankValueChanged -= UpdateBankUI; 
    }

    private void OnCurrentIslandChanged(Island island)
    {
        
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }
        currentIsland = island;
        islandItems = island.GetComponent<IslandItems>();
        islandPower = island.GetComponent<IslandPower>();
        islandEcology = island.GetComponent<IslandEcology>();
        islandNameText.text = island.name.ToString();
        islandItemManager = island.GetComponent<IslandItemManager>(); 
    }
    
    public void UpdateBankUI()
    {
        BalanceText.text = " € " + bank.GetRevenue();   // Balance
        SavingsText.text = " € " + bank.GetBalance();   // Total Sum 
        IncomeText.text = " € " + bank.GetIncome();     // Income  
        ExpenseText.text = " € " + bank.GetExpense();   // Expense
        RevenueText.text = " € " + bank.GetRevenue();   // Revenue
        LicenseText.text = " £ " + bank.GetLicense();   // License
    }


    // Update is called once per frame
    void Update()
    {
        UpdateBankUI();

        if (currentIsland != null) 
        {
            if (islandItems == null)
            {
                Debug.Log("islandResource = Null");
                return;
            }
            else
            {

                // Display the amount of each resources in the UI
                resource1Text.text = "" + islandItems.GetResourceAmount(ItemEnums.ResourceType.Resource1); // Selects a Resource of type #
                resource2Text.text = "" + islandItems.GetResourceAmount(ItemEnums.ResourceType.Resource2); // Sends the Resource# Amounts back
                resource3Text.text = "" + islandItems.GetResourceAmount(ItemEnums.ResourceType.Resource3); // Displays the Resource# Amount

                // Display the amount of each materials in the UI - Default
                material1Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material0); // Building Moduls
                material2Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material1); // Building Tools


                // Get the current faction.
                Enums.Faction currentFaction = playerFactionController.GetCurrentFactionDisplay();


                // Display foreach faction material & goods the amount stored in island
                // On to the player UI based of the display active faction for the player
                switch (currentFaction)
                {
                    case Enums.Faction.Tyc:
                    // Show Tycoon Materials
                        
                        // Tier 1
                        material3Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material2); // Displays the Tyc Material3 Amount
                        material4Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material3); // Displays the Tyc Material4 Amount
                        // Tier 2
                        material5Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material4); // Displays the Tyc Material5 Amount
                        material6Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material5); // Displays the Tyc Material6 Amount

                    // Show Tycoon Goods
                        
                        //// Tier 1
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good1); // Selects a Resource of type #
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good2); // Selects a Resource of type #

                        //// Tier 1
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good3); // Sends the Resource# Amounts back
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good4); // Sends the Resource# Amounts back

                        //// Tier 2
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good5); // Displays the Resource# Amount
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.T_Good6); // Displays the Resource# Amount

                    break;

                    case Enums.Faction.Eco:
                    // Show the Eco Materials
                        
                        // Tier 1
                        material3Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material6); // Displays the Eco Material3 Amount
                        material4Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material7); // Displays the Eco Material4 Amount
                        // Tier 2
                        material5Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material8); // Displays the Eco Material5 Amount
                        material6Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material9); // Displays the Eco Material6 Amount

                    // Show Tycoon Goods

                        //// Tier 1
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good1); // Selects a Resource of type #
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good2); // Selects a Resource of type #

                        //// Tier 1
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good3); // Sends the Resource# Amounts back
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good4); // Sends the Resource# Amounts back

                        //// Tier 2
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good5); // Displays the Resource# Amount
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.E_Good6); // Displays the Resource# Amount

                    break;

                    case Enums.Faction.Sci:
                    // Show the Sci Materials

                        // Tier 1
                        material7Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material10); // Displays the Sci Material7 Amount
                        // Tier 2
                        material8Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material11); // Displays the Sci Material8 Amount
                        // Tier 3
                        material9Text.text = "" + islandItems.GetMaterialAmount(ItemEnums.MaterialType.Material12); // Displays the Sci Material9 Amount

                    // Show Tycoon Goods

                        //// Tier 1
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good1); // Selects a Resource of type #
                        //good1Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good2); // Selects a Resource of type #

                        //// Tier 1
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good3); // Sends the Resource# Amounts back
                        //good2Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good4); // Sends the Resource# Amounts back

                        //// Tier 2
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good5); // Displays the Resource# Amount
                        //good3Text.text = "" + islandItems.GetGoodAmount(ItemEnums.GoodType.S_Good6); // Displays the Resource# Amount

                    break;

                    default:
                    // No faction is selected.

                    break;
                }
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
                    MadePowerText.text = ""+islandPower.GetMadePower();         // Total Power
                }
            }

            if (islandEcology == null)
            {
                Debug.Log("islandEcology = Null");
                return;

            } else {
                // Display the amount of Power in the UI
                EcoValueText.text = ""+islandEcology.GetCurrentEco();    // Current Eco Value
                EcoPosText.text = ""+islandEcology.GetNegativeEco();     // Negative Eco Value
                EcoNegText.text = ""+islandEcology.GetPositiveEco();     // Positive Eco Value

            }
        }
    }

    // Methods to show and hide the bank info panel
    public void ShowBankInfoPanel()
    {
        BankInfoPanel.SetActive(true);
    }

    public void HideBankInfoPanel()
    {
        BankInfoPanel.SetActive(false);
    }
}
