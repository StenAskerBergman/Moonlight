using UnityEngine;

/// <summary>
/// Hit points for a unit. Units had none - only buildings did, via BuildingSimulation -
/// so there was nothing for a death to be the consequence of. Deliberately shaped like
/// BuildingSimulation's health section so the two read the same way.
///
/// This owns "is it dead", not "what does dying look like": the presentation lives in
/// <see cref="UnitDeath"/>, which listens to <see cref="OnDied"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class UnitHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }

    public delegate void HealthChangedHandler(int current, int max);
    public event HealthChangedHandler OnHealthChanged;

    public delegate void DiedHandler(UnitHealth unit);
    public event DiedHandler OnDied;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Kill();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Kills outright, whatever the remaining health. This is what the delete command
    /// calls. Guarded so a unit can only die once - the death sequence destroys the
    /// object at its own pace, and a second call during that window would restart it.
    /// </summary>
    public void Kill()
    {
        if (IsDead) return;

        IsDead = true;
        currentHealth = 0;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDied?.Invoke(this);
    }
}
