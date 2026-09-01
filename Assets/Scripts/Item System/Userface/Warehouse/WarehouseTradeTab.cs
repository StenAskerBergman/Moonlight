using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trade tab: the island's passive buy/sell orders for normal goods, island-wide across
/// its warehouses and Port Authorities. Each row is one rule — good, Buy/Sell/None, and
/// a stock target. The selected warehouse's level caps how many rules may be active at
/// once (2 / 4 / 8, see <see cref="WarehouseSockets.TradeSlots"/>).
///
/// Reads <see cref="IslandTradeRules"/> only; the goods list is used purely to offer
/// candidate commodities, never to imply a rule exists.
/// </summary>
public sealed class WarehouseTradeTab : WarehousePanelTab
{
    [Header("Rule Editor")]
    [SerializeField] private GameObject ruleEditorRoot;
    [SerializeField] private TMP_Text ruleItemNameLabel;
    [SerializeField] private TMP_Dropdown ruleModeDropdown;
    [SerializeField] private Slider ruleStockSlider;
    [SerializeField] private TMP_Text ruleStockLabel;

    [Header("Slots")]
    [SerializeField] private TMP_Text slotCountLabel;

    [Tooltip("Upper bound of the target-stock slider. Should match the island's per-good storage capacity.")]
    [SerializeField] private int maxTargetStock = 1000;

    // Trade is island-wide and not filtered by demographic, so the tier strip is
    // hidden while this tab is open.
    public override bool UsesTierTabs => false;

    public override string TabLabel => "TRADE";

    private ItemData editingItem;

    protected override void Awake()
    {
        base.Awake();

        if (ruleModeDropdown != null)
        {
            ruleModeDropdown.ClearOptions();
            ruleModeDropdown.AddOptions(new List<string> { "No trade", "Buy", "Sell" });
            ruleModeDropdown.onValueChanged.AddListener(OnModeChanged);
        }

        if (ruleStockSlider != null)
        {
            ruleStockSlider.wholeNumbers = true;
            ruleStockSlider.minValue = 0;
            ruleStockSlider.maxValue = maxTargetStock;
            ruleStockSlider.onValueChanged.AddListener(OnStockChanged);
        }

        CloseEditor();
    }

    private void OnDestroy()
    {
        if (ruleModeDropdown != null) ruleModeDropdown.onValueChanged.RemoveListener(OnModeChanged);
        if (ruleStockSlider != null) ruleStockSlider.onValueChanged.RemoveListener(OnStockChanged);
    }

    public override void Rebuild()
    {
        IslandTradeRules rules = Context?.TradeRules;

        if (rules == null)
        {
            HideUnusedSlots(0);
            UpdateSlotCountLabel(0, 0);
            CloseEditor();
            return;
        }

        int used = 0;
        int active = 0;

        foreach (KeyValuePair<ItemData, TradeRule> entry in rules.GetAllRules())
        {
            TradeRule rule = entry.Value;
            if (rule == null || rule.Item == null) continue;
            if (rule.RuleType == TradeRuleType.None) continue;

            WarehouseSlotView slot = TakeSlot(used, s => OpenEditor(s.Item));
            if (slot == null) break;

            int stock = Context.Goods != null ? Context.Goods.GetItemAmount(rule.Item) : 0;
            int target = rule.RuleType == TradeRuleType.PassiveBuy ? rule.TargetStock : rule.MinStockToRetain;

            slot.SetGood(rule.Item, stock, target > 0 ? target : 0);

            used++;
            active++;
        }

        HideUnusedSlots(used);
        UpdateSlotCountLabel(active, Context.Sockets != null ? Context.Sockets.TradeSlots : 0);

        // The rule being edited may have been cleared elsewhere while the panel was open.
        if (editingItem != null) RefreshEditor();
    }

