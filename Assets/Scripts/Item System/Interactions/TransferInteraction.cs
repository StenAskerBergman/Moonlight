// Start - TransferInteraction.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransferInteraction : MonoBehaviour, ITransferable
{
    public void Transfer(Inventory senderInventory, Inventory receiverInventory, ItemData item, int quantity)
    {
        // Basic transfer logic
    }

    public void TransferRequest(Inventory sender, Inventory receiver, ItemData itemRequest, int quantity, bool isAccepted)
    {
        // Logic for handling a transfer request
    }

    public void TransferOffer(Inventory sender, Inventory receiver, ItemData itemOffer, int quantity, bool isAccepted)
    {
        // Logic for handling a transfer offer
    }

    public void TransferFrom(Inventory fromInventory, Inventory toInventory, ItemData item, int quantity)
    {
        // Logic for transferring from one inventory to another
    }

    public void TransferTo(Inventory toInventory, Inventory fromInventory, ItemData item, int quantity)
    {
        // Alternative logic for transferring to an inventory
    }

    public void TransferClosest(Inventory itemSenderInventory, Inventory itemReceiverInventory, ItemData item, int quantity)
    {
        // Logic for transferring to the closest recipient
    }

    public void TransferAll(Inventory itemSenderInventory, Inventory itemReceiverInventory, ItemData item, int quantity)
    {
        // Logic for transferring all instances of an item
    }

    // Transfer Interceptions - Future Plans...
    // Occurs when an item cannot be transferred
    //public void TransferFail(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity)
    //{
    //    // Logic for transfer failiure    
    //}
    //void TransferDenial(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity)
    //{
    //    // logic for partial transfer
    //}
    //void PartialTransfer(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity)
    //{
    //    // logic for partial transfer
    //}

    // ... Additional methods and logic ...
}

// End - TransferInteraction.cs