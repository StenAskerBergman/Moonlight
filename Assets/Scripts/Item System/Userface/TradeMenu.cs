// Start - TradeMenu.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TradeMenu : MonoBehaviour
{
    private TradeInteraction tradeInteraction;

    public GameObject tradeMenuUI; 
    public ItemSlot[] playerItemSlots; 
    public ItemSlot[] npcItemSlots; 
    public Button finalizeTradeButton; 
    public Button cancelTradeButton;

    [Header("Active Trade Settings")]
    public Text totalPriceText;
    public Text offeredGoodsText;
    public Text wantedGoodsText;

    private Inventory currentUnitInventory;
    private Inventory targetNpcInventory;

    private Dictionary<ItemData, int> itemsToBuy = new Dictionary<ItemData, int>();
    private Dictionary<ItemData, int> itemsToSell = new Dictionary<ItemData, int>();

    void Awake()
    {
        if (finalizeTradeButton != null) finalizeTradeButton.onClick.AddListener(FinalizeTrade);
        if (cancelTradeButton != null) cancelTradeButton.onClick.AddListener(Close);
    }

    public void Open(Inventory unitInv, Inventory npcInv, TradeInteraction interaction)
    {
        currentUnitInventory = unitInv;
        targetNpcInventory = npcInv;
        tradeInteraction = interaction;

        itemsToBuy.Clear();
        itemsToSell.Clear();

        if (tradeMenuUI != null) tradeMenuUI.SetActive(true);

        PopulateItemSlots(unitInv, npcInv);
        UpdateTradeSummary();
    }

    public void Close()
    {
        if (tradeMenuUI != null) tradeMenuUI.SetActive(false);
        ClearItemSlots();
    }

    public void PopulateItemSlots(Inventory playerInventory, Inventory npcInventory = null)
    {
        if (playerInventory != null) PopulateSlots(playerItemSlots, playerInventory.GetAllItems());
        if (npcInventory != null) PopulateSlots(npcItemSlots, npcInventory.GetAllItems());
    }

    private void PopulateSlots(ItemSlot[] slots, Dictionary<ItemData, int> items)
    {
        if (slots == null) return;
        int index = 0;
        foreach (var item in items)
        {
            if (index < slots.Length && slots[index] != null)
            {
                slots[index].InitializeSlot(item.Key, item.Value);
                index++;
            }
        }
        for (int i = index; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].ClearSlot();
        }
    }

    private void ClearItemSlots()
    {
        if (playerItemSlots != null)
        {
            foreach (var slot in playerItemSlots) { if (slot != null) slot.ClearSlot(); }
        }
        if (npcItemSlots != null)
        {
            foreach (var slot in npcItemSlots) { if (slot != null) slot.ClearSlot(); }
        }
    }

    public void StageBuyItem(ItemData item, int quantity)
    {
        if (!itemsToBuy.ContainsKey(item)) itemsToBuy[item] = 0;
        itemsToBuy[item] += quantity;
        UpdateTradeSummary();
    }

    public void StageSellItem(ItemData item, int quantity)
    {
        if (!itemsToSell.ContainsKey(item)) itemsToSell[item] = 0;
        itemsToSell[item] += quantity;
        UpdateTradeSummary();
    }

    private void UpdateTradeSummary()
    {
        int totalCost = 0;
        int totalEarnings = 0;

        // In a real scenario, use actual unit prices from ItemData or a market manager
        int defaultPrice = 10; 

        foreach (var kvp in itemsToBuy) totalCost += kvp.Value * defaultPrice;
        foreach (var kvp in itemsToSell) totalEarnings += kvp.Value * defaultPrice;

        if (totalPriceText != null)
        {
            int net = totalEarnings - totalCost;
            totalPriceText.text = $"Net Value: {(net >= 0 ? "+" : "")}{net}";
        }
    }

    public void FinalizeTrade()
    {
        if (currentUnitInventory == null || targetNpcInventory == null || tradeInteraction == null) return;

        // Execute Sells (Player -> NPC)
        foreach (var kvp in itemsToSell)
        {
            tradeInteraction.ExecuteTrade(targetNpcInventory, kvp.Key, kvp.Value);
        }

        // Execute Buys (NPC -> Player)
        // Since tradeInteraction.ExecuteTrade assumes unitInventory -> otherInventory,
        // we can temporarily swap or just call Remove/Add directly.
        foreach (var kvp in itemsToBuy)
        {
            if (targetNpcInventory.CanRemove(kvp.Key, kvp.Value) && currentUnitInventory.CanAdd(kvp.Key, kvp.Value))
            {
                targetNpcInventory.RemoveItem(kvp.Key, kvp.Value);
                currentUnitInventory.AddItem(kvp.Key, kvp.Value);
            }
        }

        Close();
    }

    public void AssignTradeInteraction(TradeInteraction interaction)
    {
        tradeInteraction = interaction;
    }
}
// End - TradeMenu.cs
