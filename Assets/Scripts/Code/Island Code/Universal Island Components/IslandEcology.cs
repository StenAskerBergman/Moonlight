using System;
using UnityEngine;
using UnityEngine.Events;

public class IslandEcology : MonoBehaviour
{
    public UnityEvent OnPositiveEco;
    public UnityEvent OnNegativeEco;
    public UnityEvent OnEcoChange;

    public event Action EcologyChanged;

    public int NegEco;
    public int PosEco;
    public int EcoValue;
    public int DefaultEcoValue;

    private void Start()
    {
        EcoValue += DefaultEcoValue;
        NotifyEcologyChanged();
    }

    public void ChangeEco(int amount)
    {
        if (amount > 0)
        {
            PosEco = DefaultEcoValue + amount;
            OnPositiveEco?.Invoke();
        }
        else if (amount < 0)
        {
            NegEco = DefaultEcoValue - amount;
            OnNegativeEco?.Invoke();
        }
        else
        {
            return;
        }

        EcoCalc();
    }

    public void EcoCalc()
    {
        int newEcoValue = NegEco + PosEco;
        if (EcoValue == newEcoValue) return;

        EcoValue = newEcoValue;
        NotifyEcologyChanged();
    }

    private void NotifyEcologyChanged()
    {
        OnEcoChange?.Invoke();
        EcologyChanged?.Invoke();
    }

    public int GetCurrentEco() => EcoValue;
    public int GetPositiveEco() => PosEco;
    public int GetNegativeEco() => NegEco;
}
