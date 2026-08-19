using UnityEngine;
using TMPro;
using System;


/*
    UnitSelections
          ↓
    selected Unit
          ↓
    | Unit.OnSelect()
    |     ↓
    | Unit.ViewUnit()
    |     ↓
    ├── UnitInventory
    ├── unit information
    ├── interactions
    └──  ...
          ↓
       Unit UI

 */

public class Unit : MonoBehaviour, ISelectable, IUniqueIdentifier
{
    // Unique ID
    public string ID { get; private set; }

    // Unit Types
    public UnitType type;
    public MoveType moveType;

    // Inventories
    public Inventory inventory;
    public UnitInventory unitInventory;

    // Userface Prefabs
    public GameObject inventoryUIPrefab; // Specific Userface Prefab for this unit.
    public GameObject categoryUIPrefab; // Specific Inventory Category for this unit.
                                        // Placeholder for the selection UI element
    [SerializeField]
    private GameObject[] selectionUIElements; // Assign this in the inspector

    public string unitTypeName { get; private set; } // Specific Unit Name - Submarine - PreSet Name
    [SerializeField] public string displayName; // Display'd Name Ingame - Personal Name 

    public Owner owner;
    public GameObject ownerPrefab;

    [SerializeField] public bool Selectable = false;
    [SerializeField] public bool Targetable = false;

    void Awake()
    {
        // Unique Unit ID
        ID = Guid.NewGuid().ToString();
    }

    void Start()
    {
        // Name Check - Ensure Name is assigned
        if (string.IsNullOrEmpty(displayName))
        {
            // Lacks a name thereby defaulting to a preset name based of vessel type
            SetDisplayName(displayName = NameGenerator.RandomNameSelector(NameType.Ship));
            Debug.Log("Unit: Generated Display Name: " + displayName);
        }

        // Set Name of the Unit
        this.gameObject.name += " - Name: " + displayName;


        // Inv Check - Ensure UnitInventory is assigned
        if (unitInventory == null && inventory == null)
        {

            Debug.Log($"<color=red><b>MISSING:</b></color> both UnitInventory && Inventory is not assigned on Unit: " + gameObject.name);
            return;

        } 
        else 
        { 
            Debug.Log($"<color=green><b>SUCCESS:</b> Unit has atleast one Inventory component!</color>"); 
        }

        UnitSelections.Instance.unitList.Add(this);

        
        // Set Name of the Unit + ID
        // this.gameObject.name += " - ID: " + ID + " - Name: " + displayName;

    }

    /// <summary>
    /// Easy way to swap between menu's
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="builder"></param>
    void MenuSwap(bool inventory, bool builder)
    {
        Debug.Log("Menu Swap!");

        UnitSelections.Instance.inventoryUIPanel.SetActive(inventory); // Assuming inventoryUIPanel is your inventory UI GameObject.
        UnitSelections.Instance.builderUIPanel.SetActive(builder); // Assuming builderUIPanel is your inventory UI GameObject.

        // Gocus - Inventory
        if (inventory) DisplayManager.Instance.FocusOn(inventoryUIPrefab);
        if (!inventory) DisplayManager.Instance.Unfocus(inventoryUIPrefab); 

        // Gocus - Builder
        if (builder) DisplayManager.Instance.FocusOn(UnitSelections.Instance.builderUIPanel); 
        if (!builder) DisplayManager.Instance.Unfocus(UnitSelections.Instance.builderUIPanel); 

        // Gocus - Userface
        if (!inventory && !builder)
        { 
          DisplayManager.Instance.Unfocus(inventoryUIPrefab);
          DisplayManager.Instance.Unfocus(UnitSelections.Instance.builderUIPanel);
        }
    }

    // Method in Unit.cs - Sets Name...
    public void SetDisplayName(string newName)
    {
        displayName = newName;
    }

