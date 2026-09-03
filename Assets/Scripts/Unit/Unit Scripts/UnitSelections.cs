using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine;
using System;

public enum UnitType { Character, House, Drone };

public class UnitSelections : MonoBehaviour
{
    // Prefabs
    public GameObject groundMarker;

    // Lists
    public List<Unit> unitList = new List<Unit>();
    public List<Unit> unitsSelected = new List<Unit>();
    public Unit FocusedUnit { get; private set; }
    public bool LastSelectionWasDrag { get; private set; }

    // Flag Bools
    public bool hasSelected = false;
    public bool hasHouses = false;
    public bool hasCharacters = false;



    // Flag Counts
    public int houseCount = 0;
    public int characterCount = 0;
    public int inventoryCount = 0;

    [Space (10)]
    public UnityEvent<List<Unit>> selectionChanged;

    // For Inventory UI
    public bool hasInventory = false;  
    
    // Panels 
    public GameObject builderUIPanel;
    public GameObject inventoryUIPanel; // GO: unitInventoryUI.cs
    public GameObject SingleUnitUIPage;
    public GameObject MultiUnitUIPanel;
    [Space (10)]

    // Categories
    public GameObject houseCategory; 
    public GameObject characterCategory;
    [Space (10)]

    private static UnitSelections _instance;
    public static UnitSelections Instance { get { return _instance; } }

    #region User Interface Events

    // User Interface Events

    // Is one or multiple units selected? - Works <- ( Future Note: how? explain plz!)
    // Is a character selected or house selected? - Works
    // Is several characters selected or several houses selected? - yet to be done

    // How to do we differantiate between units with attributes like - yet to be done
    // Enemies, Allies, Factions and so on etc.

    // Determine What userface to show when a character is selected - Corresponding Character Menu
    // Determine What userface to show when a building is selected - Corresponding Building Menu
    // Determine What userface to show when a drone is selected - Nothing

    // Characters > Building
    // Click on Enemies / Allies

    // Determine What userface to show when a unit is selected
    // Determine What userface to show depending on what unit is selected

    // Determine What userface priority to apply when showcasing
    // Determine What Userface Events are needed for this script

    // I'm starting to believe it's the limitations in size of the selected list which are to blame 

    #endregion

