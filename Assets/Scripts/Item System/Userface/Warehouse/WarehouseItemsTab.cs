using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Items tab: socketable/equippable modifiers — warehouse upgrades, seeds, building and
/// vehicle upgrades. Two regions: the selected warehouse's sockets (how many depends on
/// its level) and the island's pool of unsocketed items.
///
/// Reads <see cref="WarehouseSockets"/> and <see cref="IslandItemStorage"/> only. These
/// are not commodities and are never derived from the goods stockpile.
/// </summary>
public sealed class WarehouseItemsTab : WarehousePanelTab
{
    [Header("Sockets")]
    [SerializeField] private RectTransform socketParent;
    [SerializeField] private WarehouseSlotView socketTemplate;

    public override string TabLabel => "ITEMS";

    private readonly List<WarehouseSlotView> socketSlots = new List<WarehouseSlotView>();

    private IslandItemStorage boundStorage;
    private WarehouseSockets boundSockets;

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    protected override void Awake()
    {
        base.Awake();
        if (socketTemplate != null) socketTemplate.gameObject.SetActive(false);
    }

    public override void Rebuild()
    {
        Subscribe();
        RebuildSockets();
        RebuildPool();
    }

    private void RebuildSockets()
    {
        WarehouseSockets sockets = Context?.Sockets;
        int socketCount = sockets != null ? sockets.SocketCount : 0;

        while (socketSlots.Count < socketCount)
        {
            if (socketTemplate == null || socketParent == null) break;

            WarehouseSlotView created = Instantiate(socketTemplate, socketParent);
            created.name = $"Socket ({socketSlots.Count})";
            socketSlots.Add(created);
        }

        for (int i = 0; i < socketSlots.Count; i++)
        {
            WarehouseSlotView slot = socketSlots[i];

            if (i >= socketCount)
            {
                slot.SetEmpty();
                continue;
            }

            int socketIndex = i;
            slot.Initialise(style, _ => OnSocketClicked(socketIndex));
            slot.SetSocket(sockets.GetSocketedItem(socketIndex));
        }
    }

    private void RebuildPool()
    {
        IslandItemStorage storage = Context?.Items;
        int used = 0;

        if (storage != null)
        {
            foreach (KeyValuePair<ItemData, int> entry in storage.GetAllItems())
            {
                ItemData item = entry.Key;
                if (item == null || entry.Value <= 0) continue;
                if (!ListsOnActiveTier(item.unlock)) continue;

                WarehouseSlotView slot = TakeSlot(used, s => OnPoolItemClicked(s.Item));
                if (slot == null) break;

                if (Context.IsUnlocked(item.unlock))
                {
                    // Item stacks have no per-item capacity, so pass 0 to print the raw
                    // count and leave the stock bar hidden.
                    slot.SetGood(item, entry.Value, 0);
                }
                else
                {
                    slot.SetLockedGood(item, item.unlock, ActiveTierPopulation);
                }

                used++;
            }
        }

        HideUnusedSlots(used);
    }

    // Click a filled socket to pull the item back into the island pool.
    private void OnSocketClicked(int socketIndex)
    {
        WarehouseSockets sockets = Context?.Sockets;
        if (sockets == null || Context.Items == null) return;
        if (sockets.IsSocketEmpty(socketIndex)) return;

        sockets.TryUnsocket(socketIndex, Context.Items);
    }

    // Click a pooled item to drop it into the first free socket.
    private void OnPoolItemClicked(ItemData item)
    {
        WarehouseSockets sockets = Context?.Sockets;
        if (item == null || sockets == null || Context.Items == null) return;

        for (int i = 0; i < sockets.SocketCount; i++)
        {
            if (!sockets.IsSocketEmpty(i)) continue;
            sockets.TrySocket(i, item, Context.Items);
            return;
        }
    }

    private void Subscribe()
    {
        IslandItemStorage storage = Context?.Items;
        WarehouseSockets sockets = Context?.Sockets;

        if (boundStorage == storage && boundSockets == sockets) return;

        Unsubscribe();

        boundStorage = storage;
        boundSockets = sockets;

        if (boundStorage != null) boundStorage.ItemsChanged += OnDataChanged;
        if (boundSockets != null) boundSockets.SocketsChanged += OnDataChanged;
    }

    private void Unsubscribe()
    {
        if (boundStorage != null) boundStorage.ItemsChanged -= OnDataChanged;
        if (boundSockets != null) boundSockets.SocketsChanged -= OnDataChanged;

        boundStorage = null;
        boundSockets = null;
    }

    private void OnDataChanged()
    {
        if (!isActiveAndEnabled) return;

        // Only the visuals need refreshing here; re-running Subscribe would be a no-op.
        RebuildSockets();
        RebuildPool();
    }
}
