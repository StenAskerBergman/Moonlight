using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unloads a vessel's cargo into the nearest in-range <see cref="Depot"/>.
///
/// This is the path that was missing: TransferInteraction's methods are all empty
/// stubs and nothing in the project ever called Depot.InteractWithInventory, so cargo
/// could be picked up but never handed over. Shaped like BuildInteraction (CanDeliver /
/// Deliver) so the unit action bar can drive it the same way.
/// </summary>
[DisallowMultipleComponent]
public sealed class DeliverInteraction : MonoBehaviour
{
    [Tooltip("How close the vessel must be to a depot to unload into it.")]
    [SerializeField, Min(1f)] private float deliveryRange = 20f;

    private Inventory unitInventory;
    private UnitInventory unitCargo;

    public delegate void DeliveredHandler(Depot depot, int itemsMoved);
    public event DeliveredHandler OnDelivered;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
        unitCargo = GetComponent<UnitInventory>();
    }

    /// <summary>Nearest depot within range, or null.</summary>
    public Depot FindTargetDepot()
    {
        Depot best = null;
        float bestDistance = float.MaxValue;
        Vector3 position = transform.position;

        foreach (Depot depot in FindObjectsOfType<Depot>())
        {
            if (depot == null) continue;

            float distance = Vector3.Distance(position, depot.transform.position);
            if (distance > deliveryRange || distance >= bestDistance) continue;

            bestDistance = distance;
            best = depot;
        }

        return best;
    }

    public bool CanDeliver()
    {
        if (FindTargetDepot() == null) return false;

        foreach (KeyValuePair<ItemData, int> entry in GetCargoSnapshot())
        {
            if (entry.Key != null && entry.Value > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Moves everything the vessel is carrying into the depot. Each item is removed from
    /// the vessel only after the depot has accepted it, so a full island stockpile leaves
    /// the cargo on the boat instead of deleting it.
    /// </summary>
    public int DeliverAll()
    {
        Depot depot = FindTargetDepot();
        if (depot == null) return 0;

        int moved = 0;

        // Snapshot first: depositing mutates the inventory being enumerated.
        foreach (KeyValuePair<ItemData, int> entry in GetCargoSnapshot())
        {
            ItemData item = entry.Key;
            int amount = entry.Value;
            if (item == null || amount <= 0) continue;

            if (!depot.CanAccept(item, amount)) continue;
            if (!RemoveFromCargo(item, amount)) continue;

            if (!depot.InteractWithInventory(item, amount))
            {
                // Put it back rather than losing it if the depot refused after the fact.
                AddToCargo(item, amount);
                continue;
            }

            moved += amount;
        }

        if (moved > 0) OnDelivered?.Invoke(depot, moved);
        return moved;
    }

    /// <summary>Moves one item stack, for a UI that unloads a single slot.</summary>
    public bool Deliver(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;

        Depot depot = FindTargetDepot();
        if (depot == null || !depot.CanAccept(item, amount)) return false;
        if (!RemoveFromCargo(item, amount)) return false;

        if (!depot.InteractWithInventory(item, amount))
        {
            AddToCargo(item, amount);
            return false;
        }

        OnDelivered?.Invoke(depot, amount);
        return true;
    }

    private Dictionary<ItemData, int> GetCargoSnapshot()
    {
        if (unitCargo != null) return new Dictionary<ItemData, int>(unitCargo.GetAllItems());
        if (unitInventory != null) return new Dictionary<ItemData, int>(unitInventory.GetAllItems());
        return new Dictionary<ItemData, int>();
    }

    private bool RemoveFromCargo(ItemData item, int amount)
    {
        if (unitCargo != null) return unitCargo.RemoveItem(item, amount);
        if (unitInventory != null) return unitInventory.RemoveItem(item, amount);
        return false;
    }

    private void AddToCargo(ItemData item, int amount)
    {
        if (unitCargo != null) unitCargo.AddItem(item, amount, nameof(DeliverInteraction));
        else if (unitInventory != null) unitInventory.AddItem(item, amount);
    }
}
