using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum SettlementStatus
{
    SpecialCase = -1,
    Uninhabited = 0,
    PlayerOwned = 1,
    Hostile = 2,
    Neutral = 3,
    Allied = 4,
    Purchased = 5,
    Intelligence = 6,
    Spies = 7,
    Espionage = 8
}

public class IslandPower : MonoBehaviour
{
    public event Action OnPowerChanged;

    public int IslandSettlement;

    [FormerlySerializedAs("Settled")]
    [SerializeField] private bool settled;
    [FormerlySerializedAs("PowerSpent")]
    [SerializeField] private int powerSpent;
    [FormerlySerializedAs("PowerOutput")]
    [SerializeField] private int powerOutput;
    [FormerlySerializedAs("CurrentPower")]
    [SerializeField] private int currentPower;
    [FormerlySerializedAs("TotalPower")]
    [SerializeField] private int totalPower;

    public bool Settled => settled;
    public int PowerSpent => powerSpent;
    public int PowerOutput => powerOutput;
    public int CurrentPower => currentPower;
    public int TotalPower => totalPower;

    private void Start()
    {
        RecalculateCurrentPower();
    }

    public void SetSettled(bool isSettled)
    {
        if (settled == isSettled) return;

        settled = isSettled;
        NotifyPowerChanged();
    }

    public void AddPower(int amount)
    {
        if (amount == 0) return;

        powerOutput += amount;
        totalPower += amount;
        RecalculateCurrentPower();
        NotifyPowerChanged();
    }

    public void RemovePower(int amount)
    {
        if (amount == 0) return;

        powerOutput -= amount;
        totalPower -= amount;
        RecalculateCurrentPower();
        NotifyPowerChanged();
    }

    public void ConsumePower(int amount)
    {
        if (powerSpent == amount) return;

        powerSpent = amount;
        RecalculateCurrentPower();
        NotifyPowerChanged();
    }

    private void RecalculateCurrentPower()
    {
        currentPower = totalPower - powerSpent;
    }

    private void NotifyPowerChanged()
    {
        OnPowerChanged?.Invoke();
    }

    public int GetCurrentPower() => currentPower;
    public int GetPowerSpent() => powerSpent;
    public int GetTotalPower() => totalPower;
    public int GetMadePower() => powerOutput;
}
