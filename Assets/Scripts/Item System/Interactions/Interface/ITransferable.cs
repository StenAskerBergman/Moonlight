public interface ITransferable
{
    // Default    
    void Transfer(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);

    // Request & Offer
    void TransferRequest(Inventory Sender, Inventory Receiver, ItemData itemRequest, int quantity, bool isAccepted); // Request: Accept / Deny
    void TransferOffer(Inventory Sender, Inventory Receiver, ItemData itemOffer, int quantity, bool isAccepted);  // Offer: Accept / Deny

    // Generic Transfer Methods
    void TransferFrom(Inventory FromInventory, Inventory ToInventory, ItemData item, int quantity);
    void TransferTo(Inventory ToInventory, Inventory FromInventory, ItemData item, int quantity);

    // Unique Transfer Methods
    void TransferClosest(Inventory ItemSenderInventory, Inventory ItemReceiverInventory, ItemData item, int quantity); // For Nearest Recipient
    void TransferAll(Inventory ItemSenderInventory, Inventory ItemReceiverInventory, ItemData item, int quantity); // For Base Building

    //// Fail Transfer Methods - Future Plans...
    //void TransferInterception(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);
    //void TransferFail(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);
    //void TransferDenial(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);
    //void TransferDenied(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);
    //void PartialTransfer(Inventory SenderInventory, Inventory ReceiverInventory, ItemData item, int quantity);

}