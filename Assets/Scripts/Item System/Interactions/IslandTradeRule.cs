using System.Collections.Generic;
using UnityEngine;
using System;

public enum TradeRuleType
{
    None,
    PassiveBuy,
    PassiveSell
}

[Serializable]
public class TradeRule
{
    public ItemData Item;
    public TradeRuleType RuleType;
    public int TargetStock;      // For PassiveBuy: buy up to this amount
    public int MinStockToRetain; // For PassiveSell: sell any excess above this amount

    public TradeRule(ItemData item)
    {
        Item = item;
        RuleType = TradeRuleType.None;
        TargetStock = 0;
        MinStockToRetain = 0;
    }
}

public class IslandTradeRules : MonoBehaviour
{
    // Dictionary mapping ItemData to their specific trade rules on this island
    private Dictionary<ItemData, TradeRule> rules = new Dictionary<ItemData, TradeRule>();

    public TradeRule GetRule(ItemData item)
    {
        if (item == null) return null;
        
        if (!rules.ContainsKey(item))
        {
            rules[item] = new TradeRule(item);
        }
        return rules[item];
    }

    public void SetRuleType(ItemData item, TradeRuleType type)
    {
        if (item == null) return;
        var rule = GetRule(item);
        rule.RuleType = type;
    }

    public void SetTargetStock(ItemData item, int amount)
    {
        if (item == null) return;
        var rule = GetRule(item);
        rule.TargetStock = Mathf.Max(0, amount);
    }

    public void SetMinStockToRetain(ItemData item, int amount)
    {
        if (item == null) return;
        var rule = GetRule(item);
        rule.MinStockToRetain = Mathf.Max(0, amount);
    }

    public Dictionary<ItemData, TradeRule> GetAllRules()
    {
        return rules;
    }
}
