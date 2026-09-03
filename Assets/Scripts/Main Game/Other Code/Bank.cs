using System.Collections.Generic;
using UnityEngine;

public class Bank : MonoBehaviour
{
    public delegate void BankValueChangedHandler();
    public event BankValueChangedHandler OnBankValueChanged;

    private void UpdateBankValues()
    {
        OnBankValueChanged?.Invoke();
    }

    [System.Serializable]
    public class Building
    {
        public Island id;
        public string name;
        public int revenue;
        public int expense;
    }

    [Header("Player Bank System")]
    public int balance;
    public int license;
    public List<Building> buildings;

    /// <summary>Credits on hand. Everything that spends or earns goes through the methods below.</summary>
    public int Balance => balance;

    /// <summary>Licence points on hand.</summary>
    public int License => license;

    /// <summary>How many buildings the bank is tracking.</summary>
    public int BuildingCount => buildings != null ? buildings.Count : 0;

    [Header("Action Data & Settings")]
    public int price;
    public int profit;

    [Header("Time Data & Settings")]
    public float dayLengthInSeconds = 1f;
    private float elapsedTime;
    private int daysPassed;
    [SerializeField] private int daysInMonth = 30;

    private float timeScale = 1f;
    private bool isPaused = false;

    [Header("Player Start Values")]
    [SerializeField] private int startMoney;
    [SerializeField] private int startLicense;
    
    private void OnEnable()
    {
        BuildingCost.OnBuildingPlaced += UpdateBalanceAndRevenue;
    }

    private void OnDisable()
    {
        BuildingCost.OnBuildingPlaced -= UpdateBalanceAndRevenue;
    }
    
    public void AddBuildingToLocalGrid(GameObject buildingInstance, Bank.Building building)
    {
        GridSystem gridSystem = buildingInstance.GetComponentInParent<GridSystem>();
        if (gridSystem != null)
        {
            gridSystem.AddLocalBuilding(building);
        }
    }

    /// <summary>
    /// Starts tracking a placed building's monthly figures.
    ///
    /// It deliberately does NOT charge the build price. This runs from BuildingCost.Start,
    /// a frame after the building was approved, so a deduction here could not be refused
    /// and could not be tied to the check that allowed it. BuildingPlacer charges at the
    /// moment it decides, through Bank.TrySpend.
    /// </summary>
    private void UpdateBalanceAndRevenue(BuildingCost buildingCost)
    {
        Building newBuilding = new Building
        {            
            name = buildingCost.GetBuildingName(),  // Set the building name here or pass it from the BuildingCost script
            revenue = buildingCost.GetRevenue(),    // Set the building revenue here or pass it from the BuildingCost script
            expense = buildingCost.GetExpense()     // Set the building expense here or pass it from the BuildingCost script / Can't Dry this
        };

        buildings.Add(newBuilding);
        AddBuildingToLocalGrid(buildingCost.gameObject, newBuilding); 
        UpdateBankValues();
    }

    void Awake()
    {
        balance = startMoney;
        license = startLicense;
        buildings = new List<Building>();

        // Anything listening for the first value change would otherwise sit on whatever
        // the prefab happened to serialise until the first month ticked over.
        UpdateBankValues();
    }

    #region Credits - read

    /// <summary>Whether the player can currently pay this many credits.</summary>
    public bool CanAfford(int amount)
    {
        return amount <= 0 || balance >= amount;
    }

    /// <summary>Whether the player can pay for this building's credit price.</summary>
    public bool CanAfford(BuildingCost buildingCost)
    {
        return buildingCost == null || CanAfford(buildingCost.GetPrice());
    }

    #endregion

    #region Credits - create / update / delete

    /// <summary>
    /// Pays credits in, e.g. a sale or a refund. Negative amounts are refused rather than
    /// quietly becoming a withdrawal - spending goes through <see cref="TrySpend"/>.
    /// </summary>
    public void Deposit(int amount, string reason = null)
    {
        if (amount < 0)
        {
            Debug.LogWarning($"{name}: Deposit({amount}) refused - use TrySpend to take money out.", this);
            return;
        }
        if (amount == 0) return;

        balance += amount;
        UpdateBankValues();
    }

    /// <summary>
    /// Takes credits out, but only if they are there. Returns false and changes nothing
    /// when the player cannot pay, so a caller can refuse the purchase rather than
    /// discovering a negative balance afterwards.
    /// </summary>
    public bool TrySpend(int amount, string reason = null)
    {
        if (amount <= 0) return true;

        if (!CanAfford(amount))
        {
            Debug.Log($"Not enough credits{(string.IsNullOrEmpty(reason) ? "" : " for " + reason)}: " +
                      $"{amount} needed, {balance} available.");
            return false;
        }

        balance -= amount;
        UpdateBankValues();
        return true;
    }

    /// <summary>Sets the balance outright. For loading a save or a debug command.</summary>
    public void SetBalance(int newBalance)
    {
        if (balance == newBalance) return;

        balance = newBalance;
        UpdateBankValues();
    }

    #endregion

    #region Licence

    public bool HasLicense(int amount)
    {
        return amount <= 0 || license >= amount;
    }

    public void AddLicense(int amount)
    {
        if (amount <= 0) return;

        license += amount;
        UpdateBankValues();
    }

