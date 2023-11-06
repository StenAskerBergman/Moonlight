using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory; // Drag and drop your Inventory component here. - Can't do this in the editor, we change the inventory too often, so we'll do it in code.
    public Transform itemSlotContainer; // A parent transform containing all item slots. - Can't do this in the editor, we change the inventory too often, so we'll do it in code.
    public GameObject itemSlotPrefab; // This will be your prefab for an item slot. - Can't do this in the editor, we change the inventory too often to know how many, what slot is which, or where it is
    public void UpdateInventoryDisplay(Unit selectedUnit)
    {
        // Use the selectedUnit's inventory to update this UI.
        // Depending on the unit type, you can have specific variations in displaying items.

    }

    private void Start()
    {
        // Subscribe to the inventory change event.
        inventory.OnInventoryChanged += RefreshInventoryDisplay;
    }

    private void OnEnable()
    {
        RefreshInventoryDisplay();
    }

    // Function to refresh the UI display.
    public void RefreshInventoryDisplay()
    {
        // First, clear the existing display.
        foreach (Transform child in itemSlotContainer)
        {
            Destroy(child.gameObject);
        }

        // Fetch all items.
        Dictionary<ItemData, int> items = inventory.GetAllItems();

        // Display all items.
        foreach (var item in items)
        {
            GameObject newItemSlot = Instantiate(itemSlotPrefab, itemSlotContainer);
            Text itemText = newItemSlot.GetComponentInChildren<Text>();

            if (itemText)
            {
                itemText.text = $"{item.Key.displayName} x{item.Value}";
            }

            Button itemButton = newItemSlot.GetComponent<Button>();
            if (itemButton)
            {
                itemButton.onClick.AddListener(() => OnItemSlotClicked(item.Key));
            }
        }
    }

    // This function will be called when an item slot (button) is clicked.
    private void OnItemSlotClicked(ItemData clickedItem)
    {
        // For now, just log the clicked item. 
        // In the future, you can add more functionality here (e.g., selecting the item, showing item details, etc.)
        Debug.Log("Clicked on item: " + clickedItem.displayName);
    }
}
