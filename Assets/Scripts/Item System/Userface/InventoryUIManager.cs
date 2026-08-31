// InventoryUIManager.cs - Start

using UnityEngine;

public class InventoryUIManager : MonoBehaviour
{
    public GameObject unitInventoryTemplate;        
    public GameObject buildingInventoryTemplate;  

    private GameObject currentActiveTemplate;

    // Singleton_City
    private static InventoryUIManager _instance;
    public static InventoryUIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InventoryUIManager>();

                if (_instance == null)
                {
                    GameObject instanceObject = new GameObject(nameof(InventoryUIManager));
                    _instance = instanceObject.AddComponent<InventoryUIManager>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        // Handle singleton instance assignment
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // This is match HUD, so it must not outlive the match scene. It used to
        // DontDestroyOnLoad itself, which carried a gameplay inventory panel into
        // the main menu and left a stale _instance pointing at it.
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void DisplayInventoryForUnit(Unit selectedUnit)
    {
        // Deactivate the current active template if any
        if (currentActiveTemplate)
            currentActiveTemplate.SetActive(false);

        // Determine which inventory template to use based on the unit type
        switch (selectedUnit.unitType)
        {
            case UnitType.Character:
                currentActiveTemplate = unitInventoryTemplate;
                Debug.Log("<color=lightblue>InventoryUIManager: </color><color=green>UI Showing: Unit Inventory Template</color>");
                break;
            case UnitType.House:
                currentActiveTemplate = buildingInventoryTemplate;
                Debug.Log("<color=lightblue>InventoryUIManager: </color><color=green>UI Showing: Building Inventory Template</color>");
                break;

            // ... potentially other cases ...

            default:
                // Log an error and return if there's no suitable inventory template
                Debug.LogError($"<color=red>InventoryUIManager: No inventory template found for unit type {selectedUnit.unitType}</color>");
                return;
        }

        // Check if the unit has an inventory to show, and set the current "inventory" template active accordingly
        if (selectedUnit.inventory != null || selectedUnit.unitInventory != null)
        {
            currentActiveTemplate.SetActive(true);
            // Call methods to fill the template with data from the selected unit's inventory
        }
        else
        {
            currentActiveTemplate.SetActive(false);
        }

        // unitInventoryTemplate is a prefab with UnitInventoryUI component
        UnitInventoryUI unitInventoryUI = unitInventoryTemplate.GetComponent<UnitInventoryUI>();
        if (unitInventoryUI != null)
        {
            unitInventoryUI.SetUnitInventory(selectedUnit.GetComponent<UnitInventory>());
        }
        else
        {
            Debug.LogError("<color=red>InventoryUIManager: UnitInventoryUI component not found on the template.</color>");
        }
    }

    // Additional: To hide inventory if needed
    public void HideInventory()
    {
        if (currentActiveTemplate)
            currentActiveTemplate.SetActive(false);
    }


}

// InventoryUIManager.cs - End
