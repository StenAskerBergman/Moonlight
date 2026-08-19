// Start - Item.cs
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UnityEngine.UI;
using UnityEngine;


public enum Perishability
{
    // Boolean-Like
    NonPerishable,
    Perishable,

    // Shelf life
    ShortTerm,
    MediumTerm,
    LongTerm
}

[Flags]
public enum Usability
{
    None = 0,
    Consumable = 1,
    Craftable = 2,
    Tradeable = 4,
    // Add other usability flags as needed
}

[System.Serializable][ExecuteAlways]
public class Item : MonoBehaviour, IUniqueIdentifier
{
    // public int quantity { get; set; } // Note: unsure if I should add this or not?

    public ItemData itemData; // Description Reference of the item.
    public string ID { get; private set; }
    public ItemType Type { get; set; }
    public Perishability PerishabilityStatus { get; set; }
    public Usability UsabilityStatus { get; set; }



    // Example methods, the real implementations would be more detailed
    public virtual Item Refine() { return this; }
    public virtual Item Craft(Item otherItem) { return this; }
    public virtual void Consume() { }
    public virtual void Spawn() { }

    private void Awake()
    {
        ID = Guid.NewGuid().ToString(); // Generate a unique ID for this item. ( Using IUniqueIdentifier interface )
        Image icon = GetComponent<Image>();
        if (itemData != null ) icon.sprite = itemData.Icon;
    }
}

// Type
public enum ItemType
{
    Normal,
    Consumable // Generic To be Used By many

    // Add other item types as necessary
}


// The status of the Item ( Seed, Resource, Building, etc )
public enum ItemStatus
{

    New,
    Old,

    Spoiled,
    Rotten,
    Fresh,

    Deactivate,
    Activated,
    Activate,
    Planted,
    Unused,
    Used,

    Destroyed,
    Harvested,
    Consumed,
    PickedUp,
    Spawned,
    Crafted,
    Dropped,
    Traded,
    Bought,
    Sold,

    None,
};


// End - Item.cs