    // OnSelect() 
    // Unit.cs - Unit.OnSelect() - On Unit Selection...
    public void OnSelect()
    {
        // Enable the Selection's first child GameObject
        transform.GetChild(0).gameObject.SetActive(true);


        // Handle Specific Type Logic on a Case by Case basis
        switch (type)
        {
            case UnitType.Character:
                // Enable Character Flag for Selection
                UnitSelections.Instance.AddCharacterCount();

                // Enable Movement for Character Units
                GetComponent<UnitMovement>().enabled = true;
                break;

            case UnitType.House:

                // Enable Character Flag for Selection
                UnitSelections.Instance.AddHouseCount();

                break;

            default:
                Debug.Log("Unit Type Not Found");
                break;
        }

        // Only display the inventory UI if the unit has Inventory.
        // Add Inventory to the UnitSelections Inventories Count to
        // Verify that a unit on it's selection list has inventory.

        if (HasInventory()) UnitSelections.Instance.AddInventoryCount();

        // Show specific UI elements for a single unit
        this.ViewUnit();

        // C35 W4 E11/2
        // New Way of Doing it. - Not Tested Yet
        //GetUnitInventoryUIComponent();

        // C35 W4 E11/2 
        // Old Way of Doing it. - Still semi works = errors
        // ShowSelectionUI(true); // Show the placeholder UI

        // Older Way of Doing it.
        // if (UnitSelections.Instance.unitsSelected.Count == 1) DisplayManager.Instance.FocusOn(inventoryUIPrefab); else { DisplayManager.Instance.Unfocus(inventoryUIPrefab); }
    }

    // Method in Unit.cs - View to Inspecting a Unit
    public void ViewUnit() 
    {
        // Only display the inventory UI if the unit is selectable.
        if (!Selectable || !HasInventory()) return;

        // Fetch or find the UnitInventoryUI component
        var unitInventoryUI = GetUnitInventoryUIComponent();

        // Update the inventory UI - if Not Null
        if (unitInventoryUI != null)
        {
            // Ensure the parent GameObject is active
            // unitInventoryUI.transform.parent.gameObject.SetActive(true); // Caused Errors
                    // Ensure inventories are assigned before enabling the UI.
            //unitInventoryUI.AssignInventoriesIfNeeded();
            unitInventoryUI.SetInspection(this.gameObject);
            unitInventoryUI.SetUnit(GetUnit());
            unitInventoryUI.UpdateDisplayName(displayName);
            unitInventoryUI.UpdateBasedOnSelection(this);
            unitInventoryUI.RefreshInventoryDisplay(); // Manually call refresh
            unitInventoryUI.SetUnitInventory(GetUnitInventory()); // Manually call SetUnitInventory
            UnitSelections.Instance.inventoryUIPanel.SetActive(true);
                
            // Verify that the UnitInventoryUI was Found!
            Debug.Log("Unit: UnitInventoryUI component found in the scene. Name: " + unitInventoryUI.name);
                
            // Show the inventory UI, On Singular Unit
            // Selected, And Hide the builder Menu's

            //MenuSwap(true, false);

            if(!unitInventoryUI.isActiveAndEnabled)
            {
                UnitSelections.Instance.inventoryUIPanel.SetActive(true);
                DisplayManager.Instance.Focus();
            }
        }
        else
        {
            Debug.LogError("UnitInventoryUI component not found in the scene.");
        }

    }

    // Method in Unit.cs - View to Inspecting a Unit
    public void UnViewUnit()
    {
        // Check for Inventory
        if (HasInventory())
        {
            // Hide the inventory UI
            MenuSwap(false, false);
        }
    }

    private bool HasInventory()
    {
        return inventory != null || unitInventory != null;
    }

    // Method to handle the display of the selection UI
    private void ShowSelectionUI(bool show)
    {
        if (selectionUIElements != null && selectionUIElements.Length > 0)
        {
            // Foreach GameObject in the array...
            foreach (GameObject uiElement in selectionUIElements)
            {

                // Set as Active or Inactive based on 'show'
                if (uiElement != null) uiElement.SetActive(show); // Null Error 
                else Debug.LogWarning("uiElement is Null!");
                // if(selectionUIElements != null) uiElement.SetActive(show);
                // if (selectionUIElements == null) Destroy(uiElement);
                // if (selectionUIElements == null && uiElement.gameObject.GetComponentsInChildren<ItemSlot>.().amount == 0) Destroy(uiElement);
            }
        }
        else
        {
            Debug.LogError("Selection UI Elements are not assigned or empty.");

        }
    }

    #region Getter Methods

