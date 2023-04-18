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

    private void UpdateBalanceAndRevenue(BuildingCost buildingCost)
    {
        balance -= buildingCost.GetPrice();

        Building newBuilding = new Building
        {
            // example
            //name = "", // You should set the building name here or pass it from the BuildingCost script
            //revenue = buildingCost.GetCost(),
            //expense = 0 // You should set the building expense here or pass it from the BuildingCost script
            
            name = buildingCost.GetBuildingName(),
            revenue = buildingCost.GetCost(),
            expense = buildingCost.GetExpense()
        };

        buildings.Add(newBuilding);
        AddBuildingToLocalGrid(buildingCost.gameObject, newBuilding); // Add this line
        UpdateBankValues();
    }

    void Awake()
    {
        balance = startMoney;
        license = startLicense;
        buildings = new List<Building>();
    }

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
        Building buildingToRemove = buildings.Find(building => building.name == name);

        if (buildingToRemove != null)
        {
            buildings.Remove(buildingToRemove);
            UpdateBankValues();
        }
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

    public void AddIncome(int amount)
    {
        balance += amount;
        UpdateBankValues();
    }

    public void RemoveMonthlyExpense(int amount)
    {
        foreach (Building building in buildings)
        {
            building.expense -= amount;
        }
        UpdateBankValues();
    }

    public void PauseGame()
    {
        isPaused = true;
    }

    public void UnpauseGame()
    {
        isPaused = false;
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
        return CalculateTotalRevenue() - CalculateTotalExpenses();
    }

    public int GetBalance()
    {
        return balance;
    }

    public int GetLicense()
    {
        return license;
    }

}
