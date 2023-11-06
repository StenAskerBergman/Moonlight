using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class InventoryUIBase : MonoBehaviour
{
    public Inventory inventory;
    public Transform itemSlotContainer;
    public GameObject itemSlotPrefab;
    public int maxUISlots = 10; // Adjust as needed

    protected virtual void Start()
    {
        inventory.OnInventoryChanged += RefreshInventoryDisplay;
    }

    protected virtual void OnEnable()
    {
        RefreshInventoryDisplay();
    }

    public virtual void RefreshInventoryDisplay()
    {
        // Clear previous slots
        ClearSlots();

        Dictionary<ItemData, int> items = inventory.GetAllItems();
        int slotsUsed = 0;

        foreach (var item in items)
        {
            if (slotsUsed >= maxUISlots) break; // Ensure we don't exceed our UI slots

            CreateItemSlot(item);

            slotsUsed++;
        }
    }
    protected virtual void ClearSlots()
    {
        foreach (Transform child in itemSlotContainer)
        {
            Destroy(child.gameObject);
        }
    }

    protected virtual void CreateItemSlot(KeyValuePair<ItemData, int> item)
    {
        GameObject newItemSlot = Instantiate(itemSlotPrefab, itemSlotContainer);
        Text itemText = newItemSlot.GetComponentInChildren<Text>();

        if (itemText)
        {
            itemText.text = FormatItemDisplay(item.Key, item.Value);
        }

        Button itemButton = newItemSlot.GetComponent<Button>();
        if (itemButton)
        {
            itemButton.onClick.AddListener(() => OnItemSlotClicked(item.Key));
        }
    }

    protected virtual string FormatItemDisplay(ItemData item, int quantity)
    {
        return $"{item.displayName} x{quantity}";
    }

    protected virtual void OnItemSlotClicked(ItemData clickedItem)
    {
        Debug.Log("Clicked on item: " + clickedItem.displayName);
    }
}
