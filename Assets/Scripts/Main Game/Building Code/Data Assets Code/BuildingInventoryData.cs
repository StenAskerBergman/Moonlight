// Start - BuildingInventoryData.cs
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum InventoryType { Island, Base, Depot, Consumer }
public enum InventoryTier { Basic, Advanced }

[CreateAssetMenu(fileName = "New Building Inventory Data", menuName = "Data/Building/Building Inventory Data")]
public class BuildingInventoryData : ScriptableObject
{
 
    [Header("For Navigation & Identification")]
    [SerializeField] private int inventoryID;
    [SerializeField] private InventoryType currentInventoryType;
    [SerializeField] private InventoryTier currentInventoryTier;


    [Header("Island Type Reference")]
    [SerializeField, Tooltip("Used only if InventoryType is Island")]
    private IslandInventory islandInventoryReference; // Referencing IslandInventory instead

    public IslandInventory IslandInventoryReference
    {
        get
        {
            if (currentInventoryType != InventoryType.Island)
            {
                Debug.LogWarning("Trying to access IslandInventoryReference on a non-Island inventory.");
                return null;
            }
            return islandInventoryReference;
        }
    }

    public InventoryType CurrentInventoryType
    {
        get { return currentInventoryType; }
    }


    #region Inventory Behaviour

    [SerializeField] private InventoryBehaviour behavior;

    public void AddItem(Item item)
    {
        behavior.AddItem(item);
    }

    public void RemoveItem(Item item)
    {
        behavior.RemoveItem(item);
    }


    #endregion

    #region Amounts & Caps

    [Header("Amounts & Caps")]
    public int inventoryCapacity;
    public int nowInventoryCapacity;        // Capacity Now 
    public int maxInventoryCapacity;        // Capacity Maximum
    public int minInventoryCapacity;        // Capacity Minimum

    #endregion

    #region Speed Related

    [Header("Speed Related")]
    public int InventoryRate = 1;

    // Uncomment when needed
    // public int TotalInventoryRate = 1;             
    // public int InternalInventoryRate = 1;        
    // public int ExternalInventoryRate = 1;        

    #endregion
}
// End - BuildingInventoryData.cs