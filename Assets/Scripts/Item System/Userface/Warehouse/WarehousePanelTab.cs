using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for the three warehouse panel views. Each owns exactly one dataset and is
/// rebuilt when the selection, the active tier tab, or its own underlying data changes.
/// </summary>
public abstract class WarehousePanelTab : MonoBehaviour
{
    [SerializeField] protected WarehousePanelStyle style;
    [SerializeField] protected RectTransform contentParent;
    [SerializeField] protected WarehouseSlotView slotTemplate;

    protected WarehousePanelContext Context { get; private set; }

    /// <summary>Active tier tab, or null on tabs that don't filter by demographic.</summary>
    protected WarehouseTierTab ActiveTier { get; private set; }

    /// <summary>Whether this tab is filtered by the tier strip. Trade is island-wide and is not.</summary>
    public virtual bool UsesTierTabs => true;

    /// <summary>Label shown on this tab's button.</summary>
    public abstract string TabLabel { get; }

    private readonly List<WarehouseSlotView> pool = new List<WarehouseSlotView>();

    protected virtual void Awake()
    {
        if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
    }

    public void SetStyle(WarehousePanelStyle panelStyle) => style = panelStyle;

    public void Bind(WarehousePanelContext context, WarehouseTierTab activeTier)
    {
        Context = context;
        ActiveTier = activeTier;
        Rebuild();
    }

    public void SetActiveTier(WarehouseTierTab activeTier)
    {
        if (ActiveTier == activeTier) return;
        ActiveTier = activeTier;
        Rebuild();
    }

    public abstract void Rebuild();

    /// <summary>
    /// Whether an item belongs on the active tier tab. Tabs that don't use the tier strip
    /// list everything.
    /// </summary>
    protected bool ListsOnActiveTier(PopulationUnlock unlock) =>
        !UsesTierTabs || ActiveTier == null || ActiveTier.Lists(unlock);

    /// <summary>Population backing the active tier tab, for locked-slot progress text.</summary>
    protected int ActiveTierPopulation =>
        Context != null && ActiveTier != null
            ? Context.GetPopulation(ActiveTier.Faction, ActiveTier.PrimaryClass)
            : 0;

    /// <summary>
    /// Reuses slot instances across rebuilds. Call <see cref="TakeSlot"/> for each row
    /// you want, then <see cref="HideUnusedSlots"/> with the count you took.
    /// </summary>
    protected WarehouseSlotView TakeSlot(int index, Action<WarehouseSlotView> onClicked)
    {
        while (pool.Count <= index)
        {
            if (slotTemplate == null || contentParent == null) return null;

            WarehouseSlotView created = Instantiate(slotTemplate, contentParent);
            created.name = $"{slotTemplate.name} ({pool.Count})";
            pool.Add(created);
        }

        WarehouseSlotView slot = pool[index];
        slot.Initialise(style, onClicked);
        return slot;
    }


    protected void HideUnusedSlots(int usedCount)
    {
        for (int i = usedCount; i < pool.Count; i++)
        {
            pool[i].SetEmpty();
        }
    }
}
