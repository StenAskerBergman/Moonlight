using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitInteractions : MonoBehaviour
{
    // TODO: We will need a way to Determine what Inventory Type is being used
    // so that we implement the right logic in the right places in this script.

    private ITradable tradableComponent;
    private IBuildable buildableComponent;
    private IDiveable diveableComponent;
    private IBuyable buyableComponent;
    private ISellable sellableComponent;
    private IItemManagement itemManagementComponent;

    // Delegate and Event for notifying when an interaction occurs.
    public delegate void UnitInteractionHandler(string message);
    public event UnitInteractionHandler OnInteractionOccurred;

    private void Awake()
    {
        // Get components if they exist on the unit
        tradableComponent = GetComponent<ITradable>();
        buildableComponent = GetComponent<IBuildable>();

    }
    public void PerformTrade(UnitInteractions otherUnit, ItemData item, int quantity)
    {
        if (tradableComponent != null)
        {
            tradableComponent.TradeItem(otherUnit, item, quantity);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot trade items.");
        }
    }

    public void PerformBuild(ItemData item)
    {
        if (buildableComponent != null)
        {
            buildableComponent.Build(item);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot build.");
        }
    }

    public void PerformDive()
    {
        if (diveableComponent != null)
        {
            diveableComponent.Dive();
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot dive.");
        }
    }

    public void PerformBuy(ItemData item, int quantity, int price)
    {
        if (buyableComponent != null)
        {
            buyableComponent.BuyItem(item, quantity, price);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot buy items.");
        }
    }

    public void PerformSell(ItemData item, int quantity, int price)
    {
        if (sellableComponent != null)
        {
            sellableComponent.SellItem(item, quantity, price);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot sell items.");
        }
    }

    // NOTE: Unclear what exactly is calling this, onto who
    // and the nuance of that is alarming and game breaking

    public void PerformAddItem(ItemData item, int quantity)
    {
        if (itemManagementComponent != null)
        {
            itemManagementComponent.AddItem(item, quantity);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot add items.");
        }
    }


    public void PerformRemoveItem(ItemData item, int quantity)
    {
        if (itemManagementComponent != null)
        {
            itemManagementComponent.RemoveItem(item, quantity);
        }
        else
        {
            OnInteractionOccurred?.Invoke("This unit cannot remove items.");
        }
    }

    // Additional interactions for the opposite of diving, moving underwater, target, orders,
    // flag, ability, combat, stealth, building underwater, fuel, etc. can be added here.

    // NOTE: Unclear what exactly is calling this, onto who
    // and the nuance of that is alarming and game breaking

    // TODO: Add Target for both add & remove methods
    // Targeting - probably best part of Unit Script.
    //public void PerformTargeting()
    //{
    //    // 
    //    List<GameObject> targetList = new List<GameObject>();
    //        
    //    // Sends all targets within a proximity to sender
    //    var TargetsInRange = itemManagementComponent.Targeting();
    //
    //    // Add Each Target to a Current list
    //    foreach (target in TargetsInRange)
    //    {
    //        targetList.Add(target);
    //    }
    //
    //    // Set Target
    //    itemManagementComponent.Targeting(target);
    //
    //
    //    // Null Check 
    //    if (target != null)
    //    {
    //        // Handle if target comes into range ("New Target In Range")
    //        // Handle if target leaves range ("out of range")
    //
    //        // Handle if target is untargetable
    //        // Handle if target is removed
    //        // Handle if target dies
    //
    //        // Visuals?
    //        // Switching Target? Order of switch? Priority Target?
    //        itemManagementComponent.Target();
    //    }
    //    else
    //    {
    //        OnInteractionOccurred?.Invoke("This unit cannot target.");
    //    }
    //}

}