    /// <summary>
    /// Whether another rule may be switched on. Rules already active don't count against
    /// themselves, so an existing rule can always be re-edited or switched off.
    /// </summary>
    public bool HasFreeTradeSlot(ItemData candidate)
    {
        IslandTradeRules rules = Context?.TradeRules;
        int limit = Context?.Sockets != null ? Context.Sockets.TradeSlots : 0;
        if (rules == null || limit <= 0) return false;

        int active = 0;
        foreach (KeyValuePair<ItemData, TradeRule> entry in rules.GetAllRules())
        {
            TradeRule rule = entry.Value;
            if (rule == null || rule.RuleType == TradeRuleType.None) continue;
            if (rule.Item == candidate) return true;
            active++;
        }

        return active < limit;
    }

    public void OpenEditor(ItemData item)
    {
        if (item == null || Context?.TradeRules == null) return;

        editingItem = item;
        if (ruleEditorRoot != null) ruleEditorRoot.SetActive(true);
        RefreshEditor();
    }

    public void CloseEditor()
    {
        editingItem = null;
        if (ruleEditorRoot != null) ruleEditorRoot.SetActive(false);
    }

    private void RefreshEditor()
    {
        if (editingItem == null || Context?.TradeRules == null) return;

        TradeRule rule = Context.TradeRules.GetRule(editingItem);
        if (rule == null) return;

        if (ruleItemNameLabel != null)
        {
            ruleItemNameLabel.text = string.IsNullOrEmpty(editingItem.displayName)
                ? editingItem.name
                : editingItem.displayName;
        }

        // SetValueWithoutNotify on both controls: refreshing the editor must not look
        // like the player moved them, or every rebuild would rewrite the rule.
        if (ruleModeDropdown != null) ruleModeDropdown.SetValueWithoutNotify((int)rule.RuleType);

        if (ruleStockSlider != null)
        {
            int value = rule.RuleType == TradeRuleType.PassiveBuy ? rule.TargetStock : rule.MinStockToRetain;
            ruleStockSlider.SetValueWithoutNotify(Mathf.Clamp(value, 0, maxTargetStock));
        }

        UpdateStockLabel(rule);
    }

    private void OnModeChanged(int modeIndex)
    {
        if (editingItem == null || Context?.TradeRules == null) return;

        TradeRuleType mode = (TradeRuleType)modeIndex;

        if (mode != TradeRuleType.None && !HasFreeTradeSlot(editingItem))
        {
            Debug.Log(
                $"All {(Context.Sockets != null ? Context.Sockets.TradeSlots : 0)} trade slots on " +
                $"'{Context.Building?.name}' are in use. Upgrade the warehouse or clear a rule first.");

            // Snap the dropdown back rather than leaving it showing a rule that wasn't applied.
            RefreshEditor();
            return;
        }

        Context.TradeRules.SetRuleType(editingItem, mode);
        Rebuild();
    }

    private void OnStockChanged(float value)
    {
        if (editingItem == null || Context?.TradeRules == null) return;

        int amount = Mathf.RoundToInt(value);
        TradeRule rule = Context.TradeRules.GetRule(editingItem);
        if (rule == null) return;

        if (rule.RuleType == TradeRuleType.PassiveBuy) Context.TradeRules.SetTargetStock(editingItem, amount);
        else if (rule.RuleType == TradeRuleType.PassiveSell) Context.TradeRules.SetMinStockToRetain(editingItem, amount);

        UpdateStockLabel(rule);
    }

    private void UpdateStockLabel(TradeRule rule)
    {
        if (ruleStockLabel == null) return;

        switch (rule.RuleType)
        {
            case TradeRuleType.PassiveBuy:
                ruleStockLabel.text = $"Buy up to {rule.TargetStock}";
                break;
            case TradeRuleType.PassiveSell:
                ruleStockLabel.text = $"Sell above {rule.MinStockToRetain}";
                break;
            default:
                ruleStockLabel.text = "No active trade rule";
                break;
        }
    }

    private void UpdateSlotCountLabel(int active, int limit)
    {
        if (slotCountLabel != null) slotCountLabel.text = $"{active}/{limit}";
    }
}
