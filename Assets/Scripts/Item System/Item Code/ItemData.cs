// Start - ItemData.cs
// ScriptableObject for item data.
using UnityEngine;

[CreateAssetMenu(fileName = "New Item Data", menuName = "Data/Item/Item Data")]
[System.Serializable]
public class ItemData : ScriptableObject, IIdentifiable
{
    [Header("Identity")]
    [Tooltip("Namespaced identifier (e.g. 'core:timber', 'core:raw_gravel', 'modname:custom_alloy').")]
    [SerializeField] private string identifier = "core:item";

    public Identifier Id => !string.IsNullOrEmpty(identifier)
        ? new Identifier(identifier)
        : new Identifier($"core:{name.ToLowerInvariant().Replace(' ', '_')}");

    // Item Data
    public string displayName;  // Items Display Name 
    public string itemName;     // The Name of the item
    public string description;  // The Description of the item
    public float baseValue;     // Base value of the item
    public ItemType type;       // Generic Type of the item
    public Sprite Icon;         // Default Icon of the item

    // Slot Data
    public ItemSlot oldSlot;    // The old slot of the item
    public ItemSlot newSlot;    // The new slot of the item

    // Item Properties
    public int factor;          // 1 is low factor, 10 is high factor, or any scale you choose.
    public int powerLevel;      // 1 is low power, 10 is high power, or any scale you choose.
    public int rarity;          // 1 is common, 10 is legendary, or any scale you choose.
    public int uses;            // The number of times this item can be used before it is destroyed.
    
    // For unique items!
    // > Inventories define their own max stack size,
    // default is 1 to 40 for vessels while its 5 for
    // the drones and less for the aircrafts and subs
    
    public int maxStackSize;    // The maximum number of this item that can be stacked together. 
    
    // Production - Crafting Properties
    public int productionRate;  // The number of items this item can produce per hour.

    // Properties
    public bool isStackable;        // Can the item be stacked?
    public bool isConsumable;       // Can the item be consumed?
    public bool isCraftable;        // Can the item be crafted?
    public bool isTradeable;        // Can the item be traded?
    public bool isUnique;           // Can the item be duplicated?
    public bool isPerishable;       // Can the item be destroyed?
    public bool isMissionItem;      // Is the item a mission item?
    

    // Trade Related 
    public float BaseValue { get; set; }
    //... other properties that define the item type.
}
// End - ItemData.cs

