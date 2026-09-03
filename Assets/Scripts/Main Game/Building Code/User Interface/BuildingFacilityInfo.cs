using UnityEngine;

public enum FacilityPanelMode
{
    Production,
    Ecobalance
}

/// <summary>
/// Attached to buildings (or resolved dynamically) to supply data to the
/// Anno 2070-style building selection GUI panels (BuildingFacilityPanelUI).
/// </summary>
[DisallowMultipleComponent]
public class BuildingFacilityInfo : MonoBehaviour
{
    [Header("Facility Type & Category")]
    public FacilityPanelMode panelMode = FacilityPanelMode.Production;
    public string categoryTitle = "PRODUCTION BUILDINGS";
    public string buildingDisplayName = "";
    public Sprite headerIcon;

    [Header("Ecobalance Panel (Mode A)")]
    public Sprite portraitImage;
    public string effectText = "+100";
    public Sprite effectIcon;

    [Header("Production Panel (Mode B)")]
    public Sprite inputIcon;
    public int inputAmount = 594484;
    public string inputLabelOverride = "";

    public Sprite outputIcon;
    public int outputCapacityOverride = 30;

    [Header("Base Financial & Status Stats")]
    public int upkeepCredits = -50;
    public int energyValue = -20;
    public string ecobalanceValue = "-";
    public int maxHealth = 1000;
    public int currentHealth = 1000;

    [Header("Live Production Rate Override (0-100%)")]
    [Range(0f, 100f)]
    public float forcedProductionRate = 100f;
    public bool useForcedProductionRate = false;

    // References to underlying building components
    private Building building;
    private BuildingProductionController productionController;
    private BuildingOutput buildingOutput;
    private BuildingSupply buildingSupply;
    private BuildingCost buildingCost;
    private Damageable damageable;

    private void Awake()
    {
        CacheComponents();
    }

    public void CacheComponents()
    {
        if (building == null) building = GetComponent<Building>();
        if (productionController == null) productionController = GetComponent<BuildingProductionController>();
        if (buildingOutput == null) buildingOutput = GetComponent<BuildingOutput>();
        if (buildingSupply == null) buildingSupply = GetComponent<BuildingSupply>();
        if (buildingCost == null) buildingCost = GetComponent<BuildingCost>();
        if (damageable == null) damageable = GetComponent<Damageable>();

        if (string.IsNullOrEmpty(buildingDisplayName))
        {
            if (building != null && building.buildingData != null && !string.IsNullOrEmpty(building.buildingData.buildingName))
            {
                buildingDisplayName = building.buildingData.buildingName;
            }
            else
            {
                buildingDisplayName = gameObject.name.Replace("(Clone)", "").Trim();
            }
        }
    }

    /// <summary>
    /// Computes current production rate (0 - 100%). If building is paused, inactive,
    /// missing supplies or output is full, returns 0.
    /// </summary>
    public float GetCurrentProductionRate()
    {
        if (useForcedProductionRate) return forcedProductionRate;

        if (building != null)
        {
            if (building.CurrentState != BuildingEnums.BuildingState.Active)
            {
                return 0f;
            }
        }

        if (buildingOutput != null && buildingOutput.IsFull)
        {
            return 0f;
        }

        if (buildingSupply != null && !buildingSupply.HasRequiredSupplies())
        {
            return 0f;
        }

        return 100f;
    }

    public float GetCycleProgress()
    {
        if (GetCurrentProductionRate() <= 0.01f) return 0f;

        if (productionController != null)
        {
            return productionController.CycleProgress;
        }

        return 1f;
    }

    public int GetUpkeepCredits()
    {
        if (buildingCost != null && buildingCost.GetExpense() != 0)
        {
            return -Mathf.Abs(buildingCost.GetExpense());
        }
        return upkeepCredits;
    }

    public int GetEnergyValue()
    {
        return energyValue;
    }

    public string GetEcobalanceValue()
    {
        return ecobalanceValue;
    }

    public int GetCurrentHealth()
    {
        if (damageable != null) return damageable.currentHealth;
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        if (damageable != null) return damageable.totalHealth;
        return maxHealth;
    }

    public int GetCurrentOutputAmount()
    {
        if (buildingOutput != null) return buildingOutput.StoredAmount;
        return 1;
    }

    public int GetOutputCapacity()
    {
        if (buildingOutput != null) return buildingOutput.OutputCapacity;
        return outputCapacityOverride > 0 ? outputCapacityOverride : 30;
    }

    public int GetInputDepositAmount()
    {
        return inputAmount;
    }

    /// <summary>
    /// Ensures any Building has a valid BuildingFacilityInfo attached, creating
    /// or initializing one on the fly if needed.
    /// </summary>
    public static BuildingFacilityInfo ResolveOrCreate(Building building)
    {
        if (building == null) return null;

        BuildingFacilityInfo info = building.GetComponent<BuildingFacilityInfo>();
        if (info != null) return info;

        info = building.gameObject.AddComponent<BuildingFacilityInfo>();
        info.CacheComponents();

        string bName = info.buildingDisplayName.ToLowerInvariant();
        if (bName.Contains("ozone") || bName.Contains("deacidification") || bName.Contains("co2") || bName.Contains("weather"))
        {
            info.panelMode = FacilityPanelMode.Ecobalance;
            info.categoryTitle = "ECOBALANCE BUILDINGS";
            info.effectText = "+100";
            info.ecobalanceValue = "+100";
            info.energyValue = -60;
            info.upkeepCredits = -120;
            info.maxHealth = 3000;
            info.currentHealth = 3000;
        }
        else
        {
            info.panelMode = FacilityPanelMode.Production;
            info.categoryTitle = "PRODUCTION BUILDINGS";
            info.energyValue = -20;
            info.upkeepCredits = -50;
            info.ecobalanceValue = "-";
            info.maxHealth = 1000;
            info.currentHealth = 1000;
            info.inputAmount = 594484;
        }

        return info;
    }
}
