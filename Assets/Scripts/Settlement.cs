using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Integrations.Match3;
using UnityEngine;

[Serializable]
public struct SettlementCost
{
    public ItemData item;
    public int amount;
}

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitInventory))]
public class Settlement : MonoBehaviour
{
    [SerializeField] private float settleRange = 15f;
    [SerializeField] private bool transferEntireInventoryOnSettlement = true;
    [SerializeField] private List<SettlementCost> settlementCosts = new List<SettlementCost>();

    private Unit unit;
    private UnitInventory unitInventory;

    private bool tryingToSettle;

    [SerializeField] private LineRenderer settleRangeRenderer;
    [SerializeField] private int circleSegments = 64;
    private void Awake()
    {
        unit = GetComponent<Unit>();
        unitInventory = GetComponent<UnitInventory>();

        DrawSettleRange();
        settleRangeRenderer.enabled = false;
    }

    public bool TryingToSettle()
    {
        return tryingToSettle;
    }


    // black outside radius
    // highlight selected settler
    public void BeginSettlement()
    {
        tryingToSettle = true;
        settleRangeRenderer.enabled = true;
    }

    public void CancelSettlement()
    {
        tryingToSettle = false;
        settleRangeRenderer.enabled = false;
    }

    public bool CanSettle(Island island, out string reason)
    {
        if (unit == null)
        {
            reason = "Unit does not exist.";
            return false;
        }

        if (island == null)
        {
            reason = unit.moveType == MoveType.Submersible
                ? "No plateau in range."
                : "No island in range.";

            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            island.transform.position
        );

        if (distance > settleRange)
        {
            reason = "Out of settlement range.";
            return false;
        }

        if (unitInventory == null)
        {
            reason = "Unit has no inventory.";
            return false;
        }

        foreach (SettlementCost cost in settlementCosts)
        {
            if (cost.item == null || cost.amount <= 0)
                continue;

            if (unitInventory.GetItemQuantity(cost.item) < cost.amount)
            {
                reason = "Not enough " + cost.item.displayName + ".";
                return false;
            }
        }

        reason = null;
        return true;
    }

    //Settlement succeeds
    // -> Always deduct construction costs
    // -> If transferEntireInventoryOnSettlement == true
    // -> move all remaining ship cargo into settlement
    public void CompleteSettlement(Inventory settlementInventory)
    {
        // Always consume construction costs
        foreach (SettlementCost cost in settlementCosts)
        {
            if (cost.item == null || cost.amount <= 0)
                continue;

            unitInventory.RemoveItem(cost.item, cost.amount);
        }

        // Optionally transfer everything remaining
        if (transferEntireInventoryOnSettlement)
        {
            // Transfer remaining cargo here
        }
    }

    private void DrawSettleRange()
    {
        settleRangeRenderer.positionCount = circleSegments + 1;
        settleRangeRenderer.loop = true;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;

            Vector3 position = new Vector3(
                Mathf.Cos(angle) * settleRange,
                0f,
                Mathf.Sin(angle) * settleRange
            );

            settleRangeRenderer.SetPosition(i, position);
        }
    }

    // build a circle around the unit using the line renderer
    // add a shader to black out around the circle 
    // highlight the unit with the build placement order 

    /* 
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Vector3.Distance.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform-position.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Component.GetComponent.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.SetActive.html 
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/LineRenderer.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Material.SetVector.html
     */
}
