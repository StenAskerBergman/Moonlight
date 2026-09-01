using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WarehouseInteractionUI : MonoBehaviour
{
    public static WarehouseInteractionUI Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject warehousePanel;
    public Transform goodsContentParent; // Where ItemSlots or text for goods go
    public Transform tradeConfigPanel;   // The sub-panel for configuring a specific good

    [Header("Trade Config UI")]
    public Text selectedItemNameText;
    public Dropdown tradeModeDropdown;   // None, Buy, Sell
    public Slider targetStockSlider;
    public Text targetStockText;
    
    private Island currentIsland;
    private Inventory currentInventory;
    private IslandTradeRules currentTradeRules;
    private ItemData currentlyConfiguredItem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        if (warehousePanel != null)
        {
            warehousePanel.SetActive(false);
        }

        // Hook up UI events
        if (tradeModeDropdown != null)
        {
            tradeModeDropdown.onValueChanged.AddListener(OnTradeModeChanged);
        }
        if (targetStockSlider != null)
        {
            targetStockSlider.onValueChanged.AddListener(OnStockSliderChanged);
        }
    }

    private void Start()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnBuildingSelected);
        }
    }

    private void OnDestroy()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnBuildingSelected);
        }
    }

    private void OnBuildingSelected(Building building)
    {
        if (building == null)
        {
            ClosePanel();
            return;
        }

        // Check if building is a Warehouse/Depot
        Depot depot = building.GetComponent<Depot>();
        WarehouseSockets sockets = building.GetComponent<WarehouseSockets>();
        if (depot != null || sockets != null) 
        {
            // Find the Island
            currentIsland = building.GetComponentInParent<Island>();
            if (currentIsland != null)
            {
                currentInventory = currentIsland.GetComponent<Inventory>();
                currentTradeRules = currentIsland.TradeRules;
                OpenPanel();
            }
        }
        else
        {
            ClosePanel();
        }
    }

    public void OpenPanel()
    {
        if (warehousePanel != null) warehousePanel.SetActive(true);
        RefreshDisplay();
    }

    public void ClosePanel()
    {
        if (warehousePanel != null) warehousePanel.SetActive(false);
        currentIsland = null;
        currentInventory = null;
        currentTradeRules = null;
        currentlyConfiguredItem = null;
        if (tradeConfigPanel != null) tradeConfigPanel.gameObject.SetActive(false);
    }

    public void RefreshDisplay()
    {
        if (currentInventory == null) return;
        // In a real implementation, we would instantiate UI Prefabs for each item in currentInventory.GetAllItems()
        // For now, this is a controller stub ready to be hooked up to the Canvas.
    }

    // Called when a user clicks on a specific good in the warehouse panel
    public void SelectItemForConfig(ItemData itemData)
    {
        currentlyConfiguredItem = itemData;
        
        if (currentTradeRules != null && tradeConfigPanel != null)
        {
            tradeConfigPanel.gameObject.SetActive(true);
            if (selectedItemNameText != null) selectedItemNameText.text = itemData.displayName;

            TradeRule rule = currentTradeRules.GetRule(itemData);
            
            // Sync UI state
            if (tradeModeDropdown != null)
            {
                tradeModeDropdown.value = (int)rule.RuleType;
            }
            if (targetStockSlider != null)
            {
                targetStockSlider.value = rule.RuleType == TradeRuleType.PassiveBuy ? rule.TargetStock : rule.MinStockToRetain;
            }
            UpdateStockText();
        }
    }

    private void OnTradeModeChanged(int modeIndex)
    {
        if (currentlyConfiguredItem == null || currentTradeRules == null) return;

        TradeRuleType newMode = (TradeRuleType)modeIndex;
        currentTradeRules.SetRuleType(currentlyConfiguredItem, newMode);
        UpdateStockText();
    }

    private void OnStockSliderChanged(float value)
    {
        if (currentlyConfiguredItem == null || currentTradeRules == null) return;

        int amount = Mathf.RoundToInt(value);
        TradeRule rule = currentTradeRules.GetRule(currentlyConfiguredItem);

        if (rule.RuleType == TradeRuleType.PassiveBuy)
        {
            currentTradeRules.SetTargetStock(currentlyConfiguredItem, amount);
        }
        else if (rule.RuleType == TradeRuleType.PassiveSell)
        {
            currentTradeRules.SetMinStockToRetain(currentlyConfiguredItem, amount);
        }
        UpdateStockText();
    }

    private void UpdateStockText()
    {
        if (currentlyConfiguredItem == null || currentTradeRules == null) return;

        TradeRule rule = currentTradeRules.GetRule(currentlyConfiguredItem);
        if (targetStockText != null)
        {
            if (rule.RuleType == TradeRuleType.PassiveBuy)
            {
                targetStockText.text = $"Buy up to: {rule.TargetStock}";
            }
            else if (rule.RuleType == TradeRuleType.PassiveSell)
            {
                targetStockText.text = $"Retain min: {rule.MinStockToRetain}";
            }
            else
            {
                targetStockText.text = "No active trade rule";
            }
        }
    }
}
