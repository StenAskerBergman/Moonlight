using UnityEngine;

public class InventoryUIManager : MonoBehaviour
{
    public GameObject unitInventoryTemplate;        // Drag the Unit Inventory UI template prefab here
    public GameObject buildingInventoryTemplate;    // Drag the Building Inventory UI template prefab here

    private GameObject currentActiveTemplate;

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
                    GameObject instanceObject = new GameObject();
                    _instance = instanceObject.AddComponent<InventoryUIManager>();
                    DontDestroyOnLoad(instanceObject);
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
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void DisplayInventoryForUnit(Unit selectedUnit)
    {
        // Deactivate the current active template if any
        if (currentActiveTemplate)
            currentActiveTemplate.SetActive(false);

        // Determine which inventory template to use based on the unit type
        switch (selectedUnit.type)
        {
            case UnitType.Character:
                currentActiveTemplate = unitInventoryTemplate;
                break;
            case UnitType.House:
                currentActiveTemplate = buildingInventoryTemplate;
                break;

            // ... potentially other cases ...

            default:
                // Log an error and return if there's no suitable inventory template
                Debug.LogError($"No inventory template found for unit type {selectedUnit.type}");
                return;
        }

        // Check if the unit has an inventory to show, and set the template active accordingly
        if (selectedUnit.inventory != null)
        {
            currentActiveTemplate.SetActive(true);
            // Call methods to fill the template with data from the selected unit's inventory
        }
        else
        {
            currentActiveTemplate.SetActive(false);
        }
    }

    // Additional: To hide inventory if needed
    public void HideInventory()
    {
        if (currentActiveTemplate)
            currentActiveTemplate.SetActive(false);
    }
}

// Prior to Singleton Implementation 
//using UnityEngine;

//public class InventoryUIManager : MonoBehaviour
//{
//    public GameObject unitInventoryTemplate; // Drag the UnitInventory UI template prefab here
//    public GameObject buildingInventoryTemplate; // Drag the BuildingInventory UI template prefab here

//    private GameObject currentActiveTemplate;

//    public void DisplayInventoryForUnit(Unit selectedUnit)
//    {
//        // Deactivate the current active template if any
//        if (currentActiveTemplate)
//            currentActiveTemplate.SetActive(false);

//        // Determine which inventory template to use based on the unit type
//        switch (selectedUnit.type)
//        {
//            case UnitType.Character:
//                currentActiveTemplate = unitInventoryTemplate;
//                break;
//            case UnitType.House:
//                currentActiveTemplate = buildingInventoryTemplate;
//                break;

//            // ... potentially other cases ...

//            default:
//                // Log an error and return if there's no suitable inventory template
//                Debug.LogError($"No inventory template found for unit type {selectedUnit.type}");
//                return;
//        }

//        // Check if the unit has an inventory to show, and set the template active accordingly
//        if (selectedUnit.inventory != null)
//        {
//            currentActiveTemplate.SetActive(true);
//            // Call methods to fill the template with data from the selected unit's inventory
//        }
//        else
//        {
//            currentActiveTemplate.SetActive(false);
//        }
//    }
//}
