using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    public ItemType? restrictedType; // null means any type
    public Image itemIcon;
    public Text itemQuantity;
    
    public bool CanHold(ItemData itemData)
    {
        return !restrictedType.HasValue || restrictedType.Value == itemData.type;
    }

    public void UpdateSlot(ItemData itemData, int quantity)
    {
        itemIcon.sprite = itemData.Icon;
        itemQuantity.text = quantity.ToString();
    }
}