    // Units.cs 
    public UnitInventoryUI GetUnitInventoryUIComponent()
    {
        ShowSelectionUI(true);

        // First, find or create the UnitInventoryUI instance
        var unitInventoryUI = UnitSelections.Instance.GetUnitInventoryUI();
        if (unitInventoryUI == null)
        {
            // Can always be called once... 
            Debug.Log($"<color=Gray>Unit: UnitInventoryUI is Null... Creating New One!</color>");

            if (inventoryUIPrefab != null)
            {
                var uiPrefabInstance = inventoryUIPrefab; //Instantiate(inventoryUIPrefab);
                unitInventoryUI = uiPrefabInstance.GetComponent<UnitInventoryUI>();
                if (unitInventoryUI == null)
                {
                    Debug.LogError("UnitInventoryUI component not found on the prefab.");
                    return null;
                }
            }
            else
            {
                Debug.LogError("InventoryUIPrefab is not assigned.");
                return null;
            }
        }

        // Ensure that the UnitInventoryUI has a reference to this Unit's UnitInventory
        if (unitInventory != null)
        {
            unitInventoryUI.SetUnitInventory(unitInventory);
        }
        else if (inventory != null)
        {
            unitInventoryUI.SetInventory(inventory);
        }
        else if (unitInventory != null && inventory != null)
        {
            Debug.LogError("UnitInventory not found on " + gameObject.name);
            return null;
        }
        else if (unitInventory != null || inventory != null)
        {
            Debug.LogError("UnitInventory not found on " + gameObject.name);
            return null;
        }

        return unitInventoryUI;
    }

    public GameObject GetPanelCategory()
    {
        return categoryUIPrefab;
    }
    public GameObject GetUnitObject()
    {
        return this.gameObject;
    }
    public Unit GetUnit()
    {
        return this;
    }

    public GameObject GetInventoryUIPrefab()
    {
        return inventoryUIPrefab;
    }

    /// <summary>
    /// Gets the Unit's UnitInventory even if it's null
    /// 
    /// Important Note! - This is not the same as GetInventory();
    /// </summary>
    /// <returns>UnitInventory</returns>
    public UnitInventory GetUnitInventory()
    {
        return unitInventory;
    }

    /// <summary>
    /// Gets the Unit's Inventory even if it's null
    /// 
    /// Important Note! - This is not the same as GetUnitInventory();
    /// </summary>
    /// <returns>Inventory</returns>
    public Inventory GetInventory()
    {
        return inventory;
    }

    #endregion

    public void OnHover()
    {
        // Custom logic for when the unit is hovered over
        // Assets Yet to be made!
        // - Play Shine OnHover Animation <- Yet to be made!
        // transform.GetChild(0).gameObject.SetActive(false); // Add another child maybe?
        // - Play Hover Sound? <- Yet to be made!
    }

    public void OnDeselect()
    {
        // Custom logic for when the unit is deselected

        // Disables the Selections first Childs GameObject
        transform.GetChild(0).gameObject.SetActive(false);

        // Hide this unit's specific UI elements
        // DisplayManager.Instance.Unfocus(true);

        // Reset the flags
        switch (type)
        {
            // Cases:
            // Set Unit Type Flag to False

            case UnitType.Character:
                UnitSelections.Instance.RemoveCharacterCount();
                GetComponent<UnitMovement>().enabled = false;
                break;

            case UnitType.House:
                UnitSelections.Instance.RemoveHouseCount();
                break;

            default:
                Debug.Log("Unit Type Not Found to disable");
                break;
        }

        // Reset the inventory UI
        if (HasInventory() && UnitSelections.Instance != null)
        {
            // Set the flag to false as the unit does not have an inventory.
            UnitSelections.Instance.RemoveInventoryCount(); 
            
            // Hide the inventory UI if it's not relevant for the selected unit.
            UnitSelections.Instance.inventoryUIPanel.SetActive(false); 
        }

        // MenuSwap(false, true);

        ShowSelectionUI(false); // Hide the placeholder UI
    }


    void OnDestroy()
    {

        // Reset Any Flags
        OnDeselect();

        // Removes Unit from List
        UnitSelections.Instance.unitList.Remove(this);

        // Removes Unit from Game
        this.gameObject.SetActive(false);

    }
}

// End - Unit.cs