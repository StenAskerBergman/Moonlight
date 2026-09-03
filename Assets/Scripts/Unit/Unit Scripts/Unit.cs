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
    private string _id;
    public string ID
    {
        get => !string.IsNullOrEmpty(_id) ? _id : (_id = Guid.NewGuid().ToString());
        private set => _id = value;
    }

    private SelectionOutlineTarget outline;

    // Lazily fetched/added so unit prefabs don't each need the component
    // hand-placed — SelectionOutlineTarget carries no prefab-specific setup.
    private SelectionOutlineTarget Outline =>
        outline ??= GetComponent<SelectionOutlineTarget>()
                   ?? gameObject.AddComponent<SelectionOutlineTarget>();

    private UnitCommandExecutor commandExecutor;
    public UnitCommandExecutor CommandExecutor =>
        commandExecutor ??= GetComponent<UnitCommandExecutor>()
                          ?? gameObject.AddComponent<UnitCommandExecutor>();

    // Unit Types
    public UnitType unitType;
    public MoveType moveType;

    public UnitType GetUnitType(Unit unit) {  return unit.unitType; }
    public MoveType GetMoveType(Unit unit) { return unit.moveType; }

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

    [SerializeField] public UnitDefinition definition;

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
        if (string.IsNullOrWhiteSpace(displayName))
        {
            // Lacks a name thereby defaulting to a preset name based of vessel type.
            // Whitespace counts as nameless: the Inspector default is an empty string, and
            // IsNullOrEmpty let a name that was only spaces through as "already named".
            SetDisplayName(displayName = NameGenerator.RandomNameSelector(ResolveNameType()));
            Debug.Log("Unit: Generated Display Name: " + displayName);
        }

        // Set Name of the Unit
        this.gameObject.name += " - Name: " + displayName;


        // Try to fetch inventories if they are attached but not assigned
        if (unitInventory == null) unitInventory = GetComponent<UnitInventory>();
        if (inventory == null) inventory = GetComponent<Inventory>();

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

    /// <summary>
    /// Which name pool an unnamed unit draws from.
    ///
    /// The UnitDefinition's own nameType wins - that is what UnitFactory uses, so a unit
    /// spawned through the factory and one that fell through to this fallback end up
    /// named from the same pool. Without a definition the movement domain decides, which
    /// stops submarines and aircraft being handed surface-ship names.
    /// </summary>
    private NameType ResolveNameType()
    {
        if (definition != null) return definition.nameType;

        switch (moveType)
        {
            case MoveType.Submersible:
                return NameType.ExplorerSub;
            case MoveType.Aircraft:
                return NameType.Plane;
            default:
                return NameType.Ship;
        }
    }

    // OnSelect() 
    // Unit.cs - Unit.OnSelect() - On Unit Selection...
    public void OnSelect()
    {
        // Enable the Selection's first child GameObject
        transform.GetChild(0).gameObject.SetActive(true);

        // Stencil-outline silhouette over this unit's renderers (see
        // SelectionOutlineTarget/SelectionOutlineRendererFeature). Additive to the
        // ground-marker child above, not a replacement for it.
        Outline.SetSelected(true);


        // Handle Specific Type Logic on a Case by Case basis
        switch (unitType)
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
        if (!HasInventory()) return;

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
            if (InventoryUIManager.Instance != null)
            {
                InventoryUIManager.Instance.DisplayInventoryForUnit(this);
            }
                
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

            if (UnitInformationPanel.Instance != null)
            {
                UnitInformationPanel.Instance.SelectUnit(this);
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
        // else: Normal for units without custom overhead UI elements — not an error.
    }

    #region Getter Methods

    // Units.cs 
    public UnitInventoryUI GetUnitInventoryUIComponent()
    {
        ShowSelectionUI(true);

        // A unit points at its concrete inventory panel (ship, submarine, aircraft).
        // The shared inventoryUIPanel is only a container and does not carry
        // UnitInventoryUI itself, so asking the container first leaves the visible
        // child panel unbound.
        var unitInventoryUI = inventoryUIPrefab != null
            ? inventoryUIPrefab.GetComponent<UnitInventoryUI>()
            : null;

        if (unitInventoryUI == null)
        {
            unitInventoryUI = UnitSelections.Instance.GetUnitInventoryUI();
        }

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
        // Guarded: during OnDestroy the child may already be gone, and a throw here
        // used to abort OnDestroy before the unit deregistered from unitList.
        if (transform.childCount > 0)
        {
            transform.GetChild(0).gameObject.SetActive(false);
        }


        if (Outline != null) Outline.SetSelected(false); // Does cause error

        // Hide this unit's specific UI elements
        // DisplayManager.Instance.Unfocus(true);

        // Reset the flags
        switch (unitType)
        {
            // Cases:
            // Set Unit Type Flag to False

            case UnitType.Character:
                UnitSelections.Instance.RemoveCharacterCount();
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
            
            // Only hide the panel when no other units remain selected;
            // otherwise a shift-deselect would kill the HUD for the rest.
            if (UnitSelections.Instance.unitsSelected.Count <= 1)
            {
                UnitSelections.Instance.inventoryUIPanel.SetActive(false); 
            }
            else
            {
                Unit remainingUnit = UnitSelections.Instance.unitsSelected.Find(u => u != this && u != null);
                if (remainingUnit != null)
                {
                    remainingUnit.ViewUnit();
                }
            }
        }

        // MenuSwap(false, true);

        ShowSelectionUI(false); // Hide the placeholder UI
    }


    void OnDestroy()
    {
        // Destruction is not a normal deselection. Do not call OnDeselect here:
        // during scene teardown its UI and child objects may already be destroyed,
        // and the lazy Outline accessor would try to add a component to this dying
        // GameObject. Only keep the selection registry internally consistent.
        UnitSelections selections = UnitSelections.Instance;
        if (selections != null)
        {
            selections.unitList.Remove(this);
            selections.unitsSelected.Remove(this);
        }
    }
}

// End - Unit.cs
