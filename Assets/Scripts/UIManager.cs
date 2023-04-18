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
        public Text resource1Text;
        public Text resource2Text;
        public Text resource3Text;
                    

    // Bank Related
    
        [Header("Bank Related")]
        [Space(8)]
        public Bank bank; 
        public GameObject BankInfoPanel;

        [Space(8)]
        public Text BalanceText; 
        public Text SavingsText;
        public Text IncomeText;
        public Text ExpenseText;
        public Text RevenueText;

        [Space(8)]
        public Text LicenseText;


    // Refs 1: 
        [Header("Island Related")]
        [Space(8)]
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
        bank.OnBankValueChanged -= UpdateBankUI; // Unsubscribe from the
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
    
    public void UpdateBankUI()
    {
        BalanceText.text = " € " + bank.GetRevenue(); // Balance
        SavingsText.text = " € " + bank.GetBalance(); // Total Sum 
        IncomeText.text = " € " + bank.GetIncome(); // Income  
        ExpenseText.text = " € " + bank.GetExpense(); // Expense
        RevenueText.text = " € " + bank.GetRevenue(); // Revenue
        LicenseText.text = " £ " + bank.GetLicense(); // License
    }


    // Update is called once per frame
    void Update()
    {
        UpdateBankUI();

        if (currentIsland != null) 
        {
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
                    MadePowerText.text = ""+islandPower.GetMadePower();       // Total Power
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
