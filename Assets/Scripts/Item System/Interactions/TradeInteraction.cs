// Start - TradeInteraction.cs
using UnityEngine;

public class TradeInteraction : MonoBehaviour, ITradable
{
    public Inventory unitInventory;
    public TradeMenu tradeMenu;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
    }

    // Delegate and Event for notifying when an interaction occurs.
    public delegate void TradeInteractionHandler(string message);
    public event TradeInteractionHandler OnInteractionOccurred;

    // Trade Item
    public void TradeItem(UnitInteractions otherUnit, ItemData item, int quantity)
    {
        if (unitInventory.RemoveItem(item, quantity))
        {
            otherUnit.GetComponent<Inventory>().AddItem(item, quantity);
            OnInteractionOccurred?.Invoke($"{gameObject.name} traded {quantity} {item.displayName} to {otherUnit.name}");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to trade {quantity} {item.displayName}");
        }
    }
    public void ExecuteTrade(Inventory otherInventory, ItemData item, int quantity)
    {

        // Check if both inventories can handle the trade
        if (unitInventory.CanRemove(item, quantity) && otherInventory.CanAdd(item, quantity))
        {
            // TODO: Determine Inventory Type
            unitInventory.RemoveItem(item, quantity);
            otherInventory.AddItem(item, quantity);

            // Notify success
            OnInteractionOccurred?.Invoke($"Successfully traded {quantity} {item.displayName}");
        }
        else
        {
            // Notify failure
            OnInteractionOccurred?.Invoke($"Trade failed for {quantity} {item.displayName}");
        }
    }

    // Assuming RequestTrade is the method to initiate a trade session
    public void RequestTrade(UnitInteractions otherUnit, ItemData item, int quantity)
    {
        // Check if the unit is close enough to initiate a trade
        if (IsCloseToTradePoint(otherUnit))
        {
            // Logic to initiate trade session
            OnInteractionOccurred?.Invoke($"Request to trade {quantity} {item.displayName} with {otherUnit.name} sent.");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Too far to initiate trade with {otherUnit.name}.");
        }
    }

    public void OpenTradeMenu(Inventory targetInventory = null)
    {
        if(/*tradeMenu is Closed && player clicks to open trade menu on another islands then*/true)
        {
            // Allow to Open the trade menu
            tradeMenu.Open(unitInventory, targetInventory, this);
            // if unit close enough with inventory
            // Begin a open trade session with the
            // npc's trade point, and cancel if the
            // unit is to far away from trade point
        }
    }

    private bool IsCloseToTradePoint(UnitInteractions otherUnit)
    {
        // Implement logic to check if the unit is close enough to a trade point
        // This might involve checking the distance to a designated trade point object

        // Logic to check proximity
        // Placeholder for proximity check
        return Vector3.Distance(transform.position, otherUnit.transform.position) <= 10.0f; // Example distance check
    }

    public void CloseTradeMenu()
    {
        // Logic for closing the trade menu
        // Handle returning items to inventory if the deal is not finalized
        tradeMenu.Close();
    }

    public void FinalizeTrade()
    {
        // Logic to finalize the trade
        // Check if the trade is affordable and if there is enough space in the inventory
        // If all conditions are met, complete the trade and apply any additional effects (like influence points)
    }

    // Additional methods for handling specific trade menu interactions can be added here

    // Method to handle the unit moving too far from the trade point
    public void HandleUnitMovedAwayFromTradePoint()
    {
        // If the unit moves too far away, close the trade menu and cancel the deal
        CloseTradeMenu();
    }
}
// End - TradeInteraction.cs
