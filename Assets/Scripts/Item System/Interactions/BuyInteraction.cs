using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuyInteraction : MonoBehaviour, IBuyable
{
    public Inventory unitInventory;

    public delegate void BuyInteractionHandler(string message);
    public event BuyInteractionHandler OnInteractionOccurred;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
    }

    public void BuyItem(ItemData item, int quantity, int price)
    {
        // TODO: Implement currency check
        bool hasEnoughCredits = true;
        bool hasEnoughLicenses = true;

        if (hasEnoughCredits && hasEnoughLicenses)
        {
            // TODO: Determine Inventory Type
            unitInventory.AddItem(item, quantity);
            OnInteractionOccurred?.Invoke($"Bought {quantity} {item.displayName} for {price} credits");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Insufficient funds to buy {quantity} {item.displayName}");
        }
    }

    //public void RequestItem(UnitInteractions otherUnit, ItemData item, int quantity)
    //{
    //    // if our unit is close enough, connect to the other unit & Request
    //    // to Start Trading Session with the other unit over a spesific item
    //    if (otherUnit.unitInventory.RemoveItem(item, quantity))
    //    {
    //        unitInventory.AddItem(item, quantity);
    //        OnInteractionOccurred?.Invoke($"Requested {quantity} {item.displayName} from {otherUnit.name}");
    //    }
    //    else
    //    {
    //        OnInteractionOccurred?.Invoke($"Failed to request {quantity} {item.displayName}");
    //    }
    //}
}