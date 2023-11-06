// Start - Item.cs
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
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

public class Item : MonoBehaviour, IUniqueIdentifier
{
    public ItemData itemData; // Description Reference of the item.
    public string ID { get; private set; }
    // public int quantity { get; set; }
    public ItemType Type { get; set; }
    public Perishability PerishabilityStatus { get; set; }
    public Usability UsabilityStatus { get; set; }



    // Example methods, the real implementations would be more detailed
    public virtual Item Refine() { return this; }
    public virtual Item Craft(Item otherItem) { return this; }
    public virtual void Consume() { }


    private void Awake()
    {
        ID = Guid.NewGuid().ToString(); // Generate a unique ID for this item. ( Using IUniqueIdentifier interface )
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