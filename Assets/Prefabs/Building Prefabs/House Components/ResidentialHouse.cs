using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Enums;

#region Enums
// Building Mood - Population Mood
public enum HouseMoods
{
    Happy,
    Pleased,
    Content,
    Displeased,
    Frustrated,
    Angry,
    Leave
}


public enum HouseTier
{
    Tier1,
    Tier2,
    Tier3,
    Tier4,
    Tier5,
    //... and so on
}

#region Faction Houses
public enum EcoHouses
{
    Worker_Shacks,
    Employee_Houses,
    Manager_Apartments,
    Executive_Mansions,
    Investor_Mansions,
    //... and so on
}
public enum TycHouses
{
    Worker_Barracks,
    Employee_Houses,
    Engineer_Apartments,
    Executive_Mansions,
    Architect_Towers,
    //... and so on
}
public enum SciHouses
{
    Assistant_Domiciles,
    Researcher_Apartments,
    Genius_Residences,
    //... and so on
}
#endregion
#region Population
public enum TycoonPopulation
{
    // Tycoon Demographic

    None,           // Start Demographic
    Worker,         // Beginner Demographic
    Employee,       // Intermediate Demographic
    Engineer,       // Advanced Demographic
    Executive,      // Expert Demographic
    Architect,      // Master Demographic

}
public enum EcoPopulation
{
    // Eco Demographic

    None,           // Start Demographic
    Worker,         // Beginner Demographic
    Employee,       // Intermediate Demographic
    Manager,        // Advanced Demographic
    Executive,      // Expert Demographic
    Investor,       // Master Demographic

}
public enum SciencePopulation
{
    // Sci Demographic
    None,               // Default Demographic

    Lab_Assistant,      // (Assistant Domiciles)
    Researcher,         // (Researcher Apartments)
    Genius,             // (Genius Residences)
}
#endregion
public enum Demographics
{

    // Start Demographic
    None,           // For Both
    
    // Beginner Demographic
    Worker,         // For Both

    // Intermediate Demographic
    Employee,       // For Both

    // Advanced Demographic
    Manager,        // For Eco
    Engineer,       // For Tyc
    
    // Expert Demographic
    Executive,      // For Both

    // Master Demographic
    Architect,      // For Eco
    Investor,       // For Tyc

    //... and so on ...

    // Science Demographic

    // Low Tier
    Lab_Assistant,  // For Sci
    // Mid Tier
    Researcher,     // For Sci
    // Big Tier
    Genius,         // For Sci
    
    //... and so on ...

    // All Other Demographic
    Pirate,         // Pirate Demographic
    Merchants,      // Merchant Demographic
    Pioneers,       // Pioneer Demographic
    Tourists,       // Tourist Demographic
    Settlers,       // Settler Demographic
    Scuba,          // Scuba Demographic
    Diplomats,      // Diplomat Demographic
    Divers,         // Diver Demographic
                    //... and so on ...

}

#endregion


[System.Serializable]
public class PopulationTierNeeds
{
    public string Food;
    public string Drink;
    public string Activity;
    public List<string> Lifestyle;
    public string Information;
    public string Participation;
    // ... Add more as needed.
}

[System.Serializable]
public class PopulationTierDetails
{
    public string AscensionRights;
    public string HouseType;
    public List<string> CostMaterials; // e.g. Building modules 2, Tools 1
    public int Pop; // Population
    public string Category; 
    public int Res; // Residents required for this tier
    public int Unlock; // Residents required for a demographic unlock
    public string UnlockIcon; // e.g. Eco-ctr-icon City Center
    public Dictionary<string, int> NeedResourceAmount; // e.g. "Food" : 1
    public Dictionary<string, string> NeedResourceType; // e.g. "Food" : "Fish"
}

public class FactionDemographics
{
    public Faction FactionType;
    public Dictionary<Demographics, PopulationTierDetails> DemographicDetails = new Dictionary<Demographics, PopulationTierDetails>();
}

public class ResidentialHouse : MonoBehaviour
{
    public List<FactionDemographics> AllFactionsDetails = new List<FactionDemographics>();

    public Faction buildingFaction;
    public HouseTier currentTier;

    public List<Need> needs;  // List of all needs for the current tier

    public CityCenter linkedCityCenter;
    public bool isConnectedToCenter;

    public int CurrentPopulation { get; private set; }
    public int MaxPopulation => currentDemographicDetails?.Pop ?? 0;
    public int Happiness { get; private set; }
    public int declineRate;
    public int growthRate;

