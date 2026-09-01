using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages vehicle upgrade / equipment item slots independently from cargo slots.
/// Each unit can hold up to 3 upgrade items (e.g. engine tuning, armor plating).
/// </summary>
public class UnitEquipment : MonoBehaviour
{
    [SerializeField] private int slotCapacity = 1;
    [SerializeField] private ItemData[] equippedItems;

    public int SlotCapacity => slotCapacity;
    public IReadOnlyList<ItemData> EquippedItems => equippedItems;

    public event Action OnEquipmentChanged;

    private void Awake()
    {
        if (equippedItems == null || equippedItems.Length != slotCapacity)
        {
            Array.Resize(ref equippedItems, slotCapacity);
        }
    }

    public void ConfigureSlots(int count)
    {
        count = Mathf.Clamp(count, 0, 3);
        slotCapacity = count;
        Array.Resize(ref equippedItems, count);
        OnEquipmentChanged?.Invoke();
    }

    public bool EquipItem(int slotIndex, ItemData item)
    {
        if (slotIndex < 0 || slotIndex >= slotCapacity) return false;
        equippedItems[slotIndex] = item;
        OnEquipmentChanged?.Invoke();
        return true;
    }

    public ItemData UnequipItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCapacity) return null;
        ItemData previous = equippedItems[slotIndex];
        equippedItems[slotIndex] = null;
        OnEquipmentChanged?.Invoke();
        return previous;
    }

    public ItemData GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCapacity) return null;
        return equippedItems[slotIndex];
    }
}