    /// <summary>Spends licence points if there are enough, leaving them untouched if not.</summary>
    public bool TryUseLicense(int amount)
    {
        if (amount <= 0) return true;
        if (!HasLicense(amount)) return false;

        license -= amount;
        UpdateBankValues();
        return true;
    }

    public void SetLicense(int newLicense)
    {
        if (license == newLicense) return;

        license = newLicense;
        UpdateBankValues();
    }

    #endregion

    public void AddBuilding(string name, int revenue, int expense)
    {
        Building newBuilding = new Building
        {
            name = name,
            revenue = revenue,
            expense = expense
        };

        buildings.Add(newBuilding);
        UpdateBankValues();
    }

    public void RemoveBuilding(string name)
    {
        RemoveBuilding(buildings.Find(building => building.name == name));
    }

    /// <summary>
    /// Removes a specific entry. Prefer this over the name overload where the caller
    /// already holds the record - names are not unique, so removing by name takes
    /// whichever one happens to be first in the list.
    /// </summary>
    public void RemoveBuilding(Building building)
    {
        if (building == null || buildings == null) return;
        if (!buildings.Remove(building)) return;

        UpdateBankValues();
    }

    /// <summary>Drops every tracked building, e.g. when a match ends or a save loads.</summary>
    public void ClearBuildings()
    {
        if (buildings == null || buildings.Count == 0) return;

        buildings.Clear();
        UpdateBankValues();
    }

    /// <summary>Finds a tracked building by name.</summary>
    public bool TryGetBuilding(string name, out Building building)
    {
        building = buildings != null ? buildings.Find(b => b.name == name) : null;
        return building != null;
    }

    /// <summary>Read-only view of the tracked buildings.</summary>
    public IReadOnlyList<Building> GetBuildings()
    {
        return buildings;
    }

    /// <summary>Re-states a tracked building's monthly figures.</summary>
    public bool UpdateBuilding(string name, int revenue, int expense)
    {
        Building building;
        if (!TryGetBuilding(name, out building)) return false;

        building.revenue = revenue;
        building.expense = expense;
        UpdateBankValues();
        return true;
    }


    public int CalculateTotalRevenue()
    {
        int totalRevenue = 0;

        foreach (Building building in buildings)
        {
            totalRevenue += building.revenue;
        }

        return totalRevenue;
    }

    public int CalculateTotalExpenses()
    {
        int totalExpenses = 0;

        foreach (Building building in buildings)
        {
            totalExpenses += building.expense;
        }

        return totalExpenses;
    }

    void Update()
    {
        if (!isPaused)
        {
            elapsedTime += Time.deltaTime * timeScale;

            if (elapsedTime >= dayLengthInSeconds)
            {
                elapsedTime -= dayLengthInSeconds;
                daysPassed++;

                if (daysPassed >= daysInMonth)
                {
                    daysPassed = 0;
                    int monthlyRevenue = CalculateTotalRevenue();
                    int monthlyExpenses = CalculateTotalExpenses();
                    balance += (monthlyRevenue - monthlyExpenses);
                    UpdateBankValues();
                }
            }
        }
    }

    /// <summary>Existing callers' name for a deposit. Kept so demolition refunds and
    /// monthly yield do not have to change; both now share one code path.</summary>
    public void AddIncome(int amount)
    {
        Deposit(amount);
    }

    /// <summary>
    /// Stops tracking a demolished building, which removes both its revenue and its
    /// upkeep in one step.
    /// </summary>
    public void UntrackBuilding(BuildingCost buildingCost)
    {
        if (buildingCost == null) return;

        RemoveBuilding(buildingCost.GetBuildingName());
    }

    /// <summary>
    /// Reduces the monthly upkeep of EVERY tracked building by this amount.
    ///
    /// This is not the demolition path, whatever its name suggests - use
    /// <see cref="UntrackBuilding"/> for that. Demolition used to call this, which was
    /// invisible only because every expense was zero; with real upkeep it charges the
    /// removal to every other building the player owns.
    /// </summary>
    public void RemoveMonthlyExpense(int amount)
    {
        if (amount == 0 || buildings == null) return;

        foreach (Building building in buildings)
        {
            building.expense -= amount;
        }
        UpdateBankValues();
    }

    public void SetTimeScale(float newTimeScale)
    {
        timeScale = newTimeScale;
    }

    public int GetIncome()
    {
        return CalculateTotalRevenue();
    }

    public int GetExpense()
    {
        return CalculateTotalExpenses();
    }

    public int GetRevenue()
    {
        return CalculateTotalRevenue();
    }

    public int CalculateDisposableBudget()
    {
        return balance + GetRevenue();
    }

    public int GetBudget()
    {
        return CalculateDisposableBudget();
    }

    /// <summary>
    /// Credits on hand.
    ///
    /// This used to return revenue minus expenses - the monthly NET INCOME, not the
    /// balance - so anything asking the bank how much money the player had got a number
    /// that had nothing to do with what they could spend. That figure is still available
    /// as <see cref="GetNetIncome"/>.
    /// </summary>
    public int GetBalance()
    {
        return balance;
    }

    /// <summary>Monthly revenue minus monthly expenses across every tracked building.</summary>
    public int GetNetIncome()
    {
        return CalculateTotalRevenue() - CalculateTotalExpenses();
    }

    public int GetLicense()
    {
        return license;
    }

    // Paused Section
    private void PauseGame()
    {
        isPaused = true;
    }

    private void UnpauseGame()
    {
        isPaused = false;
    }

}
