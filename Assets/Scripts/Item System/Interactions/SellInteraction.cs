using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SellInteraction : MonoBehaviour, ISellable
{
    public Inventory unitInventory;

    public delegate void SellInteractionHandler(string message);
    public event SellInteractionHandler OnInteractionOccurred;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
    }

    public void SellItem(ItemData item, int quantity, int price)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            // TODO: Add logic to add credits or currency to the player
            OnInteractionOccurred?.Invoke($"Sold {quantity} {item.displayName} for {price} credits");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to sell {quantity} {item.displayName}");
        }
    }
}
