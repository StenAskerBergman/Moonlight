using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boat : MonoBehaviour
{
    public Inventory inventory;
    public UnitInventory unitInventory;
    
    public ItemData startItem; 

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        unitInventory = GetComponent<UnitInventory>();
    }

    private void Start()
    {
        if (startItem != null)
        {
            if (inventory != null)
            {
                Debug.Log("Adding to Inventory!");
                //AddToInventory(startItem, 1);
            }
            else if (unitInventory != null)
            {
                Debug.Log("Adding to Unit Inventory!");
                //AddToUnitInventory(startItem, 1);
            }
            else
            {
                Debug.LogError("No inventory or unit inventory attached to the boat");
            }
        }
        else
        {
            Debug.LogError("Start item not assigned to the boat");
        }
    }

    // Note: 
    // Shouldn't Have to Say What Slot to add to - Information Should be passed in
    // By itself on arrival using a sorting system for derriving what slot togo to
    public void AddToUnitInventory(ItemData itemData, int amount)
    {
        //if (amount <= 0 || itemData == null) return;
        //if (unitInventory != null) unitInventory.AddItem(itemData, amount);
    }

    public void AddToInventory(ItemData itemData, int amount)
    {
        if (amount <= 0 || itemData == null) return;
        if (inventory != null) inventory.AddItem(itemData, amount);
    }

    // More Ways to Do this
    public void AddToInventory(Inventory inventory)
    {
        inventory.AddItem(startItem, 1);
    }

    public void AddToInventory(UnitInventory unitInventory)
    {
        //unitInventory.AddItem( startItem, 1);
    }

    public void AddToInventory()
    {
        this.GetComponent<Inventory>().AddItem(startItem, 1);
    }

    public void AddToUnitInventory()
    {
       // this.GetComponent<UnitInventory>().AddItem(startItem, 1);
    }

}