    #region On Awake List
    private void Awake()
    {  
        // Singleton City
        if (_instance != null && _instance != this) 
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            if (selectionChanged == null) selectionChanged = new UnityEvent<List<Unit>>();
            if (GetComponent<MultiUnitSelectionPanel>() == null)
            {
                gameObject.AddComponent<MultiUnitSelectionPanel>();
            }
            if (GetComponent<UnitInformationPanel>() == null && UnitInformationPanel.Instance == null)
            {
                gameObject.AddComponent<UnitInformationPanel>();
            }
            if (GetComponent<IdleUnitQueueNavigator>() == null)
            {
                gameObject.AddComponent<IdleUnitQueueNavigator>();
            }
            if (GetComponent<PlayerUnitOrderDispatcher>() == null)
            {
                gameObject.AddComponent<PlayerUnitOrderDispatcher>();
            }
            if (GetComponent<SelectedUnitOrderVisualizer>() == null)
            {
                gameObject.AddComponent<SelectedUnitOrderVisualizer>();
            }
        }
    }
    #endregion

    #region Click Select

    // When Clicked on Unit Then...
    public void ClickSelect(Unit unitToAdd) 
    {
        LastSelectionWasDrag = false;
        // Handles A single Unit Clicked 
        if (!unitsSelected.Contains(unitToAdd))
        {
            DeselectBuildingWhenSelectingBoat(unitToAdd);
            this.DeselectAll();
            unitsSelected.Add(unitToAdd);
            FocusedUnit = unitToAdd;
            NotifySelectionChanged();

            // Call OnSelect on the newly selected unit
            (unitToAdd as ISelectable)?.OnSelect();

            // Show HUD elements for the selected unit
            UserfaceHandler();


        }
    }
    #endregion

    #region Shift Click

    // Shift Click Select

    public void ShiftClickSelect(Unit unitToAdd)
    {
        LastSelectionWasDrag = false;
        if (!unitsSelected.Contains(unitToAdd))
        {
            DeselectBuildingWhenSelectingBoat(unitToAdd);

            // Shift Add
            unitsSelected.Add(unitToAdd);
            unitToAdd.transform.GetChild(0).gameObject.SetActive(true); 
            unitToAdd.GetComponent<UnitMovement>().enabled = true;
            (unitToAdd as ISelectable)?.OnSelect();
            NotifySelectionChanged();
            UserfaceHandler();
            FlagCheck();
        }
        else
        {
            // Shift Remove
            unitToAdd.transform.GetChild(0).gameObject.SetActive(false);
            (unitToAdd as ISelectable)?.OnDeselect();
            unitsSelected.Remove(unitToAdd);
            NotifySelectionChanged();
            UserfaceHandler();
            FlagCheck();

            // Refresh HUD to the first remaining unit so the panel
            // isn't left stale or hidden after a shift-deselect.
            if (unitsSelected.Count > 0)
            {
                FocusedUnit.ViewUnit();
            }
        }
    }
    #endregion

    #region Drag Selection 

    public void DragSelect(Unit unitToAdd)
    {
        LastSelectionWasDrag = true;
        if (unitToAdd == null) return;

        if (!unitsSelected.Contains(unitToAdd))
        {
            // Call OnSelect on all drag-selected units (was Character-only,
            // which left ships/submarines without outline or HUD).
            (unitToAdd as ISelectable)?.OnSelect();


            // Add any new Units to Current Selection
            unitsSelected.Add(unitToAdd);
            if (unitToAdd.transform.childCount > 0)
            {
                unitToAdd.transform.GetChild(0).gameObject.SetActive(true);
            }
            NotifySelectionChanged();
        }

        UserfaceHandler();
        FlagCheck();
    }

    private static void DeselectBuildingWhenSelectingBoat(Unit unit)
    {
        if (InfluenceManager.IsBoatUnit(unit) && BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.DeselectAll();
        }
    }

    #endregion

    #region Deselect All
    public void DeselectAll()
    {
        foreach (var unit in unitsSelected)
        {

            unit.transform.GetChild(0).gameObject.SetActive(false);

            // Call OnDeselect on the deselected units
            (unit as ISelectable)?.OnDeselect();
        }
        unitsSelected.Clear();
        LastSelectionWasDrag = false;
        NotifySelectionChanged();
        UserfaceHandler();
        FlagCheck();
    }
    #endregion

    #region Deselect
    public void Deselect(GameObject unitToDeselect)
    {
        NotifySelectionChanged();
    }

    #endregion

    #region Userface Flags

    // Show Anything - Works
    public bool ShowUserface = false;

    // Show Correct Menu - Works
    public bool Single = false;
    public bool Multi = false;

    private void UserfaceHandler() // (Unit unit) Remove bc it Didn't do much...
    {
        // Show Selection Elements for the selected unit
        switch (unitsSelected.Count)
        {
            case 0:
                // No Units Selected
                // if (unit != null) unit.UnViewUnit(); // should be Null if nothing is selected? 
                hasHouses = false;
                hasCharacters = false;

                ShowUserface = false;

                Single = false;
                Multi = false;
                break;

            case 1:
                // One Unit Left
                // if (unit != null) unit.UnViewUnit(); // should be Null if nothing is selected? 
                // Hide.MultiUnitUserface(units); - yet to be made!
                ShowUserface = true;

                Single = true;
                Multi = false;
                break;
            

            default:
                // More than One Unit Left
                // if (unit != null) unit.UnViewUnit(); // should be Null if nothing is selected? 
                // Show.MultiUnitUserface(units); - yet to be made!
                ShowUserface = true;

                Single = false;
                Multi = true;
                break;
        }

        FlagCheck();
    }

    #endregion

    #region Selection Flags
    public bool MoreHouses = false;
    public bool MoreCharacters = false;


    // Flag Check
    public void FlagCheck()
    {
        // Indication Flags
        if (characterCount > 0)
            hasCharacters = true;
        else hasCharacters = false;

        if (houseCount > 0)
            hasHouses = true;
        else hasHouses = false;

        if (inventoryCount > 0)
            hasInventory = true;
        else hasInventory = false;


        // Priority Flags
        if (characterCount < houseCount)
            MoreHouses = true;
        else MoreHouses = false;

        if (houseCount < characterCount)
            MoreCharacters = true;
        else MoreCharacters = false;

    }

    // Unit Type Selection Methods
    
    // Characters
    public void AddCharacterCount()
    {
        characterCount += 1;
        // Debug.Log("Add Character to: "+ characterCount);
        FlagCheck();
    }
    public void RemoveCharacterCount()
    {
        characterCount -= 1;
        // Debug.Log("Remove Character to: "+ characterCount);
        FlagCheck();
    }

    // Houses
    public void AddHouseCount()
    {
        houseCount += 1;
        FlagCheck();
    }
    public void RemoveHouseCount()
    {
        houseCount -= 1;
        FlagCheck();
    }

    // Inventory
    public void AddInventoryCount()
    {
        inventoryCount += 1;
        FlagCheck();
    }
    public void RemoveInventoryCount()
    {
        inventoryCount -= 1;
        FlagCheck();
    }
    #endregion

    #region Get Selection References

    public int FocusedUnitIndex => FocusedUnit == null ? -1 : unitsSelected.IndexOf(FocusedUnit);

    public void SelectOnly(Unit unit)
    {
        if (unit == null) return;
        if (unitsSelected.Count == 1 && FocusedUnit == unit)
        {
            unit.ViewUnit();
            return;
        }

        // ClickSelect intentionally ignores an already-selected unit. Clear a
        // multi-selection first when the FIFO navigator targets one of its members.
        if (unitsSelected.Contains(unit)) DeselectAll();
        ClickSelect(unit);
    }

    public void FocusSelectedUnit(int index)
    {
        if (unitsSelected.Count == 0) return;

        index = (index % unitsSelected.Count + unitsSelected.Count) % unitsSelected.Count;
        Unit nextFocus = unitsSelected[index];
        if (nextFocus == null) return;

        FocusedUnit = nextFocus;
        NotifySelectionChanged();
        FocusedUnit.ViewUnit();
    }

    public void FocusSelectedUnitOffset(int offset)
    {
        int currentIndex = FocusedUnitIndex;
        FocusSelectedUnit((currentIndex < 0 ? 0 : currentIndex) + offset);
    }

    /// <summary>
    /// Reconciles selection state and notifies selection consumers after a caller has
    /// changed <see cref="unitsSelected"/> directly.
    /// </summary>
    public void NotifySelectionChanged()
    {
        unitsSelected.RemoveAll(unit => unit == null);
        if (FocusedUnit == null || !unitsSelected.Contains(FocusedUnit))
        {
            FocusedUnit = unitsSelected.Count > 0 ? unitsSelected[0] : null;
        }

        selectionChanged?.Invoke(unitsSelected);
    }

    // In UnitSelections.cs
    public UnitInventoryUI GetUnitInventoryUI()
    {
        // Gets the prefabs UI script
        if (inventoryUIPanel.GetComponent<UnitInventoryUI>() != null)
            return inventoryUIPanel.GetComponent<UnitInventoryUI>();
        else Debug.Log("GetUnitInventoryUI is returning null"); return null;
    }

    void QuickCheck()
    {
        if (characterCount == 0 && hasSelected == false)
        {
            Debug.Log("<color=red>No units selected. - All returns will be null!</color>");
        }
    }
    /* How to Use - Call a Generic Function
        
        UnitInventory unitInventory = UnitSelections.Instance.GetSelectedComponent<UnitInventory>();

        if (unitInventory != null)
        {
            // Do something with Units type of Inventory
        }
    */

    // In UnitSelections.cs - Gets a Component on the selected unit
    public T GetSelectedComponent<T>() where T : Component
    {
        QuickCheck();

        // Check if there is at least one unit selected
        if (UnitSelections.Instance.unitsSelected.Count > 0)
        {
            // Get the first selected unit
            Unit selectedUnit = UnitSelections.Instance.FocusedUnit;

            // Try to get the component of type T from the selected unit
            T instance = selectedUnit.GetComponent<T>();

            // Success: Returns Component
            if (instance != null)
            {
                return instance;
            }
            else
            {
                // If the component wasn't found on the selected unit
                Debug.Log($"UnitSelections: GetSelectedComponent - Component of type {typeof(T).Name} not found on {selectedUnit.name}");
                return default;
            }
        }
        else
        {
            // If no units are selected
            Debug.Log("<color=yellow>UnitSelections: GetSelectedComponent - No units are currently selected.</color>");
            return default;
        }
    }

    public Unit GetSelectedUnit()
    {
        return FocusedUnit;
    }

    // In UnitSelections.cs - Get UnitInventory from selected unit
    public UnitInventory GetSelectedUnitInventory()
    {
        QuickCheck();

        // Gets the selected unit UnitInventory script
        Unit selectedUnit = FocusedUnit;
        if (selectedUnit != null && selectedUnit.GetInventory()!=null) return selectedUnit.GetUnitInventory();
        else if (UnitSelections.Instance.GetComponent<UnitInventory>()!=null) return UnitSelections.Instance.GetComponent<UnitInventory>();
        else Debug.Log("<color=red>UnitSelections: Be Aware! - GetSelectedUnitInventory returned UnitInventory as null</color>"); return null;
    }

    // In UnitSelections.cs - Get Inventory from selected unit
    public Inventory GetSelectedInventory()
    {
        QuickCheck();

        // Gets the selected units Inventory script
        Unit selectedUnit = FocusedUnit;
        if (selectedUnit != null && selectedUnit.GetInventory()) return selectedUnit.GetInventory();
        else if (UnitSelections.Instance.GetComponent<Inventory>() != null) return UnitSelections.Instance.GetComponent<Inventory>();
        else Debug.Log("<color=red>UnitSelections: Be Aware! - GetSelectedUnitInventory returned Inventory as null</color>"); return null;
    }

    // In UnitSelections.cs - Get Inventory from selected unit
    public UnitInventoryUI GetSelectedInventoryUIComponent()
    {
        QuickCheck();

        // Gets the selected units Inventory script
        Unit selectedUnit = FocusedUnit;
        if (selectedUnit != null && selectedUnit.GetUnitInventoryUIComponent()) return selectedUnit.GetUnitInventoryUIComponent();
        else if (UnitSelections.Instance.GetComponent<UnitInventoryUI>() != null) return UnitSelections.Instance.GetComponent<UnitInventoryUI>();
        else Debug.Log("<color=red>UnitSelections: Be Aware! - GetSelectedUnitInventory returned Inventory as null</color>"); return null;
    }
    #endregion

    private void Start()
    {
        selectionChanged.AddListener(SelectionChangedHandler);
    }

    private void OnDestroy()
    {
        selectionChanged.RemoveListener(SelectionChangedHandler);
    }

    public void SelectionChangedHandler(List<Unit> selectedUnits)
    {
        if (selectedUnits.Count > 0) Debug.Log($"Selection changed, count: {selectedUnits.Count}");
    }

}


