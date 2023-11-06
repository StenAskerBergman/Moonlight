using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitInteractions : MonoBehaviour
{
    public Inventory unitInventory;

    // Delegate and Event for notifying when an interaction occurs.
    public delegate void UnitInteractionHandler(string message);
    public event UnitInteractionHandler OnInteractionOccurred;


    public void TradeItem(UnitInteractions otherUnit, ItemData item, int quantity)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            otherUnit.unitInventory.AddItem(item, quantity);
            OnInteractionOccurred?.Invoke($"Traded {quantity} {item.displayName} to {otherUnit.name}");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to trade {quantity} {item.displayName}");
        }
    }

    public void RequestItem(UnitInteractions otherUnit, ItemData item, int quantity)
    {
        if (otherUnit.unitInventory.RemoveItem(item, quantity))
        {
            unitInventory.AddItem(item, quantity);
            OnInteractionOccurred?.Invoke($"Requested {quantity} {item.displayName} from {otherUnit.name}");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to request {quantity} {item.displayName}");
        }
    }

    public void ThrowItemAtSea(ItemData item, int quantity)
    {
        // This just removes items from inventory. Actual visualization/interaction with the sea is another step.
        if (unitInventory.RemoveItem(item, quantity))
        {
            // TODO: Add visual representation of items thrown at sea.
            OnInteractionOccurred?.Invoke($"Threw {quantity} {item.displayName} into the sea");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to throw {quantity} {item.displayName}");
        }
    }

    public void SellItem(ItemData item, int quantity, int price)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            // Add credits or currency to the player based on price
            OnInteractionOccurred?.Invoke($"Sold {quantity} {item.displayName} for {price} credits");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to sell {quantity} {item.displayName}");
        }
    }

    public void BuyItem(ItemData item, int quantity, int price)
    {
        // TODO: Check if the player has enough currency, License or credits.
        bool hasEnoughCredits = true; // Placeholder
        bool hasEnoughLicenses = true; // Placeholder

        if (hasEnoughCredits && hasEnoughLicenses)
        {
            unitInventory.AddItem(item, quantity);
            // TODO: Subtract Currency credits from the player. ( Normal items Only )
            // TODO: Subtract License credits from the player. ( Special items Only )

            OnInteractionOccurred?.Invoke($"Bought {quantity} {item.displayName} for {price} credits");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Insufficient funds to buy {quantity} {item.displayName}");
        }
    }

    public void AddItem(ItemData item, int quantity)
    {
        unitInventory.AddItem(item, quantity);
        OnInteractionOccurred?.Invoke($"Added {quantity} {item.displayName} to inventory");
    }

    public void RemoveItem(ItemData item, int quantity)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            OnInteractionOccurred?.Invoke($"Removed {quantity} {item.displayName} from inventory");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to remove {quantity} {item.displayName}");
        }
    }
    public void UseItem(ItemData item, int quantity)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            // Implement item usage logic
        }
    }

    public void Dive()
    {
        // TO dive requires to be a Diver Unit
    }

    public void Build(ItemData item)
    {
        // Maybe something like...
        //if(unitData.unitType == UnitType.Builder)
        //{
            //  Anyway to check if we are building on land or under water?
            //  Anyway to check if we can build on Land or under Water?
        //}
        // TO build the Unit requires to be a Builder Unit
        
    }

    // Additional interactions for diving, building underwater, etc. can be added here.
}
