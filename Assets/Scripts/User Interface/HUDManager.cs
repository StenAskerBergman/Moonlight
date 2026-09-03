using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HUDManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    #region Hover Section ... 

        [Tooltip("Hover Panel Target Trigger to be enabled / disabled.")]
        public GameObject InfoPanel;

        [Tooltip("Target Trigger for enabling / disabling the Panel.")]
        public Text TargetText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TargetText == null) return;
            // Check if the pointer is entering the bank info panel
            if (eventData.pointerEnter == TargetText.gameObject)
            {
                ShowInfoPanel();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TargetText == null) return;
            // Check if the pointer is exiting the bank info panel
            if (eventData.pointerEnter == TargetText.gameObject)
            {
                HideInfoPanel();
            }
        }

        #region  Method: Show / Hide

        // Info Panel

        // Methods to show and hide the info panel
        public void ShowInfoPanel()
        {
            InfoPanel.SetActive(true);
        }

        public void HideInfoPanel()
        {
            InfoPanel.SetActive(false);
        }

        #endregion


    #endregion

    #region Variables ... 
    
    // Inspector Interface 
    [Header("UI Header")]
    
    // Player Set Variables
        [Space(10)]
        public Text islandNameText;
        public bool IslandSettled = false;

    // Resource Related
        [Header("Resource Related")]
        [Space(8)]
        public GameObject ItemInfoPanel;
        public Text[] ResourceTexts;

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
        public Text material3Text, material4Text; // Basic Materials: z / x
        
        // Tier 3 Faction Materials
        public Text material5Text, material6Text; // Advance Faction Materials: z / x 

        // Science Materials
        [Header("Science Materials")]
        [Space(8)]

        // Tier 4 Science Material 
        public Text material7Text; // Super Materials - Carbon Fiber
        public Text material9Text; // Ultra Materials - Kerosene

       // Future Ideas
       // public Text SavingsText;    // Current Savings from Paused Facilities 
       // public Text TradeText;      // Current # of Trade Earnings by a Route

    [Space(8)]
        public Text LicenseText; // Current # of Licences

    // Refs 1
        public PlayerFactionController playerFactionController;
        private Inventory playerInventory;
        private Inventory islandInventory;
        private Island currentIsland;

    // Resource Items
        [Header("Resource Items")]
        public ItemData 
            gravelData, coalData, ironData, sandData, copperData, oilData, uraniumData, goldData;

    // Deposit Slots GameObjects
        [Tooltip("GameObject Ref")]
        public GameObject
            gravelSlot, coalSlot, ironSlot, sandSlot, copperSlot, oilSlot, uraniumSlot, goldSlot;

    // Deposit Slots Sliders
        [Tooltip("Slider Ref")]
        public Slider
            gravelSlider, coalSlider, ironSlider, sandSlider, copperSlider, oilSlider, uraniumSlider, goldSlider;

    // Text For Panel
        [Tooltip("Text For Panel")]
        public Text
            gravelAmountPanelText, coalAmountPanelText, ironAmountPanelText, copperAmountPanelText,
            sandAmountPanelText, oilAmountPanelText, uraniumAmountPanelText, goldAmountPanelText;

    // Material Items
        [Tooltip("Item Data")]
        public ItemData 
            modulsData, toolsData, steelData, concreteData, glassData, woodData, carbonData, keroseneData;

    // Extra
        [Header("Empty Icons/Slots")]
        public GameObject emptyIcon;

    #endregion

    private void Start()
    {
        // Locates PlayerFactionController
        playerFactionController = FindObjectOfType<PlayerFactionController>();

        // Subscribe to event for the current island.
        playerInventory = FindObjectOfType<Inventory>();
        IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;

        // Initially hide all info panels
        if (InfoPanel != null) InfoPanel.SetActive(false);

        if (strategicMapButton != null) strategicMapButton.onClick.AddListener(ToggleStrategicMap);
    }

    private void OnDestroy()
    {
        // Unsubscribes on Destruction
        IslandManager.instance.OnPlayerHoverIsland -= OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;

        if (strategicMapButton != null) strategicMapButton.onClick.RemoveListener(ToggleStrategicMap);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleStrategicMap();
        }

        if (currentIsland != null)
        {
            // Player Based
            UpdateItemsUI();

            // Island Based
            UpdateResourceUI();
        }
    }




    private void OnCurrentIslandChanged(Island island)
    {
        
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }
        currentIsland = island;
        islandNameText.text = island.name.ToString();
        islandInventory = island.GetComponent<Inventory>(); 

    }


    /// <summary>
    /// Updates the UI for the items in the player's inventory.
    /// </summary>
    /// <remarks> This method is called when the player's inventory changes. </remarks>
    /// <returns> Returns Ints & Strings </returns>
    public void UpdateItemsUI()
    {

        // Player Inventory - Window

        if (playerInventory == null)
        {
            Debug.Log("No Player Base Detected! (Reason: playerInventory = Null)");
            return;
        }
        else
        {

            // Obtained:
            // Items Store on the Island to be Obtained by Players

            // Items are Stored on the Player Base to be Used by its Player

            material1Text.text = "" + playerInventory.GetItemAmount(modulsData);            // Displays the Modules Amount
            material2Text.text = "" + playerInventory.GetItemAmount(toolsData);             // Displays the Tools Amount

            material3Text.text = "" + playerInventory.GetItemAmount(steelData);             // Displays the Steel Amount
            material4Text.text = "" + playerInventory.GetItemAmount(concreteData);          // Displays the concrete Amount

            material5Text.text = "" + playerInventory.GetItemAmount(glassData);             // Displays the glass Amount
            material6Text.text = "" + playerInventory.GetItemAmount(woodData);              // Displays the wood Amount

            material7Text.text = "" + playerInventory.GetItemAmount(carbonData);            // Displays the carbon Amount
            material9Text.text = "" + playerInventory.GetItemAmount(keroseneData);          // Displays the kerosene Amount


        }
    }

    /// <summary>
    /// Desc: Resources Items Store on the Island to be Obtained by Players.
    /// + Updates the UI for the items in the island's inventory.
    /// </summary>
    /// <remarks> This method is called when the island's inventory changes. </remarks>
    /// <returns> Returns Ints & Strings </returns>
    public void UpdateResourceUI()
    {
        if (currentIsland == null || islandInventory == null)
        {
            Debug.LogError("Failed to update resource UI. Current island or inventory is null.");
            return;
        }
        else
        {
            UpdateResourcePanel(islandInventory);
            UpdateResourceSliders(islandInventory);
        }
    }
    // Helper method to check for null and log a specific error message
    void CheckPanelNull(object panelText, string panelName)
    {
        if (panelText == null)
            Debug.LogError("Error: " + panelName + " = Null");
    }
    private void UpdateResourcePanel(Inventory inventory)
    {

        // Panel Null Checks
        CheckPanelNull(uraniumAmountPanelText, "uraniumAmountPanelText");
        CheckPanelNull(copperAmountPanelText, "copperAmountPanelText");
        CheckPanelNull(gravelAmountPanelText, "gravelAmountPanelText");
        CheckPanelNull(goldAmountPanelText, "goldAmountPanelText");
        CheckPanelNull(sandAmountPanelText, "sandAmountPanelText");
        CheckPanelNull(ironAmountPanelText, "ironAmountPanelText");
        CheckPanelNull(coalAmountPanelText, "coalAmountPanelText");
        CheckPanelNull(oilAmountPanelText, "oilAmountPanelText");



        // Obtainables:
        // Resources Items Store on the Island to be Obtained by Players

        // Resource Panel

            gravelAmountPanelText.text = "" + inventory.GetItemAmount(gravelData);        // Displays the Gravel Amount
            coalAmountPanelText.text = "" + inventory.GetItemAmount(coalData);            // Displays the Coal Amount
            ironAmountPanelText.text = "" + inventory.GetItemAmount(ironData);            // Displays the Iron Amount

            sandAmountPanelText.text = "" + inventory.GetItemAmount(sandData);            // Displays the Sand Amount
            copperAmountPanelText.text = "" + inventory.GetItemAmount(copperData);        // Displays the Copper Amount
            oilAmountPanelText.text = "" + inventory.GetItemAmount(oilData);              // Displays the Oil Amount
                
            uraniumAmountPanelText.text = "" + inventory.GetItemAmount(uraniumData);      // Displays the Uranium Amount
            goldAmountPanelText.text = "" + inventory.GetItemAmount(goldData);            // Displays the Gold Amount

            // Purpose: To display the amount of items in the island inventory

            // Gathers the Amount from the island inventory and then
            // Displays its Int at the given Text Reference these are
            // the References for the Panels Total Amount. 
    }

    private void UpdateResourceSliders(Inventory inventory)
    {
        // Deposit Bars - Update According to Island inventory

        // Slider Null Check
        if (uraniumSlider == null || goldSlider == null || oilSlider == null || copperSlider == null || ironSlider == null) Debug.Log("Error: Slider 1 = Null");
        if (sandSlider == null || coalSlider == null || gravelSlider == null) Debug.Log("Error: Slider 2 = Null");



        // Total Amount

        UpdateResourceSlider(gravelSlider, inventory.GetItemAmount(gravelData));      // Displays the Gravel Amount
        UpdateResourceSlider(coalSlider, inventory.GetItemAmount(coalData));          // Displays the Coal Amount
        UpdateResourceSlider(ironSlider, inventory.GetItemAmount(ironData));          // Displays the Iron Amount
        UpdateResourceSlider(sandSlider, inventory.GetItemAmount(sandData));          // Displays the Sand Amount
        UpdateResourceSlider(copperSlider, inventory.GetItemAmount(copperData));      // Displays the Copper Amount
        UpdateResourceSlider(oilSlider, inventory.GetItemAmount(oilData));            // Displays the Oil Amount
        UpdateResourceSlider(uraniumSlider, inventory.GetItemAmount(uraniumData));    // Displays the Uranium Amount
        UpdateResourceSlider(goldSlider, inventory.GetItemAmount(goldData));          // Displays the Gold Amount

    }


    


    public void UpdateResourceSlider(Slider slider, int resourceAmount)
    {
        slider.value = resourceAmount;
        if (resourceAmount == 0)
        {
            // Code to display the "empty" icon - there will be one per Resource Item Slot in the UI,
            // Same object will have the slider as a child component. This is the same object players
            // have to hover to use the ShowResourceDetails() method.

            slider.gameObject.SetActive(false);
            // Assume emptyIcon is a GameObject that represents an icon indicating an empty resource.
            emptyIcon.gameObject.SetActive(true);
        }
        else
        {
            // Code to display the slider
            slider.gameObject.SetActive(true);
            // Code to hide the "empty" icon
            emptyIcon.gameObject.SetActive(false);

            // Code to change the color of the slider based on the amount of resources.
            if (resourceAmount <= 20000)
            {
                // Set slider color to red
                ChangeSliderColor(slider, Color.red);
                ChangeSliderLevel(slider, 20000);
            }
            else if (resourceAmount <= 30000)
            {
                // Set slider color to orange - Why doesn't orange exist?
                ChangeSliderColor(slider, Color.red + Color.yellow);
                ChangeSliderLevel(slider, 30000);

            }
            else if (resourceAmount <= 50000)
            {
                // Set slider color to yellow
                ChangeSliderColor(slider, Color.yellow);
                ChangeSliderLevel(slider, 50000);

            }
            else
            {
                // Set slider color to green
                ChangeSliderColor(slider, Color.green);
                ChangeSliderLevel(slider, 100000);

            }
        }
    }
    public void ChangeSliderColor(Slider slider, Color newColor)
    {
        // Changing the color of all Image components in the Slider and its children.
        Image[] allImages = slider.GetComponentsInChildren<Image>();
        foreach (Image img in allImages)
        {
            img.color = newColor;
        }
    }

    public void ChangeSliderLevel(Slider slider, int amount)
    {
        // exessive method?
        slider.value = amount;
       
        // slider.fillRect.sizeDelta = new Vector2(slider.fillRect.sizeDelta.x, slider.fillRect.sizeDelta.y);
    }

    public void ShowResourceDetail(string resourceName, int amount)
    {

        // This method will be called when the player hovers over the resource slot.

        // Once called this method will enable the resource detail panel.

        // We already have a method updating the text value of the resource detail panel.

    }

    #region Strategic Map Integration

    [Header("Strategic Map")]
    [SerializeField] private Button strategicMapButton;

    public void ToggleStrategicMap()
    {
        if (StrategicMapUI.Instance == null)
        {
            var map = FindObjectOfType<StrategicMapUI>(true);
            if (map == null)
            {
                GameObject go = new GameObject("StrategicMapUI");
                go.AddComponent<StrategicMapUI>();
            }
        }

        if (StrategicMapUI.Instance != null)
        {
            StrategicMapUI.Instance.Toggle();
        }
    }

    #endregion

}
