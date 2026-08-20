using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Note: 
// Shouldn't Have to Say What Slot to add to - Information Should be passed in
// By itself on arrival using a sorting system for derriving what ItemSlot the
// Item Should go to. This means all Items should be added in a "ToGo" System.

// EMPTY SLOT
// If the Slot is empty, it will be added to the first available slot.
// if a slot has same item and has space, then its considered empty slot.

// FULL SLOT
// If the Slot is Full, it will be added to the next available slot.

// NO SLOT
// If no available slot is found, it will be returned > denied > dropped / Sold in that order.

// PARTIALLY FULL SLOT = EMPTY SLOT 
// If the Slot is not at full capacity yet, then it partially will be added in untill
// the slot capacity is full Then the rest is added to the next available slot.

// SUICIDE
// ONCE FINISHED SHOULD REMOVE ITSELF -> Currently by disabling itself 

// TEMPO:
// Temporary: This is a temporary Solution to global item sender entity that
// sends the items that are needed on Game Start to all units that require it

public class StarterItems : MonoBehaviour
{
    // Variables
    public UnitInventory unitInventory;
    public ItemData[] startItems;
    public ItemData startItem;
    public int startAmount;
    bool logVerify = false;

    public void verifyLogUnitInventory(UnitInventory unitInventory)
    {
       if (unitInventory != null) Debug.Log($"StarterItems: Verify unitInventory: {unitInventory}");
       else Debug.Log($"<color=red><b>MISSING:</b></color> <color=orange>StarterItems: Unit is missing unitInventory!</color>");
    }

    private void Awake()
    {
        // Get the UnitInventory
        if (unitInventory == null) { unitInventory = GetComponent<UnitInventory>(); verifyLogUnitInventory(unitInventory); }
    
        // Log Verified Items
        if (logVerify) {
        
            // Verify All the Starter Items
            foreach (ItemData item in startItems)
            {
                Debug.Log($"Verify Starter items: {item} Amount: {startAmount}st");
            }
        }
    }

    // Add Start Item to UnitInventory
    private void Start()
    {
        // Verifying Unit Inventory
        if (logVerify) verifyLogUnitInventory(unitInventory);

        // Check if UnitInventory Exists & Start Item is assigned
        if (unitInventory == null)
        { Debug.LogError("Unit Inventory not assigned to the boat"); return; }

        if (startItem == null)
        { Debug.LogError("Start item not assigned to the boat"); return; }

        // Add Start Item to UnitInventory
        AddToUnitInventory(startItem, startAmount);
        Debug.Log($"Success! Start item: {startItem} of amount: {startAmount}");

        foreach (ItemData item in startItems)
        {   
            switch (item)
            {
                case null:
                    Debug.Log($"item: {item} is Null");
                    break;

                default:
                    Debug.Log($"item: {item} is not Null");
                    break;
            }
        }

        // Add Each Item in starterItems to UnitInventory
        foreach (ItemData item in startItems)
        {
            Debug.Log($"Adding: Starter item: {item}");
            AddToUnitInventory(item, startAmount);
        }

        // Disable this component once the items have been added
        this.enabled = false;
    }


    public void AddToUnitInventory(ItemData itemData, int amount)
    {
        #region Null + Zero Check 

        // Null Check
        if (itemData == null)
        { Debug.LogError($"Null Start item: {itemData} in amount: {amount} is not assigned to the boat"); return; }

        // Zero Check 
        if (amount <= 0)
        { Debug.LogError($"Zero Start item: {itemData} of amount: {amount}"); return; }

        // Zero Null Check
        if (amount <= 0 || itemData == null) { Debug.LogError($"Start item: {itemData} is Null or its amount: {amount} is Zero!"); return; }

        #endregion

        // Final - Null Check
        if (unitInventory != null)
        {
            // Error here - Time Spent Here: 46h 
            Debug.Log($"<color=orange>StarterItems: </color><color=yellow><b>ATTEMPT:</b> {itemData.displayName} item added to {unitInventory.name}</color>");

            // Report what actually happened. This used to log SUCCESS unconditionally,
            // so a rejected add (full hold, no free slot) still read as a success.
            if (unitInventory.AddItem(itemData, amount, $" StarterItems: "))
            {
                Debug.Log($"<color=green><b>SUCCESS:</b> {itemData.displayName} item added to {unitInventory.name}</color>");
            }
            else
            {
                Debug.LogWarning($"<color=orange><b>REJECTED:</b> {itemData.displayName} x{amount} was not added to {unitInventory.name}</color>");
            }
        }
        else if (unitInventory == null)
        {
            Debug.LogError($"<color=red>unitInventory == Null</color>");
        }
    }

    
}