    private PopulationTierDetails currentDemographicDetails;
    private Demographics ConvertToDemographics(HouseTier tier)
    {
        switch (tier)
        {
            case HouseTier.Tier1:
                return Demographics.Worker;
            case HouseTier.Tier2:
                return Demographics.Employee;
            case HouseTier.Tier3:
                return Demographics.Manager; // Or Engineer based on Faction
                                             // ... add other mappings as needed
            default:
                return Demographics.None; // Default case to avoid errors
        }
    }

    private void Start()
    {
        
        // Initialize based on Building Faction and Tier
        currentDemographicDetails = AllFactionsDetails
            .FirstOrDefault(f => f.FactionType == buildingFaction)?
            .DemographicDetails[ConvertToDemographics(currentTier)];


        // Initialize needs and other values from currentDemographicDetails
        InitializeEcoDemographicDetails();
        InitializeTycoonDemographicDetails();
        InitializeScienceDemographicDetails();
        // Add initialization for other factions here...
    }

    #region Initialize Demographic Details
    
    private void InitializeEcoDemographicDetails()
    {
        FactionDemographics ecoFaction = new FactionDemographics { FactionType = Faction.Eco };

        // Worker demographic details
        ecoFaction.DemographicDetails[Demographics.Worker] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
            {
                { "Food", "Fish" },
                { "Drink", "Tea" },
                { "Activity", "Concert Hall" }
            }
        };

        // Employee demographic details
        ecoFaction.DemographicDetails[Demographics.Employee] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
            {
                { "Food", "Health food" },
                { "Lifestyle", "Communicators" },
                { "Information", "Education Network" }
            }
        };

        // Engineer demographic details
        ecoFaction.DemographicDetails[Demographics.Engineer] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
            {
                { "Food", "Pasta dishes" },
                { "Drink", "Bio drinks" },
                { "Participation", "Congress Center" },
                { "Lifestyle", "Service bots" } 
            }
        };

        // Executive demographic details
        ecoFaction.DemographicDetails[Demographics.Executive] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
            {
                { "Lifestyle", "3D Projectors" },
                { "Lifestyle", "Bio Medication" }
            }
        };

        // Add the faction details to the list
        AllFactionsDetails.Add(ecoFaction);
    }
    private void InitializeTycoonDemographicDetails()
    {
        FactionDemographics tycoonFaction = new FactionDemographics { FactionType = Faction.Tyc };

        // Worker demographic details
        tycoonFaction.DemographicDetails[Demographics.Worker] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
        {
            { "Food", "Fish" },
            { "Drink", "Liquor" },
            { "Activity", "Casino" }
        }
        };

        // Employee demographic details
        tycoonFaction.DemographicDetails[Demographics.Employee] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
        {
            { "Food", "Convenience food" },
            { "Lifestyle", "Plastics" },
            { "Information", "Ministry of Truth" }
        }
        };

        // Engineer demographic details
        tycoonFaction.DemographicDetails[Demographics.Engineer] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
        {
            { "Food", "Luxury meal" },
            { "Drink", "Champagne" },
            { "Participation", "Financial Center" },
            { "Lifestyle", "Service bots" }
        }
        };

        // Executive demographic details
        tycoonFaction.DemographicDetails[Demographics.Executive] = new PopulationTierDetails
        {
            NeedResourceType = new Dictionary<string, string>
        {
            { "Lifestyle", "Jewelry" },
            { "Lifestyle", "Pharmaceuticals" } 
        }
        };

        // Add the faction details to the list
        AllFactionsDetails.Add(tycoonFaction);
    }
    private void InitializeScienceDemographicDetails()
    {
        FactionDemographics scienceFaction = new FactionDemographics { FactionType = Faction.Sci };

        // Define science faction details similarly to Eco...

        AllFactionsDetails.Add(scienceFaction);
    }

    #endregion

    // ... rest of the ResidentialHouse class ...

    public void CheckNeedsFulfillment()
    {
        foreach (var need in needs)
        {
            // Check if need is satisfied
        }
    }

    public void UpgradeHouse()
    {
        // Check if conditions are met
        // If so, upgrade to next tier and re-initialize values
    }

    public void UpdatePopulation()
    {
        if (needs.All(n => n.IsSatisfied))
        {
            CurrentPopulation += growthRate;
        }
        else
        {
            CurrentPopulation -= declineRate;
        }
    }
}
